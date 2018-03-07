using System.Runtime.Serialization;

namespace Server.Models
{
    public class RegistrationSubmission
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public string DisplayName { get; set; }

		public string RecaptchaResponse { get; set; }
    }
}
