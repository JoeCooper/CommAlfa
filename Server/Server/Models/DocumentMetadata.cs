using System;
using Newtonsoft.Json;
using Server.Utilities;

namespace Server.Models
{
	public class DocumentMetadata
	{
		public DocumentMetadata(MD5Sum id, string title, Guid authorId, DateTime timestamp)
		{
			Id = id;
			Title = title;
			AuthorId = authorId;
			Timestamp = timestamp;
		}

		[JsonProperty("id")]
		[JsonConverter(typeof(IdentifierConverter))]
		public MD5Sum Id { get; }

		[JsonProperty("title")]
		public string Title { get; }

		[JsonProperty("authorId")]
		[JsonConverter(typeof(IdentifierConverter))]
		public Guid AuthorId { get; }

		[JsonProperty("timestamp")]
		public DateTime Timestamp;
	}
}
