using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Npgsql;
using Server.Models;
using Server.Utilities;

namespace Server.Controllers
{
	[Route("api/accounts")]
	public class AccountApiController: Controller
	{
		readonly DatabaseConfiguration databaseConfiguration;

		public AccountApiController(IOptions<DatabaseConfiguration> _databaseConfiguration)
		{
			databaseConfiguration = _databaseConfiguration.Value;
		}

		[HttpGet("{id}/metadata")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetAccountMetadata(string id)
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
					cmd.CommandText = "SELECT displayName,email FROM account WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", idBoxedInGuidForDatabase);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return Ok(new AccountMetadata(idBoxedInGuidForDatabase, reader.GetString(0), reader.GetString(1).ToGravatarHash()));
						}
						return NotFound();
					}
				}
			}
		}
	}
}
