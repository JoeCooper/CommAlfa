using System;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Npgsql;
using Server.Models;
using Server.Utilities;

namespace Server.Controllers
{
    [Route("")]
    public class SitemapController: Controller
    {
        readonly DatabaseConfiguration databaseConfiguration;

        public SitemapController(IOptions<DatabaseConfiguration> _databaseConfiguration)
        {
            databaseConfiguration = _databaseConfiguration.Value;
        }

        [HttpGet("sitemap.xml")]
        [ResponseCache(CacheProfileName = CacheProfileNames.Sitemap)]
        public IActionResult GetSitemap()
        {
            return new SitemapResult(databaseConfiguration, Url);
        }

        [HttpGet("robots.txt")]
        [ResponseCache(CacheProfileName = CacheProfileNames.Sitemap)]
        public IActionResult GetRobots()
        {
            return Content(string.Format("User-agent: *\nSitemap: {0}", Url.Action(nameof(GetSitemap), null, null, "http")), "text/plain");
        }

        class SitemapResult : IActionResult
        {
            readonly DatabaseConfiguration databaseConfiguration;
            readonly IUrlHelper urlHelper;

            public SitemapResult(DatabaseConfiguration databaseConfiguration, IUrlHelper urlHelper)
            {
                this.urlHelper = urlHelper;
                this.databaseConfiguration = databaseConfiguration;
            }

            public async Task ExecuteResultAsync(ActionContext context)
            {
                var response = context.HttpContext.Response;

                response.StatusCode = 200;
                response.ContentType = "application/xml";

                using (var connection = new NpgsqlConnection(databaseConfiguration.ConnectionString))
                {
                    await connection.OpenAsync();

                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = connection;
                        cmd.CommandText = "SELECT id,authorId,timestamp FROM document;";

                        using (var xmlWriter = XmlWriter.Create(response.Body, new XmlWriterSettings {
                            Async = true
                        }))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            await xmlWriter.WriteStartDocumentAsync();
                            await xmlWriter.WriteStartElementAsync(null, "urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                            await xmlWriter.WriteAttributeStringAsync(null, "schemaLocation", "http://www.w3.org/2001/XMLSchema-instance", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                            while (await reader.ReadAsync())
                            {
                                var id = reader.GetGuid(0);
                                var authorId = reader.GetGuid(1);
                                var timestamp = reader.GetDateTime(2);

                                var idEncoded = WebEncoders.Base64UrlEncode(id.ToByteArray());
                                var timestampEncoded = timestamp.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

                                var url = urlHelper.Action("GetDocumentForIndexing", "Document", new { id = idEncoded }, "http");

                                await xmlWriter.WriteStartElementAsync(null, "url", null);
                                await xmlWriter.WriteElementStringAsync(null, "loc", null, url);
                                await xmlWriter.WriteElementStringAsync(null, "changefreq", null, "never");
                                await xmlWriter.WriteElementStringAsync(null, "lastmod", null, timestampEncoded);
                                await xmlWriter.WriteEndElementAsync();
                            }

                            await xmlWriter.WriteEndElementAsync();
                            await xmlWriter.WriteEndDocumentAsync();
                        }
                    }
                }
            }
        }
    }
}
