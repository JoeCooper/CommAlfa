using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using Server.Models;
using Server.ViewModels;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Reflection;
using System.IO;
using Server.Utilities;
using Server.Services;

namespace Server.Controllers
{
    [Route("documents")]
    public class DocumentController : Controller
    {
        readonly static string NewDocumentBody;

        static DocumentController() {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Server.New.md";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                NewDocumentBody = reader.ReadToEnd();
            }
        }

		readonly IDatabaseService databaseService;
        readonly DatabaseConfiguration databaseConfiguration;
		readonly InputConfiguration inputConfiguration;

		public DocumentController(IDatabaseService databaseService, IOptions<DatabaseConfiguration> _databaseConfiguration, IOptions<InputConfiguration> _inputConfiguration)
		{
			this.databaseService = databaseService;
            databaseConfiguration = _databaseConfiguration.Value;
			inputConfiguration = _inputConfiguration.Value;
        }

        [HttpPost("{id}/edit")]
        [Authorize]
        public async Task<IActionResult> Save(string id, DocumentSubmissionModel submissionModel)
		{
            submissionModel.AntecedentIdBase64 = submissionModel.AntecedentIdBase64 ?? Enumerable.Empty<string>();
            
            if (submissionModel.AntecedentIdBase64.Count() > 2)
            {
                return BadRequest();
            }

			if(submissionModel.AntecedentIdBase64.Any(_id => _id.FalsifyAsIdentifier()))
			{
				return BadRequest();
			}

            submissionModel.Title = submissionModel.Title ?? string.Empty;
            submissionModel.Body = submissionModel.Body ?? string.Empty;

            submissionModel.Title = submissionModel.Title.Trim();
            submissionModel.Body = submissionModel.Body.Trim();

			if(submissionModel.Title.Length > inputConfiguration.TitleLengthLimit) {
				return StatusCode(413);
			}

			if(submissionModel.Body.Length > inputConfiguration.BodyLengthLimit) {
				return StatusCode(413);
			}

			var antecedantIds = ImmutableArray.CreateRange(submissionModel.AntecedentIdBase64.Select(s => new MD5Sum(WebEncoders.Base64UrlDecode(s))));

            Guid authorId;

            {
                var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);

                authorId = Guid.Parse(nameIdentifierClaim.Value);
            }

			var submissionId = await databaseService.AddDocumentAsync(authorId, submissionModel.Body, submissionModel.Title, antecedantIds);

