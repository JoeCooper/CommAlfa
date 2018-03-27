using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Server.Models;
using Server.Services;
using Server.Utilities;

namespace Server.Controllers
{
	[Route("api/accounts")]
	public class AccountApiController: Controller
	{
		readonly IDatabaseService databaseService;
		readonly ILogger<AccountApiController> logger;

		public AccountApiController(IDatabaseService databaseService, ILogger<AccountApiController> logger)
		{
			this.databaseService = databaseService;
			this.logger = logger;
		}

		[HttpGet("{id}/metadata")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetAccountMetadata(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Account id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idBoxedInGuidForDatabase = new Guid(idInBinary);
			var account = await databaseService.GetAccountAsync(idBoxedInGuidForDatabase);
			var responseBody = new AccountMetadata(account.Id, account.DisplayName, account.Email.ToGravatarHash());
			return Ok(responseBody);
		}

		[HttpGet("{id}/documents")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetAccountDocuments(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Account id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var guid = new Guid(WebEncoders.Base64UrlDecode(id));
			var documentKeys = await databaseService.GetDocumentsForAccountAsync(guid);
			return Ok(documentKeys);
		}
	}
}
