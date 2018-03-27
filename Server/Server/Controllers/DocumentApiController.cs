using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
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
	[Route("api/documents")]
	public class DocumentApiController: Controller
	{
		readonly IDatabaseService databaseService;
		readonly InputConfiguration inputConfiguration;
		readonly ILogger<DocumentApiController> logger;

		public DocumentApiController(IDatabaseService databaseService, IOptions<InputConfiguration> _inputConfiguration, ILogger<DocumentApiController> logger)
		{
			this.databaseService = databaseService;
			inputConfiguration = _inputConfiguration.Value;
			this.logger = logger;
		}

		[HttpGet("{id}/family")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetFamily(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var docId = new MD5Sum(idInBinary);
			var relations = await databaseService.GetFamilyAsync(docId);
			return Ok(relations);
		}

		[HttpGet("{id}/descendants")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public async Task<IActionResult> GetDescendants(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var docId = new MD5Sum(idInBinary);
			var descendants = await databaseService.GetDescendantIds(docId);
			return Ok(descendants);
		}

		[HttpGet("{id}/metadata")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Immutable)]
		public async Task<IActionResult> GetDocumentMetadata(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idBoxed = new MD5Sum(idInBinary);
			var metadata = await databaseService.GetDocumentMetadataAsync(idBoxed);
			return Ok(metadata);
		}

		[HttpGet("{id}")]
		[ResponseCache(CacheProfileName = CacheProfileNames.SemiImmutable)]
		public async Task<IActionResult> GetDocument(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var docId = new MD5Sum(idInBinary);
			return Content(await databaseService.GetDocumentBodyAsync(docId), "text/markdown");
		}
	}
}
