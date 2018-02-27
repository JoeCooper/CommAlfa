using System;
using System.Collections.Generic;
using Server.Models;

namespace Server.ViewModels
{
    public class HistoryViewModel: DocumentListingViewModel
    {
        public HistoryViewModel(
            DocumentListingViewModel subject,
            IEnumerable<Relation> relations, IEnumerable<DocumentListingViewModel> documentsInFamily):
            base(subject.Id, subject.Title, subject.AuthorDisplayName, subject.AuthorId, subject.Timestamp)
        {
            Relations = relations;
            DocumentsInFamily = documentsInFamily;
        }

        public IEnumerable<DocumentListingViewModel> DocumentsInFamily { get; }

        public IEnumerable<Relation> Relations { get; }
    }
}
