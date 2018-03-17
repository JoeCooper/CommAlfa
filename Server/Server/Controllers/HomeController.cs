using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Server.Models;
using Server.Services;
using Server.Utilities;

namespace Server.Controllers
{
    public class HomeController : Controller
    {
        readonly IDatabaseService databaseService;

        public HomeController(IDatabaseService databaseService)
        {
            this.databaseService = databaseService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("sitemap.xml")]
        [ResponseCache(CacheProfileName = CacheProfileNames.Sitemap)]
        public IActionResult GetSitemap()
        {
            return new SitemapResult(databaseService, Url);
        }

        [HttpGet("robots.txt")]
        [ResponseCache(CacheProfileName = CacheProfileNames.Sitemap)]
        public IActionResult GetRobots()
        {
            return Content(string.Format("User-agent: *\nSitemap: {0}", Url.Action(nameof(GetSitemap), null, null, "http")), "text/plain");
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        class SitemapResult : IActionResult
        {
            readonly IDatabaseService databaseService;
            readonly IUrlHelper urlHelper;

            public SitemapResult(IDatabaseService databaseService, IUrlHelper urlHelper)
            {
                this.databaseService = databaseService;
                this.urlHelper = urlHelper;
            }

            public async Task ExecuteResultAsync(ActionContext context)
            {
                var response = context.HttpContext.Response;

                response.StatusCode = 200;
                response.ContentType = "application/xml";

                using (var reader = await databaseService.GetDocumentMetadataAsync())
                using (var xmlWriter = XmlWriter.Create(response.Body, new XmlWriterSettings
                {
                    Async = true
                }))
                {
                    await xmlWriter.WriteStartDocumentAsync();
                    await xmlWriter.WriteStartElementAsync(null, "urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                    await xmlWriter.WriteAttributeStringAsync(null, "schemaLocation", "http://www.w3.org/2001/XMLSchema-instance", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                    const int MaximumRecords = 50000; //as per the spec

                    int recordsEncodedThusFar = 0;

                    while (recordsEncodedThusFar < MaximumRecords && await reader.MoveNextAsync())
                    {
                        recordsEncodedThusFar++;

                        var row = reader.Current;

                        var idEncoded = row.Id.ToString();
                        var timestampEncoded = row.Timestamp.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

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
