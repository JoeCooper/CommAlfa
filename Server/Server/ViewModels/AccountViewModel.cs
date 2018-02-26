using System;
using System.Collections.Generic;

namespace Server.ViewModels
{
    public class AccountViewModel
    {
        public AccountViewModel(Guid id, string displayName, string gravatarHash, IEnumerable<DocumentListingViewModel> documents, bool mayEdit)
        {
            Id = id;
            DisplayName = displayName;
            GravatarHash = gravatarHash;
            Documents = documents;
            MayEdit = mayEdit;
        }

        public Guid Id { get; }

        public string DisplayName { get; }

        public string GravatarHash { get; }

        public IEnumerable<DocumentListingViewModel> Documents { get; }

        public bool MayEdit { get; }
    }
}
