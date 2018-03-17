using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Server.Controllers;
using Server.Models;
using Server.UnitTests.Services;
using Xunit;
using Server.Utilities;
using System.Threading.Tasks;

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
    }
}
