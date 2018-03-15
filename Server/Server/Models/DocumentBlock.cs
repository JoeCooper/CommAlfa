using System;
namespace Server.Models
{
	public class DocumentBlock
	{
		public DocumentBlock(MD5Sum id, bool isVoluntary, Guid agentId, string comment, DateTime timestamp)
		{
			Id = id;
			IsVoluntary = isVoluntary;
			AgentId = agentId;
			Comment = comment;
			Timestamp = timestamp;
		}

		public MD5Sum Id { get; }
		public bool IsVoluntary { get; }
		public Guid AgentId { get; }
		public string Comment { get; }
		public DateTime Timestamp { get; }
	}
}
