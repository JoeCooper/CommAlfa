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
using System.IO;
using Server.UnitTests.Fodder;

namespace Server.UnitTests
{
    public class ApiTests
    {
        [Fact(DisplayName = "Fetch account")]
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

        [Fact(DisplayName = "Fetch non-existent account")]
        public async Task TestFetchNonExistentAccount()
        {
            var falseDatabaseService = new FalseDatabaseService();
            var draftAccount = new Account(Guid.NewGuid(), "Alfa", "alfa@bravo.com", Array.Empty<byte>());
            await falseDatabaseService.SaveAccountAsync(draftAccount, true);
            var accountApiController = new AccountApiController(falseDatabaseService);
            var idWhichIsNotRepresentedInTheDatabase = WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());
            try
            {
                var result = await accountApiController.GetAccountMetadata(idWhichIsNotRepresentedInTheDatabase);
                Assert.True(false);
            }
            catch (Exception ex)
            {
                Assert.True(ex is FileNotFoundException);
            }
        }

        [Theory(DisplayName = "Fetch account with bad ID")]
        [ClassData(typeof(BadIds))]
        public async Task TestFetchAccountFastFail(string id)
        {
            var accountApiController = new AccountApiController(new FalseDatabaseService());
            var result = await accountApiController.GetAccountMetadata(id);
            Assert.True(result is BadRequestResult);
        }

        [Fact(DisplayName = "Fetch documents for account")]
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

        [Fact(DisplayName = "Fetch documents for non-existent account")]
        public async Task TestFetchDocumentsForNonExistentAccount()
        {
            var falseDatabaseService = new FalseDatabaseService();
            var draftAccount = new Account(Guid.NewGuid(), "Alfa", "alfa@bravo.com", Array.Empty<byte>());
            await falseDatabaseService.SaveAccountAsync(draftAccount, true);
            var accountApiController = new AccountApiController(falseDatabaseService);
            var idWhichIsNotRepresentedInTheDatabase = WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());
            var result = (OkObjectResult) await accountApiController.GetAccountDocuments(idWhichIsNotRepresentedInTheDatabase);
            var resultBody = (IEnumerable<MD5Sum>) result.Value;
            Assert.Empty(resultBody);
        }

        [Theory(DisplayName = "Fetch documents for account with bad id")]
        [ClassData(typeof(BadIds))]
        public async Task TestFetchDocumentsFastFail(string id)
        {
            var accountApiController = new AccountApiController(new FalseDatabaseService());
            var result = await accountApiController.GetAccountMetadata(id);
            Assert.True(result is BadRequestResult);
        }
    }
}
