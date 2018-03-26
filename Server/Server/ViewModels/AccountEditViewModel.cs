using System;
using System.Collections.Generic;
using System.Linq;
using Server.Models;

namespace Server.ViewModels
{
	public class AccountEditViewModel: IdentifierViewModel
	{
		public AccountEditViewModel(string id) : this(string.Empty, Enumerable.Empty<AccountEditFailureReasons>(), id)
		{
		}

		public AccountEditViewModel(string displayName, IEnumerable<AccountEditFailureReasons> reasons, string id): base(id)
		{
			DisplayName = displayName;
			Reasons = reasons;
		}

		public string DisplayName { get; }

		public IEnumerable<AccountEditFailureReasons> Reasons { get; }

		public bool Failed { get => Reasons.Any(); }
	}
}
