using System;
namespace Server.Models
{
    public class Account
    {
        public Account(Guid id, string displayName, string email, byte[] passwordDigest)
        {
            Id = id;
            DisplayName = displayName;
			Email = email;
            PasswordDigest = passwordDigest;
        }

        public Guid Id { get; }

        public string DisplayName { get; }

		public string Email { get; }

        public byte[] PasswordDigest { get; }

		public Account WithId(Guid id)
		{
			return new Account(id, DisplayName, Email, PasswordDigest);
		}

		public Account WithDisplayName(string displayName)
		{
			return new Account(Id, displayName, Email, PasswordDigest);
		}

		public Account WithEmail(string email)
		{
			return new Account(Id, DisplayName, email, PasswordDigest);
		}

		public Account WithPasswordDigest(byte[] passwordDigest)
		{
			return new Account(Id, DisplayName, Email, passwordDigest);
		}
    }
}
