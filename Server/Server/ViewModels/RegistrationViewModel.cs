using Server.Models;
using System.Collections.Generic;
using System.Linq;

namespace Server.ViewModels
{
    public class RegistrationViewModel
    {
        public RegistrationViewModel() : this(string.Empty, string.Empty, null, Enumerable.Empty<RegistrationFailureReasons>())
        {
        }

        public RegistrationViewModel(string displayName, string username, IEnumerable<RegistrationFailureReasons> reasons) : this(displayName, username, null, reasons)
        {
        }

        public RegistrationViewModel(string displayName, string username, string recaptchaSiteKey, IEnumerable<RegistrationFailureReasons> reasons)
        {
            DisplayName = displayName;
            Username = username;
            RecaptchaSiteKey = recaptchaSiteKey;
            Reasons = reasons;
        }

        public bool UseRecaptcha { get => !string.IsNullOrWhiteSpace(RecaptchaSiteKey); }

        public string DisplayName { get; }

        public string Username { get; }

        public string RecaptchaSiteKey { get; }

        public IEnumerable<RegistrationFailureReasons> Reasons { get; }

        public bool Failed { get => Reasons.Any(); }

        public RegistrationViewModel WithRecaptchaSiteKey(string recaptchaSiteKey) {
            return new RegistrationViewModel(DisplayName, Username, recaptchaSiteKey, Reasons);
        }
    }
}
