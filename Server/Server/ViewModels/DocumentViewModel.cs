using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Server.ViewModels
{
    public class DocumentViewModel
    {
        public DocumentViewModel(Guid authorId, string authorDisplayName):
            this(Enumerable.Empty<byte[]>(), string.Empty, string.Empty, authorId, DateTime.Now, authorDisplayName)
        {
        }
        
        public DocumentViewModel(
            IEnumerable<byte[]> sourceIds,
            string body,
            string title,
            Guid authorId,
            DateTime timestamp):
        this(sourceIds, body, title, authorId, timestamp, "Unknown Author")
        {
        }
        
        public DocumentViewModel(
            IEnumerable<byte[]> sourceIds,
            string body,
            string title,
            Guid authorId,
            DateTime timestamp,
            string authorDisplayName
            )
        {
            SourceIds = sourceIds;
            Body = body;
            Title = title;
            AuthorDisplayName = authorDisplayName;
            Timestamp = timestamp;
            AuthorId = authorId;
        }

        public IEnumerable<byte[]> SourceIds { get; }

        [DataType(DataType.MultilineText)]
        public string Body { get; }

        public string Title { get; }

        public Guid AuthorId { get; }

        public string AuthorDisplayName { get; }

        public DateTime Timestamp { get; }

        public DocumentViewModel WithAuthorDisplayName(string authorDisplayName) {
            return new DocumentViewModel(SourceIds, Body, Title, AuthorId, Timestamp, authorDisplayName);
        }
    }
}
