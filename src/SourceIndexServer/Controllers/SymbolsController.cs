using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.SourceIndexServer.Models;

namespace Microsoft.SourceBrowser.SourceIndexServer.Controllers
{
    public class SymbolsController : Controller
    {
        private readonly IServiceProvider _provider;
        private const int MaxInputLength = 260;
        private static readonly Dictionary<string, int> usages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly DateTime serviceStarted = DateTime.UtcNow;
        private static int requestsServed = 0;

        public SymbolsController(IServiceProvider provider)
        {
            _provider = provider;
        }

        [HttpGet("/api/symbols")]
        public IActionResult GetHtml(string symbol)
        {
            string result = null;
            try
            {
                result = GetHtmlCore(symbol);
            }
            catch (Exception ex)
            {
                result = Markup.Note(ex.ToString());
            }

            return Content(result, "text/html", Encoding.UTF8);
        }

        private string UpdateUsages()
        {
            lock (usages)
            {
                var userName = this.User.Identity.Name;
                int requests = 0;
                usages.TryGetValue(userName, out requests);
                requests++;
                requestsServed++;
                usages[userName] = requests;
                return string.Format(
                    "Since {0}:<br>&nbsp;&nbsp;{1} unique users<br>&nbsp;&nbsp;{2} requests served.",
                    serviceStarted.ToLocalTime().ToString("m"),
                    usages.Keys.Count,
                    requestsServed);
            }
        }

        private string GetHtmlCore(string symbol, string usageStats = null)
        {
            if (symbol == null || symbol.Length < 3)
            {
                return Markup.Note("Enter at least 3 characters.");
            }

            if (symbol.Length > 260)
            {
                return Markup.Note(string.Format(
                    "Query string is too long (maximum is {0} characters, input is {1} characters)",
                    MaxInputLength,
                    symbol.Length));
            }

            using (Disposable.Timing("Get symbols"))
            {
                Stopwatch sw = Stopwatch.StartNew();

                var index = _provider.GetRequiredService<Index>();
                var query = index.Get(symbol);

                var result = new ResultsHtmlGenerator(query).Generate(sw, index, usageStats);
                return result;
            }
        }
    }
}