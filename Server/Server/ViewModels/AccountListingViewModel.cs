using System;
namespace Server.ViewModels
{
    public class AccountListingViewModel
    {
        public AccountListingViewModel(Guid id, string displayName, string gravatarHash)
        {
            Id = id;
            DisplayName = displayName;
            GravatarHash = gravatarHash;
        }

        public Guid Id { get; }

        public string DisplayName { get; }

        public string GravatarHash { get; }
    }
}
