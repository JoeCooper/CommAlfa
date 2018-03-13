using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Models;

namespace Server.Services
{
	public interface IDatabaseService
	{
		Task<MD5Sum> AddDocumentAsync(Guid authorId, string body, string title, IEnumerable<MD5Sum> antecedents);
		Task<Account> GetAccountAsync(Guid id);
		Task<DocumentMetadata> GetDocumentMetadataAsync(MD5Sum id);
		Task<string> GetDocumentBodyAsync(MD5Sum id, bool ignoreBlock = false);
		Task<DocumentBlock> GetDocumentBlockAsync(MD5Sum id);
		Task<IEnumerable<Relation>> GetFamilyAsync(MD5Sum familyMemberId);
	}
}
