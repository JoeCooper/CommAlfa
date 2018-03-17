using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Models;
using Server.Services;
using System.Linq;
using System.IO;

namespace Server.UnitTests.Services
{
	public class FalseDatabaseService: IDatabaseService
	{
		public class DocumentBodyRow {
			public DocumentBodyRow(MD5Sum id, string body)
			{
				Id = id;
				Body = body;
			}
			public MD5Sum Id { get; }
			public string Body { get; }
		}

		readonly List<Account> account;
		readonly List<Relation> relation;
		readonly List<DocumentMetadata> documentMetadata;
		readonly List<DocumentBlock> documentBlock;
		readonly List<DocumentBodyRow> documentBody;

		public FalseDatabaseService() : this(
			new List<Account>(),
			new List<Relation>(),
			new List<DocumentMetadata>(),
			new List<DocumentBlock>(),
			new List<DocumentBodyRow>())
		{
		}

		public FalseDatabaseService(
			List<Account> account,
			List<Relation> relation,
			List<DocumentMetadata> documentMetadata,
			List<DocumentBlock> documentBlock,
			List<DocumentBodyRow> documentBody)
		{
			this.account = account;
			this.relation = relation;
			this.documentMetadata = documentMetadata;
			this.documentBlock = documentBlock;
			this.documentBody = documentBody;
		}

		public async Task<MD5Sum> AddDocumentAsync(Guid authorId, string body, string title, IEnumerable<MD5Sum> antecedents)
		{
			if (account.Any(a => a.Id.Equals(authorId)))
			{
				throw new Exception();
			}
			var submissionId = MD5Sum.Encode(title + body);
			if(!documentMetadata.Any(d => d.Id.Equals(submissionId)))
			{
				documentMetadata.Add(new DocumentMetadata(submissionId, title, authorId, DateTime.UtcNow));
				documentBody.Add(new DocumentBodyRow(submissionId, body));
				relation.AddRange(antecedents.Select(a => new Relation(a, submissionId)));
			}
			await Task.Yield();
			return submissionId;
		}

		public async Task<Account> GetAccountAsync(Guid id)
		{
			await Task.Yield();
			if(account.Any(a => a.Id.Equals(id))) {
				return account.Single(a => a.Id.Equals(id));
			}
			throw new FileNotFoundException();
		}

		public async Task<IEnumerable<MD5Sum>> GetDescendantIds(MD5Sum id)
		{
			await Task.Yield();
			return relation.Where(r => r.AntecedentId.Equals(id)).Select(r => r.DescendantId);
		}

		public async Task<string> GetDocumentBodyAsync(MD5Sum id, bool ignoreBlock = false)
		{
			await Task.Yield();
			if(documentBlock.Any(d => d.Id.Equals(id))) {
				var block = documentBlock.Single(d => d.Id.Equals(id));
				throw new DocumentBlockedException(block.IsVoluntary);
			}
			if(documentBody.Any(d => d.Id.Equals(id)))
			{
				return documentBody.Single(d => d.Id.Equals(id)).Body;
			}
			throw new FileNotFoundException();
		}

		public async Task<DocumentMetadata> GetDocumentMetadataAsync(MD5Sum id)
		{
			await Task.Yield();
			if (documentBody.Any(d => d.Id.Equals(id)))
			{
				return documentMetadata.Single(d => d.Id.Equals(id));
			}
			throw new FileNotFoundException();
		}

		public async Task<IEnumerable<MD5Sum>> GetDocumentsForAccountAsync(Guid id)
		{
			await Task.Yield();
			return documentMetadata.Where(d => d.AuthorId.Equals(id)).Select(d => d.Id);
		}

		public async Task<IEnumerable<Relation>> GetFamilyAsync(MD5Sum familyMemberId)
		{
			await Task.Yield();
			var builder = new HashSet<Relation>();
			var closedSet = new HashSet<MD5Sum>();
			var openSet = new HashSet<MD5Sum>();
			openSet.Add(familyMemberId);
			while(openSet.Any()) {
				closedSet.UnionWith(openSet);
				var relevant = relation.Where(r => openSet.Contains(r.AntecedentId) || openSet.Contains(r.DescendantId));
				openSet.UnionWith(relation.Select(r => r.AntecedentId));
				openSet.UnionWith(relation.Select(r => r.DescendantId));
				builder.UnionWith(relevant);
				openSet.ExceptWith(closedSet);
			}
			return builder;
		}

        public async Task<Reader<DocumentMetadata>> GetDocumentMetadataAsync()
        {
            await Task.Yield();
            var enumerator = documentMetadata.GetEnumerator();
            return new Reader<DocumentMetadata>(null, async () =>
            {
                await Task.Yield();
                return enumerator.MoveNext();
            }, () => enumerator.Current);
            throw new NotImplementedException();
        }

        public Task<Account> GetAccountAsync(string email)
        {
            throw new NotImplementedException();
        }

        public async Task SaveAccountAsync(Account account, bool onlyNew)
        {
            await Task.Yield();
            if (onlyNew && this.account.Any(a => a.Id.Equals(account.Id)))
            {
                throw new DuplicateKeyException();
            }
            var extant = this.account.SingleOrDefault(a => a.Id.Equals(account.Id));
            var indexOfExtant = this.account.IndexOf(extant);
            if(indexOfExtant >= 0)
            {
                this.account[indexOfExtant] = account;
            }
            else
            {
                this.account.Add(account);
            }
        }
	}
}
