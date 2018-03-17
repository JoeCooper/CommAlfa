using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Server.Models;

namespace Server.Services
{
	public class PostgreSQLDatabaseService: IDatabaseService
	{
		readonly string connectionString;
		
		public PostgreSQLDatabaseService(string connectionString)
		{
			this.connectionString = connectionString;
		}

		public async Task<MD5Sum> AddDocumentAsync(Guid authorId, string body, string title, IEnumerable<MD5Sum> antecedantIds)
		{
			var submissionId = MD5Sum.Encode(title + body);

			var submissionIdAsGuid = submissionId.ToGuid();

            try
			{
				using (var conn = new NpgsqlConnection(connectionString))
				{
					await conn.OpenAsync();

					using (var cmd = new NpgsqlCommand())
					{
						cmd.Connection = conn;
						cmd.CommandText = "INSERT INTO document(id,title,authorId) VALUES(@id,@title,@authorId);";
						cmd.Parameters.AddWithValue("@id", submissionIdAsGuid);
						cmd.Parameters.AddWithValue("@title", title);
						cmd.Parameters.AddWithValue("@authorId", authorId);
						await cmd.ExecuteNonQueryAsync();
					}

					using (var cmd = new NpgsqlCommand())
					{
						cmd.Connection = conn;
						cmd.CommandText = "INSERT INTO documentBody(id,body) VALUES(@id,@body);";
						cmd.Parameters.AddWithValue("@id", submissionIdAsGuid);
						cmd.Parameters.AddWithValue("@body", body);
						await cmd.ExecuteNonQueryAsync();
					}

					foreach (var antecedantIdBoxedAsGuid in antecedantIds
					         .Select(sum => sum.ToGuid())
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
				if (ex.SqlState == "23505")
				{
					//As per the PostgreSQL manual, this is the error code for a uniqueness violation. It
					//means this document is already in there (the id is duplicated). If that is the case
					//than fall through; the redirect which happens at the end of this method body will work
					//the same if the object is already in there.
				}
				else
				{
					throw ex;
				}
			}
			return submissionId;
		}

		public async Task<IEnumerable<MD5Sum>> GetDescendantIds(MD5Sum documentId)
		{
			var builder = ImmutableArray.CreateBuilder<MD5Sum>();

			using (var connection = new NpgsqlConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT descendantId FROM relation WHERE antecedentId=@documentId;";
					cmd.Parameters.AddWithValue("@documentId", documentId.ToGuid());

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						while (await reader.ReadAsync())
						{
							var figure = reader.GetGuid(0);
							builder.Add(new MD5Sum(figure.ToByteArray()));
						}
					}
				}
			}

			return builder;
		}

		public async Task<IEnumerable<MD5Sum>> GetDocumentsForAccountAsync(Guid id)
		{
			var builder = ImmutableArray.CreateBuilder<MD5Sum>();

			using (var connection = new NpgsqlConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT id FROM document WHERE authorId=@authorId;";
					cmd.Parameters.AddWithValue("@authorId", id);

					using(var reader = await cmd.ExecuteReaderAsync())
					{
						while(await reader.ReadAsync()) {
							var figure = reader.GetGuid(0);
							builder.Add(new MD5Sum(figure.ToByteArray()));
						}
					}
				}
			}

			return builder;
		}

		public async Task<Account> GetAccountAsync(Guid id)
		{
			using (var connection = new NpgsqlConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT displayName, email FROM account WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", id);

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return new Account(id, reader.GetString(0), reader.GetString(1));
						}
						throw new FileNotFoundException();
					}
				}
			}
		}

		public async Task<DocumentMetadata> GetDocumentMetadataAsync(MD5Sum id)
		{
			using (var connection = new NpgsqlConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT title,authorId,timestamp FROM document WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", id.ToGuid());

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return new DocumentMetadata(id, reader.GetString(0), reader.GetGuid(1), reader.GetDateTime(2));
						}
						throw new FileNotFoundException();
					}
				}
			}
		}

		public async Task<string> GetDocumentBodyAsync(MD5Sum id, bool ignoreBlock = false)
		{
			using (var connection = new NpgsqlConnection(connectionString))
			{
				await connection.OpenAsync();

				if(!ignoreBlock) {
					using (var cmd = new NpgsqlCommand())
					{
						cmd.Connection = connection;
						cmd.CommandText = "SELECT isVoluntary FROM documentBlock WHERE id=@id;";
						cmd.Parameters.AddWithValue("@id", id.ToGuid());

						using (var reader = await cmd.ExecuteReaderAsync())
						{
							if (await reader.ReadAsync())
							{
								throw new DocumentBlockedException(reader.GetBoolean(0));
							}
						}
					}
				}

				using (var cmd = new NpgsqlCommand())
				{
					cmd.Connection = connection;
					cmd.CommandText = "SELECT body FROM documentBody WHERE id=@id;";
					cmd.Parameters.AddWithValue("@id", id.ToGuid());

					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							return reader.GetString(0);
						}
						throw new FileNotFoundException();
					}
				}
			}
		}

		public async Task<IEnumerable<Relation>> GetFamilyAsync(MD5Sum familyMemberId)
		{
			var idBoxedInGuidForDatabase = familyMemberId.ToGuid();
			var relationsBuilder = ImmutableHashSet.CreateBuilder<Relation>();
			var closedSet = ImmutableHashSet.CreateBuilder<Guid>();
			var openSet = ImmutableHashSet.CreateBuilder<Guid>();

			openSet.Add(idBoxedInGuidForDatabase);

			using (var connection = new NpgsqlConnection(connectionString))
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

			return relationsBuilder;
		}
	}
}
