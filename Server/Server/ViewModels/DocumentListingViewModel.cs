using System;
using System.Collections.Generic;

namespace Server.ViewModels
{
    public class DocumentListingViewModel
    {
        public DocumentListingViewModel(byte[] id, string title, Guid authorId, DateTime timestamp):
            this(id, title, string.Empty, authorId, timestamp)
        {
        }

        public DocumentListingViewModel(byte[] id, string title, string authorDisplayName, Guid authorId, DateTime timestamp)
        {
            Id = id;
            Title = title;
            AuthorDisplayName = authorDisplayName;
            AuthorId = authorId;
            Timestamp = timestamp;
        }

        public byte[] Id { get; }

        public string Title { get; }

        public string AuthorDisplayName { get; }

        public Guid AuthorId { get; }

        public DateTime Timestamp { get; }

        public DocumentListingViewModel WithAuthorDisplayName(string displayName) {
            return new DocumentListingViewModel(Id, Title, displayName, AuthorId, Timestamp);
        }
    }
}
