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

        public DocumentController(IOptions<DatabaseConfiguration> _databaseConfiguration)
        {
            databaseConfiguration = _databaseConfiguration.Value;
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
                        cmd.CommandText = "INSERT INTO document(id,title,body,authorId) VALUES(@id,@title,@body,@authorId);";
                        cmd.Parameters.AddWithValue("@id", submissionIdAsGuid);
                        cmd.Parameters.AddWithValue("@title", submissionModel.Title);
                        cmd.Parameters.AddWithValue("@body", submissionModel.Body);
                        cmd.Parameters.AddWithValue("@authorId", authorId);
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

        [HttpGet("new/edit")] [Authorize]
        public IActionResult New()
        {
            var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
            var authorId = Guid.Parse(nameIdentifierClaim.Value);
            var authorDisplayName = User.Identity.Name;
            return View("Edit", new DocumentViewModel(Enumerable.Empty<byte[]>(), NewDocumentBody, string.Empty, authorId, DateTime.Now, authorDisplayName));
        }

        [Authorize]
        [HttpGet("{id}/edit")]
        public async Task<IActionResult> Edit(string id)
        {
            return await GetDocument(id, true);
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(string id)
        {
            var idInBinary = WebEncoders.Base64UrlDecode(id);
            var idBoxedInGuidForDatabase = new Guid(idInBinary);

            IEnumerable<DocumentListingViewModel> documentsInFamily;
            IEnumerable<Relation> relations;
            IImmutableDictionary<Guid, string> authorNames;
            IEnumerable<AccountListingViewModel> contributors;

            using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
            {
                await conn.OpenAsync();

                var relationsBuilder = ImmutableHashSet.CreateBuilder<Relation>();
                var closedSet = ImmutableHashSet.CreateBuilder<Guid>();
                var openSet = ImmutableHashSet.CreateBuilder<Guid>();

                openSet.Add(idBoxedInGuidForDatabase);

                while(openSet.Count > 0)
                {
                    closedSet.UnionWith(openSet);
                    var openSetAsArray = openSet.ToArray();
                    openSet.Clear();

                    using(var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT antecedentId, descendantId FROM relation WHERE ARRAY[antecedentId, descendantId] && @openSet;";
                        cmd.Parameters.AddWithValue("@openSet", openSetAsArray);

                        using(var reader = await cmd.ExecuteReaderAsync())
                        {
                            while(await reader.ReadAsync())
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

                relations = relationsBuilder;

                using (var cmd = new NpgsqlCommand())
                {
                    var closedSetAsArray = closedSet.ToArray();
                    
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT id, title, authorId, timestamp FROM document WHERE ARRAY[id] && @closedSet;";
                    cmd.Parameters.AddWithValue("@closedSet", closedSetAsArray);

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

                        documentsInFamily = documentsBuilder;
                    }
                }

                using (var cmd = new NpgsqlCommand())
                {
                    var contributorsBuilder = ImmutableArray.CreateBuilder<AccountListingViewModel>();
                    var authorNamesBuilder = ImmutableDictionary.CreateBuilder<Guid, string>();
                    var authorIdsAsArray = ImmutableHashSet.CreateRange(documentsInFamily.Select(d => d.AuthorId)).ToArray();

                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT id, displayName FROM account WHERE ARRAY[id] && @authorIds;";
                    cmd.Parameters.AddWithValue("@authorIds", authorIdsAsArray);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            authorNamesBuilder[reader.GetGuid(0)] = reader.GetString(1);
                        }
                    }

                    authorNames = authorNamesBuilder.ToImmutable();
                }
            }

            documentsInFamily = documentsInFamily.Select(d => d.WithAuthorDisplayName(authorNames[d.AuthorId]));

            var subject = documentsInFamily.Single(d => d.Id.Equals(idInBinary));

            return View("History", new HistoryViewModel(subject, relations, documentsInFamily));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(string id, bool edit)
        {
            using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
            {
                await conn.OpenAsync();

                var idInBinary = WebEncoders.Base64UrlDecode(id);
                var idBoxedInGuidForDatabase = new Guid(idInBinary);

                DocumentViewModel viewModel;

                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT body,title,authorId,timestamp FROM document WHERE id=@id;";
                    cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            viewModel = new DocumentViewModel(ImmutableArray.Create<byte[]>(idInBinary), reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetDateTime(3));
                        }
                        else
                        {
                            viewModel = null;
                        }
                    }
                }

                if (viewModel != null)
                {
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT displayName FROM account WHERE id=@id;";
                        cmd.Parameters.AddWithValue("@id", viewModel.AuthorId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                viewModel = viewModel.WithAuthorDisplayName(reader.GetString(0));
                            }
                        }
                    }

                    return View(edit ? "Edit" : "Document", viewModel);
                }

                return NotFound();
            }
        }
    }
}
