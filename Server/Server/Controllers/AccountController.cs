using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Server.ViewModels;
using Server.Models;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Net;
using System.Collections.Specialized;
using System.Net.Http;
using Server.Utilities;

namespace Server.Controllers
{
    [Route("account")]
    public class AccountController : Controller
	{
		//Any values over this likely represent the client messing with us
		const int PropertyLengthLimit = 1024;

        readonly DatabaseConfiguration databaseConfiguration;
        readonly RecaptchaConfiguration recaptchaConfiguration;

        public AccountController(IOptions<DatabaseConfiguration> _databaseConfiguration, IOptions<RecaptchaConfiguration> _recaptchaConfiguration) {
            databaseConfiguration = _databaseConfiguration.Value;
            recaptchaConfiguration = _recaptchaConfiguration.Value;
        }

        [HttpGet]
        [Authorize]
        public IActionResult Index() {
            var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
            var id = Guid.Parse(nameIdentifierClaim.Value);
			var stringified = WebEncoders.Base64UrlEncode(id.ToByteArray());
			if (stringified.FalsifyAsIdentifier())
				return BadRequest();
			return GetAccount(stringified);
        }

        [HttpGet("{id}")]
		public IActionResult GetAccount(string id) {
			if (id.FalsifyAsIdentifier())
				return BadRequest();
			return View("Account", new IdentifierViewModel(id));
        }

        [HttpGet("login")]
        public IActionResult Login(string returnUrl)
        {
			return View(new LoginViewModel(returnUrl));
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout() {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(string returnUrl, LoginSubmission submission) {
			if (submission == null)
				return BadRequest();
			if (submission.Password.Length > PropertyLengthLimit)
				return BadRequest();
			if (submission.Username.Length > PropertyLengthLimit)
				return BadRequest();
			Account account = null;
			if (VetEmail(submission.Username) && VetPassword(submission.Password))
			{
				using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
				using (var cmd = new NpgsqlCommand())
				{
					await conn.OpenAsync();
					cmd.Connection = conn;
					cmd.CommandText = "SELECT id,displayName,password_digest FROM account WHERE email=@email;";
					cmd.Parameters.AddWithValue("@email", submission.Username ?? string.Empty);
					using (var reader = await cmd.ExecuteReaderAsync())
					{
						if (await reader.ReadAsync())
						{
							var guid = reader.GetGuid(0);
							var displayName = reader.GetString(1);
							var extantDigest = new byte[384 / 8];
							reader.GetBytes(2, 0, extantDigest, 0, extantDigest.Length);
							var isValid = EvaluatePassword(submission.Password, extantDigest);
							if (isValid)
							{
								account = new Account(guid, displayName, string.Empty);
							}
						}
					}
				}
			}
			if (account != null)
			{
				var claims = new[] {
							new Claim(ClaimTypes.NameIdentifier, account.Id.ToString(), ClaimValueTypes.String),
							new Claim(ClaimTypes.Name, account.DisplayName, ClaimValueTypes.String)
						};
				var claimsIdentity = new ClaimsIdentity(claims, "SecureLogin");
				var authProperties = new AuthenticationProperties
				{
					ExpiresUtc = DateTimeOffset.UtcNow.AddYears(1),
					IsPersistent = true
				};
				await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
											  new ClaimsPrincipal(claimsIdentity),
											  authProperties);
				return Redirect(returnUrl ?? "/");
			}
			return View(new LoginViewModel(returnUrl, true));
        }

        [HttpGet("register")]
		public IActionResult Register(string returnUrl)
        {
            var viewModel = new RegistrationViewModel(returnUrl);
            if(recaptchaConfiguration.HasConfiguration)
            {
                viewModel = viewModel.WithRecaptchaSiteKey(recaptchaConfiguration.SiteKey);
            }
            return View(viewModel);
        }

        static bool EvaluatePassword(string password, byte[] extantDigest) {
            var extantSalt = new byte[128 / 8];
            var extantHash = new byte[256 / 8];

            Array.Copy(extantDigest, extantSalt, extantSalt.Length);
            Array.Copy(extantDigest, extantSalt.Length, extantHash, 0, extantHash.Length);

            var hash = KeyDerivation.Pbkdf2(password, extantSalt, KeyDerivationPrf.HMACSHA1, 10000, 256 / 8);

            var match = true;

            System.Diagnostics.Debug.Assert(hash.Length == extantHash.Length);

            for (var i = 0; i < extantHash.Length; i++) {
                match &= extantHash[i] == hash[i];
            }

            return match;
        }

        protected static byte[] GetPasswordDigest(string password) {
            // generate a 128-bit salt using a secure PRNG
            var salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            var hashed = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA1, 10000, 256 / 8);

            var digest = new byte[384 / 8];

            Array.Copy(salt, digest, salt.Length);
            Array.Copy(hashed, 0, digest, salt.Length, hashed.Length);

            return digest;
        }

