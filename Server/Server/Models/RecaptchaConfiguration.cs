
namespace Server.Models
{
    public class RecaptchaConfiguration
    {
        public string SiteKey { get; set; }
        public string SecretKey { get; set; }
        public bool HasConfiguration { get => !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(SiteKey); }
    }
}
