using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Npgsql;
using Server.Models;
using Server.Utilities;

namespace Server.Controllers
{
	[Route("api/documents")]
	public class DocumentApiController: Controller
	{
		readonly DatabaseConfiguration databaseConfiguration;
		readonly InputConfiguration inputConfiguration;

		public DocumentApiController(IOptions<DatabaseConfiguration> _databaseConfiguration, IOptions<InputConfiguration> _inputConfiguration)
		{
			databaseConfiguration = _databaseConfiguration.Value;
			inputConfiguration = _inputConfiguration.Value;
		}

		[HttpGet("{id}/family")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetFamily(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();

			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);
			var relationsBuilder = ImmutableHashSet.CreateBuilder<Relation>();
			var closedSet = ImmutableHashSet.CreateBuilder<Guid>();
			var openSet = ImmutableHashSet.CreateBuilder<Guid>();

			openSet.Add(idBoxedInGuidForDatabase);

			using (var connection = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await connection.OpenAsync();
				while (openSet.Count > 0)
				{
					var openSetAsArray = openSet.ToArray();
					closedSet.UnionWith(openSet);
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
			}

			return Ok(relationsBuilder);
		}

		[HttpGet("{id}/metadata")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Immutable)]
		public async Task<IActionResult> GetDocumentMetadata(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();

			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);

			using (var connection = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT title,authorId,timestamp FROM document WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return Ok(new DocumentMetadata(new MD5Sum(idInBinary), reader.GetString(0), reader.GetGuid(1), reader.GetDateTime(2)));
						}
						return NotFound();
					}
				}
			}
		}

		[HttpGet("{id}")]
		[ResponseCache(CacheProfileName = CacheProfileNames.SemiImmutable)]
		public async Task<IActionResult> GetDocument(string id)
		{
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);

			using (var connection = new NpgsqlConnection(databaseConfiguration.ConnectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT isVoluntary FROM documentBlock WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							var isVoluntary = reader.GetBoolean(0);
							if (isVoluntary)
							{
								return StatusCode(410);
							}
							return StatusCode(451);
						}
					}
				}

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT body FROM documentBody WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return Content(reader.GetString(0), "text/markdown");
						}
						return NotFound();
					}
				}
			}
		}
	}
}
