using System;
namespace Server.Models
{
    public class Account
    {
        public Account(Guid id, string displayName, string email):
            this(id, displayName, email, Array.Empty<byte>())
        {
        }

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
    }
}
