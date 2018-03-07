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
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

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
        
        readonly DatabaseConfiguration databaseConfiguration;
		readonly InputConfiguration inputConfiguration;

		public DocumentController(IOptions<DatabaseConfiguration> _databaseConfiguration, IOptions<InputConfiguration> _inputConfiguration)
        {
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

            submissionModel.Title = submissionModel.Title ?? string.Empty;
            submissionModel.Body = submissionModel.Body ?? string.Empty;

			if(submissionModel.Title.Length > inputConfiguration.TitleLengthLimit) {
				return StatusCode(413);
			}

			if(submissionModel.Body.Length > inputConfiguration.BodyLengthLimit) {
				return StatusCode(413);
			}

            byte[] submissionId;

            using (var md5Encoder = MD5.Create())
            {
                md5Encoder.Initialize();
                var whole = submissionModel.Title + submissionModel.Body;
                var buffer = Encoding.UTF8.GetBytes(whole);
                submissionId = md5Encoder.ComputeHash(buffer);
            }

            var antecedantIds = ImmutableArray.CreateRange(submissionModel.AntecedentIdBase64.Select(s => WebEncoders.Base64UrlDecode(s)));

            Guid authorId;

            {
                var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);

                authorId = Guid.Parse(nameIdentifierClaim.Value);
            }

            var submissionIdAsGuid = new Guid(submissionId);

            try
            {
                using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "INSERT INTO document(id,title,authorId) VALUES(@id,@title,@authorId);";
                        cmd.Parameters.AddWithValue("@id", submissionIdAsGuid);
                        cmd.Parameters.AddWithValue("@title", submissionModel.Title);
                        cmd.Parameters.AddWithValue("@authorId", authorId);
                        await cmd.ExecuteNonQueryAsync();
					}

					using (var cmd = new NpgsqlCommand())
					{
						cmd.Connection = conn;
						cmd.CommandText = "INSERT INTO documentBody(id,body) VALUES(@id,@body);";
						cmd.Parameters.AddWithValue("@id", submissionIdAsGuid);
						cmd.Parameters.AddWithValue("@body", submissionModel.Body);
						await cmd.ExecuteNonQueryAsync();
					}

                    foreach (var antecedantIdBoxedAsGuid in antecedantIds
                             .Select(bytes => new Guid(bytes))
                             .Where(antecedantId => antecedantId != submissionIdAsGuid))
                    {
                        using (var cmd = new NpgsqlCommand())
                        {
                            cmd.Connection = conn;
                            cmd.CommandText = "INSERT INTO relation(antecedentId,descendantId) VALUES(@antecedentId,@descendantId);";
                            cmd.Parameters.AddWithValue("@antecedentId", antecedantIdBoxedAsGuid);
                            cmd.Parameters.AddWithValue("@descendantId", submissionIdAsGuid);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (PostgresException ex)
            {
                if(ex.SqlState == "23505") {
                    //As per the PostgreSQL manual, this is the error code for a uniqueness violation. It
                    //means this document is already in there (the id is duplicated). If that is the case
                    //than fall through; the redirect which happens at the end of this method body will work
                    //the same if the object is already in there.
                }
                else {
                    throw ex;
                }
            }

            var submissionIdAsBase64 = WebEncoders.Base64UrlEncode(submissionId);

            return RedirectToAction(nameof(GetDocument), new { id = submissionIdAsBase64 });
        }

        [Authorize]
		[HttpGet("{id}/edit")]
		public async Task<IActionResult> Edit(string id, string secondId)
        {
			DocumentViewModel viewModel;
			if(id == "new") {
				viewModel = new DocumentViewModel(NewDocumentBody, string.Empty);
			} else {
				var idInBinary = WebEncoders.Base64UrlDecode(id);
				var idInMD5Sum = new MD5Sum(idInBinary);
				using (var connection = new NpgsqlConnection(databaseConfiguration.ConnectionString))
				{
					await connection.OpenAsync();
					viewModel = await GetDocumentViewModelAsync(connection, idInMD5Sum);
					var relations = await GetRelation(connection, idInMD5Sum);
					var antecedents = ImmutableHashSet.CreateRange(relations.Select(r => r.AntecedentId));
					var tips = relations.Select(r => r.DescendantId).Where(d => !antecedents.Contains(d)).Where(d => !d.Equals(idInMD5Sum));
					var documentListings = await GetDocumentListingsAsync(connection, tips);
					var accountListings = await GetAccountListingsAsync(connection, documentListings.Select(d => d.AuthorId));
					documentListings = EnrichDocumentListings(documentListings, accountListings);
					viewModel = viewModel.WithComparables(documentListings.Where(dl => !dl.Id.Equals(idInMD5Sum)));

					if(secondId != null) {
						var secondIdInBinary = WebEncoders.Base64UrlDecode(secondId);
						var secondIdInMD5Sum = new MD5Sum(secondIdInBinary);
						var secondViewModel = await GetDocumentViewModelAsync(connection, secondIdInMD5Sum);

						var diffBuilder = new InlineDiffBuilder(new Differ());
						var diff = diffBuilder.BuildDiffModel(viewModel.Body, secondViewModel.Body);

						var bodyBuilder = new StringBuilder();

						foreach (var line in diff.Lines)
						{
							switch (line.Type)
							{
								case ChangeType.Inserted:
									bodyBuilder.Append("+ ");
									break;
								case ChangeType.Deleted:
									bodyBuilder.Append("- ");
									break;
							}
							bodyBuilder.AppendLine(line.Text);
						}

						viewModel = new DocumentViewModel(bodyBuilder.ToString(), viewModel.Title, viewModel.Sources.Concat(secondViewModel.Sources), viewModel.Comparables);
					}
				}
			}
			return View("Edit", viewModel);
		}

		[HttpGet("{id}")]
		public async Task<IActionResult> GetDocument(string id)
		{
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idInMD5Sum = new MD5Sum(idInBinary);
			DocumentViewModel viewModel;
			using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await conn.OpenAsync();
				viewModel = await GetDocumentViewModelAsync(conn, idInMD5Sum);
			}
			return View("Document", viewModel);
		}

		async Task<DocumentViewModel> GetDocumentViewModelAsync(NpgsqlConnection connection, MD5Sum id)
		{
			var idBoxedInGuidForDatabase = id.ToGuid();

			DocumentListingViewModel sourceDescription;
			string title;

			using (var cmd = new NpgsqlCommand())
			{
				cmd.Connection = connection;
				cmd.CommandText = "SELECT title,authorId,timestamp FROM document WHERE id=@id;";
				cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

				using (var reader = await cmd.ExecuteReaderAsync())
				{
					if (await reader.ReadAsync())
					{
						title = reader.GetString(0);
						var authorId = reader.GetGuid(1);
						var timestamp = reader.GetDateTime(2);
						sourceDescription = new DocumentListingViewModel(id, title, authorId, timestamp);
					}
					else
					{
						throw new FileNotFoundException();
					}
				}
			}

			string body;

			using (var cmd = new NpgsqlCommand())
			{
				cmd.Connection = connection;
				cmd.CommandText = "SELECT body FROM documentBody WHERE id=@id;";
				cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

				using (var reader = await cmd.ExecuteReaderAsync())
				{
					if (await reader.ReadAsync())
					{
						body = reader.GetString(0);
					}
					else
					{
						throw new FileNotFoundException();
					}
				}
			}

			using (var cmd = new NpgsqlCommand())
			{
				cmd.Connection = connection;
				cmd.CommandText = "SELECT displayName FROM account WHERE id=@id;";
				cmd.Parameters.AddWithValue("@id", sourceDescription.AuthorId);

				using (var reader = await cmd.ExecuteReaderAsync())
				{
					if (await reader.ReadAsync())
					{
						var authorDisplayName = reader.GetString(0);
						sourceDescription = sourceDescription.WithAuthorDisplayName(authorDisplayName);
					}
				}
			}

			return new DocumentViewModel(body, title, new[] { sourceDescription }, Enumerable.Empty<DocumentListingViewModel>());
		}

		async Task<IEnumerable<Relation>> GetRelation(NpgsqlConnection connection, MD5Sum documentId) {
			var relationsBuilder = ImmutableHashSet.CreateBuilder<Relation>();
			var closedSet = ImmutableHashSet.CreateBuilder<Guid>();
			var openSet = ImmutableHashSet.CreateBuilder<Guid>();

			openSet.Add(documentId.ToGuid());

			while (openSet.Count > 0)
			{
				var openSetAsArray = openSet.ToArray();
				closedSet.UnionWith(openSetAsArray);
				openSet.Clear();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT antecedentId, descendantId FROM relation WHERE ARRAY[antecedentId, descendantId] && @openSet;";
					cmd.Parameters.AddWithValue("@openSet", openSetAsArray);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						while (await reader.ReadAsync())
						{
							var alfa = reader.GetGuid(0);
							var bravo = reader.GetGuid(1);
							openSet.Add(alfa);
							openSet.Add(bravo);
							relationsBuilder.Add(new Relation(new MD5Sum(alfa.ToByteArray()), new MD5Sum(bravo.ToByteArray())));
						}
					}
				}

				openSet.ExceptWith(closedSet);
			}

			return relationsBuilder;
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
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idInMD5Sum = new MD5Sum(idInBinary);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);

			IEnumerable<DocumentListingViewModel> documentsInFamily;
			IEnumerable<AccountListingViewModel> accountListings;
			IEnumerable<Relation> relations;

			using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await conn.OpenAsync();

				relations = await GetRelation(conn, idInMD5Sum);

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
