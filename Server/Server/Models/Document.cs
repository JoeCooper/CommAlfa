using System;
namespace Server.Models
{
    public class Document
    {
        public Document(byte[] id, string title, string body, Guid authorId, DateTime timestamp)
        {
            Id = id;
            Title = title;
            Body = body;
            AuthorId = authorId;
            Timestamp = timestamp;
        }

        public byte[] Id { get; }

        public string Title { get; }

        public string Body { get; }

        public Guid AuthorId { get; }

        public DateTime Timestamp { get; }
    }
}
