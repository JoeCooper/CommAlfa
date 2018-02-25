using System;
namespace Server.Models
{
    public class Account
    {
        public Account(Guid id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
        public Guid Id { get; }
        public string DisplayName { get; }
    }
}
