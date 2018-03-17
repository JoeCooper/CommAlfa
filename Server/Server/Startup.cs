using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Npgsql;
using Server.Middleware;
using Server.Models;
using Server.Services;
using Server.Utilities;

namespace Server
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddResponseCaching();
			services.AddOptions();

			services.AddSingleton<IDatabaseService>(new PostgreSQLDatabaseService(Environment.GetEnvironmentVariable("POSTGRES_URL")));

			services.Configure<InputConfiguration>(options =>
			{
				int bodyLengthLimit, titleLengthLimit;
				if (!int.TryParse(Environment.GetEnvironmentVariable("LIMIT_BODYLENGTH"), out bodyLengthLimit))
				{
					bodyLengthLimit = 262144; //2 to the 18th power
				}
				if (!int.TryParse(Environment.GetEnvironmentVariable("LIMIT_TITLELENGTH"), out titleLengthLimit))
				{
					titleLengthLimit = 128; //2 to the 7th power
				}
				options.BodyLengthLimit = bodyLengthLimit;
				options.TitleLengthLimit = titleLengthLimit;
			});

			services.Configure<RecaptchaConfiguration>(options =>
			{
				options.SecretKey = Environment.GetEnvironmentVariable("RECAPTCHA_SECRETKEY");
				options.SiteKey = Environment.GetEnvironmentVariable("RECAPTCHA_SITEKEY");
			});

			services.AddMvc(options =>
			{
				options.CacheProfiles.Add(CacheProfileNames.Default,
					new CacheProfile
					{
						Duration = 60
					});
				options.CacheProfiles.Add(CacheProfileNames.Never,
					new CacheProfile
					{
						Location = ResponseCacheLocation.None,
						NoStore = true
					});
				options.CacheProfiles.Add(CacheProfileNames.SemiImmutable,
					new CacheProfile
					{
						Location = ResponseCacheLocation.Any,
						Duration = 3600
					});
				options.CacheProfiles.Add(CacheProfileNames.Immutable,
					new CacheProfile
					{
						Location = ResponseCacheLocation.Any,
						Duration = 31536000
					});
                options.CacheProfiles.Add(CacheProfileNames.Sitemap,
                    new CacheProfile
                    {
                        Location = ResponseCacheLocation.Any,
                        Duration = 86400
                    });
			});

			services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
					.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			app.UseResponseCaching();

			app.Use(async (context, next) =>
			{
				context.Response.Headers[HeaderNames.Vary] = new string[] { "Accept-Encoding" };

				await next();
			});

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}

			app.UseStaticFiles();

			app.UseAuthentication();

			app.UseMiddleware(typeof(ErrorHandlingMiddleware));

			app.UseMvc(routes =>
			{
				routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
			});

			var connectionString = Environment.GetEnvironmentVariable("POSTGRES_URL");
			using (var conn = new NpgsqlConnection(connectionString))
			{
				conn.Open();
				var assembly = Assembly.GetExecutingAssembly();
				var resourceName = "Server.Setup.sql";
				string setupSql;
				using (Stream stream = assembly.GetManifestResourceStream(resourceName))
				using (StreamReader reader = new StreamReader(stream))
				{
					setupSql = reader.ReadToEnd();
				}
				using (var cmd = new NpgsqlCommand(setupSql, conn))
				{
					cmd.ExecuteNonQuery();
				}
			}
		}
	}
}
