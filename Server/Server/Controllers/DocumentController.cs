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
using Microsoft.Extensions.Logging;

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
		readonly InputConfiguration inputConfiguration;
		readonly ILogger<DocumentController> logger;

		public DocumentController(IDatabaseService databaseService, IOptions<InputConfiguration> _inputConfiguration, ILogger<DocumentController> logger)
		{
			this.databaseService = databaseService;
			inputConfiguration = _inputConfiguration.Value;
			this.logger = logger;
        }

        [HttpPost("{id}/edit")]
        [Authorize]
        public async Task<IActionResult> Save(string id, DocumentSubmissionModel submissionModel)
		{
            submissionModel.AntecedentIdBase64 = submissionModel.AntecedentIdBase64 ?? Enumerable.Empty<string>();
            
			if(submissionModel.AntecedentIdBase64.Any() && !submissionModel.AntecedentIdBase64.Contains(id)) {
				logger.LogWarning("Document rejected; The given id is a member of the given antecedents; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}

            if (submissionModel.AntecedentIdBase64.Count() > 2)
			{
				logger.LogWarning("Document rejected; More than two antecedents; Origin: {0}", HttpContext.GetRemoteAddress());
                return BadRequest();
            }

			if(submissionModel.AntecedentIdBase64.Any(_id => _id.FalsifyAsIdentifier()))
			{
				logger.LogWarning("Document rejected; Invalid antecents; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}

            submissionModel.Title = submissionModel.Title ?? string.Empty;
            submissionModel.Body = submissionModel.Body ?? string.Empty;

            submissionModel.Title = submissionModel.Title.Trim();
            submissionModel.Body = submissionModel.Body.Trim();

			if(submissionModel.Title.Length > inputConfiguration.TitleLengthLimit) {
				logger.LogWarning("Document rejected; Title too long; Origin: {0}", HttpContext.GetRemoteAddress());
				return StatusCode(413);
			}

			if(submissionModel.Body.Length > inputConfiguration.BodyLengthLimit) {
				logger.LogWarning("Document rejected; Body too long; Origin: {0}", HttpContext.GetRemoteAddress());
				return StatusCode(413);
			}

			var antecedantIds = ImmutableArray.CreateRange(submissionModel.AntecedentIdBase64.Select(s => new MD5Sum(WebEncoders.Base64UrlDecode(s))));

            Guid authorId;

            {
                var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);

                authorId = Guid.Parse(nameIdentifierClaim.Value);
            }

			var submissionId = await databaseService.AddDocumentAsync(authorId, submissionModel.Body, submissionModel.Title, antecedantIds);

			logger.LogInformation("Document saved; Key: {1}; Origin: {0}", HttpContext.GetRemoteAddress(), submissionId.ToString());

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
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			return View("Edit", new IdentifierViewModel(id));
		}

        [HttpGet("{id}")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public IActionResult GetDocument(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			return View("Document", new IdentifierViewModel(id));
		}

        [HttpGet("{id}/history")]
		[ResponseCache(CacheProfileName = CacheProfileNames.Default)]
		public IActionResult GetHistory(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			return View("History", new IdentifierViewModel(id));
		}

		[HttpGet("{id}/indexable")]
		[ResponseCache(CacheProfileName = CacheProfileNames.SemiImmutable)]
		public async Task<IActionResult> GetDocumentForIndexing(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Document id rejected; Origin: {0}", HttpContext.GetRemoteAddress());
				return BadRequest();
			}
			var idInBinary = WebEncoders.Base64UrlDecode(id);
			var idMD5 = new MD5Sum(idInBinary);
			var metadata = await databaseService.GetDocumentMetadataAsync(idMD5);
			var body = await databaseService.GetDocumentBodyAsync(idMD5);
			return View("DocumentForIndexing", new DocumentViewModel(body, metadata.Title));
		}
    }
}
