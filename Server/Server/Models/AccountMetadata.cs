using System;
using Newtonsoft.Json;
using Server.Utilities;

namespace Server.Models
{
	public class AccountMetadata
	{
		public AccountMetadata(Guid id, string displayName, string gravatarHash)
		{
			Id = id;
			GravatarHash = gravatarHash;
			DisplayName = displayName;
		}

		[JsonProperty("id")]
		[JsonConverter(typeof(IdentifierConverter))]
		public Guid Id { get; }

		[JsonProperty("gravatarHash")]
		public string GravatarHash { get; }

		[JsonProperty("displayName")]
		public string DisplayName { get; }
	}
}