			return RedirectToAction(nameof(GetDocument), new { id = submissionId.ToString() });
		}

		[Authorize]
		[HttpGet("{id}/edit")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public IActionResult Edit(string id)
		{
			if (id.Equals("new"))
				return View("New", new DocumentViewModel(NewDocumentBody, string.Empty));
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			return View("Edit", new IdentifierViewModel(id));
		}

        [HttpGet("{id}")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public IActionResult GetDocument(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			return View("Document", new IdentifierViewModel(id));
		}

		[HttpGet("{id}/indexable")]
		[ResponseCache(CacheProfileName = CacheProfileNames.SemiImmutable)]
		public async Task<IActionResult> GetDocumentForIndexing(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idMD5 = new MD5Sum(idInBinary);
			var metadata = await databaseService.GetDocumentMetadataAsync(idMD5);
			var body = await databaseService.GetDocumentBodyAsync(idMD5);
			return View("DocumentForIndexing", new DocumentViewModel(body, metadata.Title));
		}

		async Task<IEnumerable<DocumentListingViewModel>> GetDocumentListingsAsync(NpgsqlConnection connection, IEnumerable<MD5Sum> keys)
		{
			var boxedKeys = keys.Select(m => m.ToGuid()).ToArray();

			using (var cmd = new NpgsqlCommand())
			{
				cmd.Connection = connection;
				cmd.CommandText = "SELECT id, title, authorId, timestamp FROM document WHERE ARRAY[id] && @closedSet;";
				cmd.Parameters.AddWithValue("@closedSet", boxedKeys);

				using (var reader = await cmd.ExecuteReaderAsync())
				{
					var documentsBuilder = ImmutableArray.CreateBuilder<DocumentListingViewModel>();

					while (await reader.ReadAsync())
					{
						documentsBuilder.Add(
							new DocumentListingViewModel(
								new MD5Sum(reader.GetGuid(0).ToByteArray()),
								reader.GetString(1),
								reader.GetGuid(2),
								reader.GetDateTime(3)
							)
						);
					}

					return documentsBuilder;
				}
			}
		}

		async Task<IEnumerable<AccountListingViewModel>> GetAccountListingsAsync(NpgsqlConnection connection, IEnumerable<Guid> keys)
		{
			using (var cmd = new NpgsqlCommand())
			{
				var accountListingsBuilder = ImmutableArray.CreateBuilder<AccountListingViewModel>();

				cmd.Connection = connection;
				cmd.CommandText = "SELECT id, displayName, email FROM account WHERE ARRAY[id] && @authorIds;";
				cmd.Parameters.AddWithValue("@authorIds", keys.ToArray());

				using (var reader = await cmd.ExecuteReaderAsync())
				{
					while (await reader.ReadAsync())
					{
						var accountId = reader.GetGuid(0);
						var displayName = reader.GetString(1);
						var email = reader.GetString(2);
						var gravatarHash = email.ToGravatarHash();
						accountListingsBuilder.Add(new AccountListingViewModel(accountId, displayName, gravatarHash));
					}
				}

				return accountListingsBuilder;
			}
		}

		IEnumerable<DocumentListingViewModel> EnrichDocumentListings(IEnumerable<DocumentListingViewModel> documentListings,
		                                                             IEnumerable<AccountListingViewModel> accountListings)
		{
			var authorNames = ImmutableDictionary.CreateRange(accountListings.Select(a => new KeyValuePair<Guid, string>(a.Id, a.DisplayName)));
			return documentListings.Select(d => d.WithAuthorDisplayName(authorNames[d.AuthorId]));
		}

		[HttpGet("{id}/history")]
		public async Task<IActionResult> GetHistory(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idInMD5Sum = new MD5Sum(idInBinary);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);

			IEnumerable<DocumentListingViewModel> documentsInFamily;
			IEnumerable<AccountListingViewModel> accountListings;
			IEnumerable<Relation> relations;

			using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await conn.OpenAsync();

				relations = await databaseService.GetFamilyAsync(idInMD5Sum);

				{
					var allIdsBuilder = ImmutableHashSet.CreateBuilder<MD5Sum>();
					allIdsBuilder.UnionWith(relations.Select(r => r.AntecedentId));
					allIdsBuilder.UnionWith(relations.Select(r => r.DescendantId));
					allIdsBuilder.Add(idInMD5Sum);
					documentsInFamily = await GetDocumentListingsAsync(conn, allIdsBuilder);
				}

				accountListings = await GetAccountListingsAsync(conn, documentsInFamily.Select(d => d.AuthorId));
			}

			IImmutableSet<Guid> contributorIds;
			{
				var closedSet = ImmutableHashSet.CreateBuilder<MD5Sum>();
				var openSet = ImmutableHashSet.CreateBuilder<MD5Sum>();

				openSet.Add(new MD5Sum(idInBinary));

				do
				{
					var antecedentIds = ImmutableArray.CreateRange(relations.Where(r => openSet.Contains(r.DescendantId)).Select(r => r.AntecedentId));
					closedSet.UnionWith(openSet);
					openSet.UnionWith(antecedentIds);
					openSet.ExceptWith(closedSet);
				} while (openSet.Count > 0);

				var documentsInHistory = documentsInFamily.Where(d => closedSet.Contains(d.Id));
				contributorIds = ImmutableHashSet.CreateRange(documentsInHistory.Select(d => d.AuthorId));
			}

			documentsInFamily = EnrichDocumentListings(documentsInFamily, accountListings);

			var contributorListings = accountListings.Where(c => contributorIds.Contains(c.Id));

			var subject = documentsInFamily.Single(d => d.Id.Equals(idInBinary));

			return View("History", new HistoryViewModel(subject, relations, documentsInFamily, accountListings));
		}
    }
}
