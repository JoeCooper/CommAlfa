using System;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Server.Models;
using Server.UnitTests.Fodder;
using Xunit;

namespace Server.UnitTests
{
    static class Extension
    {
        public static string Enquote(this string s)
        {
            return string.Format("\"{0}\"", s);
        }
    }

    public class JsonEncodeTest
    {
        [Theory(DisplayName = "Encoding account metadata")]
        [ClassData(typeof(AccountMetadataData))]
        public void TestAccountMetadataEncodesId(AccountMetadata metadata) {
            var encodedId = WebEncoders.Base64UrlEncode(metadata.Id.ToByteArray());
            var encodedMetadata = JsonConvert.SerializeObject(metadata);
            Assert.Contains(encodedId.Enquote(), encodedMetadata);
            Assert.Contains(metadata.DisplayName.Enquote(), encodedMetadata);
            Assert.Contains(metadata.GravatarHash.Enquote(), encodedMetadata);
        }
    }
}
