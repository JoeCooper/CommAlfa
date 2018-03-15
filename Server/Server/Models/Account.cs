using System;
namespace Server.Models
{
    public class Account
    {
        public Account(Guid id, string displayName, string email)
        {
            Id = id;
            DisplayName = displayName;
			Email = email;
        }
        public Guid Id { get; }
        public string DisplayName { get; }
		public string Email { get; }
    }
}
