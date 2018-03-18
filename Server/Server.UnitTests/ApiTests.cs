using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Server.Controllers;
using Server.Models;
using Server.UnitTests.Services;
using Xunit;
using Server.Utilities;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Server.UnitTests
{
    public class ApiTests
    {
        [Fact(DisplayName = "Fetch account via API")]
        public async Task TestFetchAccount()
        {
            var falseDatabaseService = new FalseDatabaseService();
            var draftAccount = new Account(Guid.NewGuid(), "Alfa", "alfa@bravo.com", Array.Empty<byte>());
            await falseDatabaseService.SaveAccountAsync(draftAccount, true);
            var accountApiController = new AccountApiController(falseDatabaseService);
            var encodedId = WebEncoders.Base64UrlEncode(draftAccount.Id.ToByteArray());
            var result = (OkObjectResult) await accountApiController.GetAccountMetadata(encodedId);
            var resultBody = (AccountMetadata)result.Value;
            Assert.Equal(draftAccount.DisplayName, resultBody.DisplayName);
            Assert.Equal(draftAccount.Email.ToGravatarHash(), resultBody.GravatarHash);
            Assert.Equal(draftAccount.Id, resultBody.Id);
        }

        [Fact(DisplayName = "Fetch documents for account via API")]
        public async Task TestFetchDocuments()
        {
            var falseDatabaseService = new FalseDatabaseService();
            var draftAccount = new Account(Guid.NewGuid(), "Alfa", "alfa@bravo.com", Array.Empty<byte>());
            var decoyAccount = new Account(Guid.NewGuid(), "Bravo", "bravo@bravo.com", Array.Empty<byte>());
            await falseDatabaseService.SaveAccountAsync(draftAccount, true);
            await falseDatabaseService.SaveAccountAsync(decoyAccount, true);
            var draftDocumentKeys = new[] {
                await falseDatabaseService.AddDocumentAsync(draftAccount.Id, "alfa", string.Empty, Enumerable.Empty<MD5Sum>()),
                await falseDatabaseService.AddDocumentAsync(draftAccount.Id, "bravo", string.Empty, Enumerable.Empty<MD5Sum>())
            };
            var decoyDocumentKeys = new[] {
                await falseDatabaseService.AddDocumentAsync(decoyAccount.Id, "charlie", string.Empty, Enumerable.Empty<MD5Sum>()),
                await falseDatabaseService.AddDocumentAsync(decoyAccount.Id, "delta", string.Empty, Enumerable.Empty<MD5Sum>())
            };
            var accountApiController = new AccountApiController(falseDatabaseService);
            var encodedId = WebEncoders.Base64UrlEncode(draftAccount.Id.ToByteArray());
            var result = (OkObjectResult)await accountApiController.GetAccountDocuments(encodedId);
            var resultBody = (IEnumerable<MD5Sum>)result.Value;
            Assert.Equal(resultBody.Count(), draftDocumentKeys.Count());
            Assert.True(resultBody.SequenceEqual(draftDocumentKeys));
        }
    }
}
