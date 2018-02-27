using Newtonsoft.Json;

namespace Server.Models
{
    public class RegistrationSubmission
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public string DisplayName { get; set; }

        [JsonProperty("g-recaptcha-response")]
        public string RecaptchaResponse { get; set; }
    }
}
