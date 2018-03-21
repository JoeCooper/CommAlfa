using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Server.Models;

namespace Server.Services
{
	public interface IDatabaseService
	{
		Task<MD5Sum> AddDocumentAsync(Guid authorId, string body, string title, IEnumerable<MD5Sum> antecedents);
		Task<Account> GetAccountAsync(Guid id);
        Task<Account> GetAccountAsync(string email);
        Task SaveAccountAsync(Account account, bool onlyNew);
		Task<IEnumerable<MD5Sum>> GetDocumentsForAccountAsync(Guid id);
		Task<IEnumerable<MD5Sum>> GetDescendantIds(MD5Sum documentId);
        Task<DocumentMetadata> GetDocumentMetadataAsync(MD5Sum id);
        Task<Reader<DocumentMetadata>> GetDocumentMetadataAsync();
		Task<string> GetDocumentBodyAsync(MD5Sum id, bool ignoreBlock = false);
        Task<IEnumerable<Relation>> GetFamilyAsync(MD5Sum familyMemberId);
	}
}
