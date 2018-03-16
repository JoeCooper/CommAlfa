using System.Reflection;
using Xunit;
using Server.Utilities;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System;

namespace Server.UnitTests
{
	public class GravatarTest
	{
		[Theory(DisplayName = "Does Gravatar hash result in a valid hash")]
		[InlineData("frapaquad@gmail.com")]
		public void TestIfValidHash(string emailAddress)
		{
			var validCharacters = ImmutableHashSet.CreateRange("0987654321abcdef");
			var gravatarHash = emailAddress.ToGravatarHash();
			Assert.Equal(32, gravatarHash.Length);
			for (var i = 0; i < gravatarHash.Length; i++) {
				Assert.Contains(gravatarHash[i], validCharacters);
			}
		}

		[Fact(DisplayName = "Does Gravatar Root end with a slash")]
		public void TestRootEndsWithSlash()
		{
			Assert.EndsWith("/", Gravatar.Root);
		}

		[Fact(DisplayName = "Is Gravatar Root a valid URL")]
		public void TestRootIsURL()
		{
			Uri uri;
			Assert.True(Uri.TryCreate(Gravatar.Root, UriKind.Absolute, out uri));
		}

		[Theory(DisplayName = "Does Gravatar fetch yield a PNG")]
		[InlineData("frapaquad@gmail.com")]
		public async Task TestFetchVague(string emailAddress)
		{
			var hash = emailAddress.ToGravatarHash();
			var url = Gravatar.Root + hash;
			using (var httpClient = new HttpClient())
			using(var response = await httpClient.GetAsync(url))
			{
				Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
				Assert.Contains("image/png", response.Content.Headers.GetValues("Content-Type"));
			}
		}

		[Theory(DisplayName = "Does Gravatar fetch yield correct image")]
		[InlineData("frapaquad@gmail.com", "Server.UnitTests.Fodder.gravatar.png")]
		public async Task TestFetchSpecific(string emailAddress, string resourceNameForExpectedPayload)
		{
			var hash = emailAddress.ToGravatarHash();
			var url = Gravatar.Root + hash;
            var assembly = Assembly.GetExecutingAssembly();
			byte[] localExample;
			const int bufferLength = 1024;
			using (var localStream = assembly.GetManifestResourceStream(resourceNameForExpectedPayload))
			{
				var builder = ImmutableArray.CreateBuilder<byte>();
				var buffer = new byte[bufferLength];
				int bytesRead;
				do
				{
					bytesRead = await localStream.ReadAsync(buffer, 0, bufferLength);
					builder.AddRange(buffer, bytesRead);
				} while (bytesRead > 0);
				localExample = builder.ToArray();
			}
			byte[] remoteExample;
			using (var httpClient = new HttpClient()){
				remoteExample = await httpClient.GetByteArrayAsync(url);
			}
			Assert.Equal(localExample.Length, remoteExample.Length);
			for (var i = 0; i < localExample.Length; i++)
			{
				Assert.Equal(localExample[i], remoteExample[i]);
			}
		}
	}
}
