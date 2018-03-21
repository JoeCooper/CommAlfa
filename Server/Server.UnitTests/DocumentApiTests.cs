using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Controllers;
using Server.Models;
using Server.UnitTests.Fodder;
using Server.UnitTests.Services;
using Xunit;

namespace Server.UnitTests
{
	public class DocumentApiTests
	{
		[Fact(DisplayName = "Fetch document")]
		public async Task TestFetchDocument()
		{
			var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
			var factory = serviceProvider.GetService<ILoggerFactory>();
			var logger = factory.CreateLogger<DocumentApiController>();
			var testBody = "The quick brown fox jumps over the lazy dog.";
			var falseDatabaseService = new FalseDatabaseService();
			var draftAccount = new Account(Guid.NewGuid(), "Alfa", "alfa@bravo.com", Array.Empty<byte>());
			await falseDatabaseService.SaveAccountAsync(draftAccount, true);
			var documentId = await falseDatabaseService.AddDocumentAsync(draftAccount.Id, testBody, string.Empty, Enumerable.Empty<MD5Sum>());
			var options = new Options<InputConfiguration>(new InputConfiguration());
			var documentApiController = new DocumentApiController(falseDatabaseService, options, logger);
			var result = (ContentResult)await documentApiController.GetDocument(documentId.ToString());
			Assert.Equal(result.Content, testBody);
			Assert.Equal(result.ContentType, "text/markdown");
		}
	}
}
