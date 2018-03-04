using System;
using System.Collections;
using System.Collections.Generic;
using Server.ViewModels;
using System.Linq;

namespace Server.Models
{
    public class MutableLane: IEnumerable<DocumentListingViewModel>
    {
        readonly List<DocumentListingViewModel> documentListings;
        readonly HashSet<MD5Sum> documentKeys;

        public MutableLane(Guid authorId)
        {
            documentListings = new List<DocumentListingViewModel>();
            documentKeys = new HashSet<MD5Sum>();
            AuthorId = authorId;
        }

        public MutableLane(DocumentListingViewModel document) : this(document.AuthorId)
        {
            Add(document);
        }

        public Guid AuthorId { get; }

        public bool ContainsKey(DocumentListingViewModel d) {
            return documentKeys.Contains(d.Id);
        }

        public void Add(DocumentListingViewModel d) {
            documentKeys.Add(d.Id);
            documentListings.Add(d);
        }

        public IEnumerator<DocumentListingViewModel> GetEnumerator()
        {
            return ((IEnumerable<DocumentListingViewModel>)documentListings).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<DocumentListingViewModel>)documentListings).GetEnumerator();
        }
    }
}