		[HttpPost("register")]
		public async Task<IActionResult> Register(RegistrationSubmission submission, string returnUrl)
		{
			if (submission == null)
				return BadRequest();
			{
				//This is a hack. WebAPI cannot automatically deserialized the recaptcha response
				//because its key has an unexpected form and it doesn't have any documented method
				//to specify the serialized key of the field, akin to [JsonProperty] or [DataMember].
				const string recaptchaResponseKey = "g-recaptcha-response";
				if (Request.Form.ContainsKey(recaptchaResponseKey))
				{
					submission.RecaptchaResponse = Request.Form[recaptchaResponseKey];
				}
			}
            var failureBuilder = ImmutableArray.CreateBuilder<RegistrationFailureReasons>();
            if(VetEmail(submission.Username) == false) {
                failureBuilder.Add(RegistrationFailureReasons.EmailIsInvalid);
            }
            if(VetPassword(submission.Password) == false) {
                failureBuilder.Add(RegistrationFailureReasons.PasswordIsInadequate);
            }
            if(VetDisplayName(submission.DisplayName) == false) {
                failureBuilder.Add(RegistrationFailureReasons.DisplayNameIsBlank);
            }
            if (recaptchaConfiguration.HasConfiguration && string.IsNullOrWhiteSpace(submission.RecaptchaResponse))
            {
                failureBuilder.Add(RegistrationFailureReasons.CaptchaFail);
            }
            if (recaptchaConfiguration.HasConfiguration && !string.IsNullOrWhiteSpace(submission.RecaptchaResponse) && failureBuilder.Any() == false)
            {
                using(var httpClient = new HttpClient()) {
                    var recaptchaUrl = new Uri("https://www.google.com/recaptcha/api/siteverify");
                    var requestBody = new[] {
                        new KeyValuePair<string,string>("secret", recaptchaConfiguration.SecretKey),
                        new KeyValuePair<string, string>("response", submission.RecaptchaResponse)
                    };
                    var response = await httpClient.PostAsync(recaptchaUrl, new FormUrlEncodedContent(requestBody));
                    var rawResponseBody = await response.Content.ReadAsStringAsync();
                    var recaptchaResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<RecaptchaResponse>(rawResponseBody);
                    if(recaptchaResponse.Success == false) {
                        failureBuilder.Add(RegistrationFailureReasons.CaptchaFail);
                    }
                }
            }
            if(!failureBuilder.Any()) {
                using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
                using (var cmd = new NpgsqlCommand())
                {
                    await conn.OpenAsync();
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO account(id,displayName,email,password_digest) values(@guid,@displayName,@email,@password_digest)";
                    cmd.Parameters.AddWithValue("@guid", Guid.NewGuid());
                    cmd.Parameters.AddWithValue("@displayName", submission.DisplayName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@email", submission.Username ?? string.Empty);
                    cmd.Parameters.AddWithValue("@password_digest", GetPasswordDigest(submission.Password ?? string.Empty));
                    try {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch(PostgresException ex) {
                        if(ex.SqlState == "23505") {
                            failureBuilder.Add(RegistrationFailureReasons.EmailIsInUse);
                        }
                        else {
                            throw ex;
                        }
                    }
                }
            }
            if(failureBuilder.Any()) {
				var viewModel = new RegistrationViewModel(returnUrl, submission.DisplayName ?? string.Empty, submission.Password ?? string.Empty, failureBuilder);
                if (recaptchaConfiguration.HasConfiguration)
                {
                    viewModel = viewModel.WithRecaptchaSiteKey(recaptchaConfiguration.SiteKey);
                }
                return View(viewModel);
            }
			return RedirectToAction(nameof(Login), new { returnUrl });
        }

        public static bool VetEmail(string email)
        {
			return !string.IsNullOrWhiteSpace(email) && EmailValidation.EmailValidator.Validate(email) && email.Length < PropertyLengthLimit;
        }

		public static bool VetDisplayName(string displayName)
        {
			return !string.IsNullOrWhiteSpace(displayName) && displayName.Length < PropertyLengthLimit;
        }

		public static bool VetPassword(string password)
        {
			return !string.IsNullOrWhiteSpace(password) && password.Length >= 6 && password.Length < PropertyLengthLimit;
        }
    }
}
