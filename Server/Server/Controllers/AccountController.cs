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

namespace Server.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        readonly DatabaseConfiguration databaseConfiguration;
        readonly RecaptchaConfiguration recaptchaConfiguration;

        public AccountController(IOptions<DatabaseConfiguration> _databaseConfiguration, IOptions<RecaptchaConfiguration> _recaptchaConfiguration) {
            databaseConfiguration = _databaseConfiguration.Value;
            recaptchaConfiguration = _recaptchaConfiguration.Value;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index() {
            var nameIdentifierClaim = User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier);
            var id = Guid.Parse(nameIdentifierClaim.Value);
            return await Detail(id);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Detail(string id) {
            var idInBinary = WebEncoders.Base64UrlDecode(id);
            var idAsGuid = new Guid(idInBinary);
            return await Detail(idAsGuid);
        }

        async Task<IActionResult> Detail(Guid accountId) {
            string displayName;
            string email;
            IEnumerable<DocumentListingViewModel> documentListings;
            IEnumerable<Tuple<Guid, Guid>> boxedRelations;

            using (var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT displayName,email FROM account WHERE id=@id;";
                    cmd.Parameters.AddWithValue("@id", accountId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            displayName = reader.GetString(0);
                            email = reader.GetString(1);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }

                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT id,title,timestamp FROM document WHERE authorId=@authorId;";
                    cmd.Parameters.AddWithValue("@authorId", accountId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var documentListingBuilder = ImmutableArray.CreateBuilder<DocumentListingViewModel>();

                        while (await reader.ReadAsync())
                        {
                            documentListingBuilder.Add(new DocumentListingViewModel(
                                new MD5Sum(reader.GetGuid(0).ToByteArray()),
                                reader.GetString(1),
                                displayName,
                                accountId,
                                reader.GetDateTime(2)
                            ));
                        }

                        documentListings = documentListingBuilder;
                    }
                }

                using(var cmd = new NpgsqlCommand())
                {
                    var boxedDocumentIds = documentListings.Select(d => d.Id.ToGuid()).ToArray();

                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT antecedentId, descendantId FROM relation WHERE ARRAY[antecedentId] <@ @documentIds;";
                    cmd.Parameters.AddWithValue("@documentIds", boxedDocumentIds);

                    using(var reader = await cmd.ExecuteReaderAsync())
                    {
                        var boxedRelationsBuilder = ImmutableArray.CreateBuilder<Tuple<Guid, Guid>>();

                        while(await reader.ReadAsync()) {
                            boxedRelationsBuilder.Add(new Tuple<Guid, Guid>(reader.GetGuid(0), reader.GetGuid(1)));
                        }

                        boxedRelations = boxedRelationsBuilder;
                    }
                }
            }

            {
                var ourDocumentIds = ImmutableHashSet.CreateRange(documentListings.Select(dl => dl.Id.ToGuid()));
                var supercededDocumentIds = ImmutableHashSet.CreateRange(boxedRelations.Where(r => ourDocumentIds.Contains(r.Item2)).Select(r => r.Item1));
                documentListings = documentListings.Where(dl => supercededDocumentIds.Contains(dl.Id.ToGuid()) == false);
            }

            string gravatarHash;

            using (var md5Encoder = MD5.Create())
            {
                md5Encoder.Initialize();
                var flattened = email.Trim().ToLower();
                var buffer = Encoding.UTF8.GetBytes(flattened);
                var hash = md5Encoder.ComputeHash(buffer);
                var builder = new StringBuilder();
                for (var i = 0; i < hash.Length; i++) {
                    builder.AppendFormat("{0:x2}", hash[i]);
                }
                gravatarHash = builder.ToString();
            }

            return View("Detail", new AccountViewModel(accountId, displayName, gravatarHash, documentListings, false));
        }

        [HttpGet("login")]
        public IActionResult Login(string returnUrl)
        {
            return View(new LoginViewModel());
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout() {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(string returnUrl, LoginSubmission submission) {
            using(var conn = new NpgsqlConnection(databaseConfiguration.ConnectionString))
            using(var cmd = new NpgsqlCommand()) {
                await conn.OpenAsync();
                cmd.Connection = conn;
                cmd.CommandText = "SELECT id,displayName,password_digest FROM account WHERE email=@email;";
                cmd.Parameters.AddWithValue("@email", submission.Username ?? string.Empty);
                using(var reader = await cmd.ExecuteReaderAsync()) {
                    Account account = null;
                    if(await reader.ReadAsync()) {
                        var guid = reader.GetGuid(0);
                        var displayName = reader.GetString(1);
                        var extantDigest = new byte[384 / 8];
                        reader.GetBytes(2, 0, extantDigest, 0, extantDigest.Length);
                        var isValid = EvaluatePassword(submission.Password, extantDigest);
                        if(isValid) {
                            account = new Account(guid, displayName);
                        }
                    }
                    if(account != null) {
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
                    return View(new LoginViewModel(true));
                }
            }
        }

        [HttpGet("register")]
        public IActionResult Register()
        {
            var viewModel = new RegistrationViewModel();
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

            var hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: extantSalt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8);

            var match = true;

            System.Diagnostics.Debug.Assert(hash.Length == extantHash.Length);

            for (var i = 0; i < extantHash.Length; i++) {
                match &= extantHash[i] == hash[i];
            }

            return match;
        }

        static byte[] GetPasswordDigest(string password) {
            // generate a 128-bit salt using a secure PRNG
            var salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            var hashed = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8);

            var digest = new byte[384 / 8];

            Array.Copy(salt, digest, salt.Length);
            Array.Copy(hashed, 0, digest, salt.Length, hashed.Length);

            return digest;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegistrationSubmission submission)
        {
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
            if(failureBuilder.Any() == false) {
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
                var viewModel = new RegistrationViewModel(submission.DisplayName ?? string.Empty, submission.Password ?? string.Empty, failureBuilder);
                if (recaptchaConfiguration.HasConfiguration)
                {
                    viewModel = viewModel.WithRecaptchaSiteKey(recaptchaConfiguration.SiteKey);
                }
                return View(viewModel);
            }
            return RedirectToAction(nameof(Login));
        }

        static bool VetEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailValidation.EmailValidator.Validate(email);
        }

        static bool VetDisplayName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) == false;
        }

        static bool VetPassword(string password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Length >= 6;
        }
    }
}
