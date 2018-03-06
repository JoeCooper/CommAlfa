using Server.Models;
using System.Collections.Generic;
using System.Linq;

namespace Server.ViewModels
{
    public class RegistrationViewModel
    {
        public RegistrationViewModel(string returnUrl) : this(returnUrl, string.Empty, string.Empty, null, Enumerable.Empty<RegistrationFailureReasons>())
        {
        }

		public RegistrationViewModel(string returnUrl, string displayName, string username, IEnumerable<RegistrationFailureReasons> reasons) : this(returnUrl, displayName, username, null, reasons)
        {
        }

        public RegistrationViewModel(string returnUrl, string displayName, string username, string recaptchaSiteKey, IEnumerable<RegistrationFailureReasons> reasons)
        {
            DisplayName = displayName;
            Username = username;
            RecaptchaSiteKey = recaptchaSiteKey;
            Reasons = reasons;
			ReturnUrl = returnUrl;
        }

		public string ReturnUrl { get; }

        public bool UseRecaptcha { get => !string.IsNullOrWhiteSpace(RecaptchaSiteKey); }

        public string DisplayName { get; }

        public string Username { get; }

        public string RecaptchaSiteKey { get; }

        public IEnumerable<RegistrationFailureReasons> Reasons { get; }

        public bool Failed { get => Reasons.Any(); }

        public RegistrationViewModel WithRecaptchaSiteKey(string recaptchaSiteKey) {
            return new RegistrationViewModel(ReturnUrl,DisplayName, Username, recaptchaSiteKey, Reasons);
        }
    }
}
