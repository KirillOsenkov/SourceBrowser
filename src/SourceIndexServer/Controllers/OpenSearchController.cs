using System;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.SourceBrowser.SourceIndexServer.Controllers
{
    [ApiController]
    public class OpenSearchController : Controller
    {
        [HttpGet("/opensearch")]
        public IActionResult GetOpenSearchDescriptionDocument()
        {
            var pathBase = String.IsNullOrWhiteSpace(Request.PathBase) ? "" : "/" + Request.PathBase;
            var urlBase = $"{Request.Scheme}://{Request.Host}{pathBase}";
            var result = String.Join("\r\n",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                "<OpenSearchDescription xmlns=\"http://a9.com/-/spec/opensearch/1.1/\">",
                "	<ShortName>Source Browser</ShortName>",
                $"	<Image height=\"256\" width=\"256\" type=\"image/vnd.microsoft.icon\">{urlBase}/favicon.ico</Image>",
                $"	<Url type=\"text/html\" template=\"{urlBase}/#q={{searchTerms}}\"></Url>",
                "</OpenSearchDescription>"
            );
            return Content(result, "application/opensearchdescription+xml", Encoding.UTF8);
        }
    }
}
