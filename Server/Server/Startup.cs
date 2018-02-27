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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Server.Models;

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
            services.AddOptions();

            services.Configure<DatabaseConfiguration>(options => {
                options.ConnectionString = Environment.GetEnvironmentVariable("POSTGRES_URL");
            });

            services.Configure<RecaptchaConfiguration>(options => {
                options.SecretKey = Environment.GetEnvironmentVariable("RECAPTCHA_SECRETKEY");
                options.SiteKey = Environment.GetEnvironmentVariable("RECAPTCHA_SITEKEY");
            });
            
            services.AddMvc();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
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

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
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
