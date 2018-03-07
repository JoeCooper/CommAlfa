using System;
using System.Collections.Generic;
using Server.Models;

namespace Server.ViewModels
{
    public class DocumentListingViewModel
	{
		const string Ellipsis = "…";

        public DocumentListingViewModel(MD5Sum id, string title, Guid authorId, DateTime timestamp):
			this(id, title, Ellipsis, authorId, timestamp)
        {
        }

        public DocumentListingViewModel(MD5Sum id, string title, string authorDisplayName, Guid authorId, DateTime timestamp)
        {
            Id = id;
            Title = title;
            AuthorDisplayName = authorDisplayName;
            AuthorId = authorId;
            Timestamp = timestamp;
        }

        public MD5Sum Id { get; }

        public string Title { get; }

        public string AuthorDisplayName { get; }

        public Guid AuthorId { get; }

        public DateTime Timestamp { get; }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(obj is DocumentListingViewModel) {
                var other = (DocumentListingViewModel)obj;
                return Id.Equals(other.Id);
            }
            return false;
        }

        public DocumentListingViewModel WithAuthorDisplayName(string displayName) {
            return new DocumentListingViewModel(Id, Title, displayName, AuthorId, Timestamp);
        }
    }
}
