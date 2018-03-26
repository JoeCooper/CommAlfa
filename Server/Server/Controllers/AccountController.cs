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
using Server.Services;
using Microsoft.Extensions.Logging;

namespace Server.Controllers
{
    [Route("account")]
    public class AccountController : Controller
	{
		//Any values over this likely represent the client messing with us
		const int PropertyLengthLimit = 1024;

        readonly IDatabaseService databaseService;
		readonly RecaptchaConfiguration recaptchaConfiguration;
		readonly ILogger<AccountController> logger;

		public AccountController(IDatabaseService _databaseService, IOptions<RecaptchaConfiguration> _recaptchaConfiguration, ILogger<AccountController> logger) {
            databaseService = _databaseService;
			recaptchaConfiguration = _recaptchaConfiguration.Value;
			this.logger = logger;
        }

        [HttpGet]
        [Authorize]
        public IActionResult Index() {
            var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
            var id = Guid.Parse(nameIdentifierClaim.Value);
			var stringified = WebEncoders.Base64UrlEncode(id.ToByteArray());
			if (stringified.FalsifyAsIdentifier())
				return BadRequest();
			return View("Edit", new AccountEditViewModel(stringified));
        }

		[HttpPost]
		[Authorize]
		public async Task<IActionResult> SaveAccount(AccountMetadataSubmission submission)
		{
			var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
			var authenticatedId = Guid.Parse(nameIdentifierClaim.Value);
			var stringified = WebEncoders.Base64UrlEncode(authenticatedId.ToByteArray());
			var failureBuilder = ImmutableArray.CreateBuilder<AccountEditFailureReasons>();
			var accountAsPulled = await databaseService.GetAccountAsync(authenticatedId);
			var account = accountAsPulled;
			if(submission.NewPassword != null)
			{
				if (submission.Password.Length > PropertyLengthLimit)
				{
					logger.LogWarning("Account save rejected; Reason: password violates property length limit; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
					return BadRequest();
				}
				if(Password.EvaluatePassword(submission.Password, accountAsPulled.PasswordDigest) == false)
				{
					logger.LogWarning("Account save rejected; Reason: current password is wrong; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
					failureBuilder.Add(AccountEditFailureReasons.PasswordIsWrong);
				}
				if(VetPassword(submission.NewPassword))
				{
					account = account.WithPasswordDigest(Password.GetPasswordDigest(submission.NewPassword));
				}
				else
				{
					failureBuilder.Add(AccountEditFailureReasons.PasswordIsInadequate);						
				}
			}
			if(submission.DisplayName != null)
			{
				if(VetDisplayName(submission.DisplayName))
				{
					account = account.WithDisplayName(submission.DisplayName);
				}
				else
				{
					failureBuilder.Add(AccountEditFailureReasons.DisplayNameIsBlank);
				}
			}
			if(account != accountAsPulled && !failureBuilder.Any())
			{
				await databaseService.SaveAccountAsync(account, false);
			}
			return View("Edit", new AccountEditViewModel(submission.DisplayName ?? String.Empty, failureBuilder, stringified));
		}

        [HttpGet("{id}")]
		public IActionResult GetAccount(string id)
		{
			if (id.FalsifyAsIdentifier())
			{
				logger.LogWarning("Account id rejected; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
				return BadRequest();
			}
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
			{
				logger.LogWarning("Login rejected; Reason: missing body; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
				return BadRequest();
			}
			if (submission.Password.Length > PropertyLengthLimit)
			{
				logger.LogWarning("Login rejected; Reason: password violates property length limit; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
				return BadRequest();
			}
			if (submission.Username.Length > PropertyLengthLimit)
			{
				logger.LogWarning("Login rejected; Reason: username violates property length limit; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
				return BadRequest();
			}
			if (VetEmail(submission.Username) && VetPassword(submission.Password))
			{
                var account = await databaseService.GetAccountAsync(submission.Username);
                var isValid = Password.EvaluatePassword(submission.Password, account.PasswordDigest);
                if (isValid)
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

		[HttpPost("register")]
		public async Task<IActionResult> Register(RegistrationSubmission submission, string returnUrl)
		{
			if (submission == null)
			{
				logger.LogWarning("Registration rejected; Reason: missing body; Origin: {0}", HttpContext.Connection.RemoteIpAddress);
				return BadRequest();
			}
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
                try
                {
                    var account = new Account(
                        Guid.NewGuid(),
                        submission.DisplayName ?? string.Empty,
                        submission.Username ?? string.Empty,
                        Password.GetPasswordDigest(submission.Password ?? string.Empty));
                    await databaseService.SaveAccountAsync(account, true);
					logger.LogWarning("Registration accepted; Id: {0}; Origin: {1}", WebEncoders.Base64UrlEncode(account.Id.ToByteArray()), HttpContext.Connection.RemoteIpAddress);
                }
                catch(DuplicateKeyException)
                {
                    failureBuilder.Add(RegistrationFailureReasons.EmailIsInUse);
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
