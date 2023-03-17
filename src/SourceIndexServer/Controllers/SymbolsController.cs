using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.SourceIndexServer.Models;
using Index = Microsoft.SourceBrowser.SourceIndexServer.Models.Index;

namespace Microsoft.SourceBrowser.SourceIndexServer.Controllers
{
    [ApiController]
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

        [HttpGet("/api/symbolurl")]
        public IActionResult GetSymbolUrl(string symbolId)
        {
            try
            {
                if (!TryGetSymbolId(symbolId, out ulong id))
                {
                    return NotFound();
                }

                var index = _provider.GetRequiredService<Index>();
                if (!index.symbolsById.TryGetValue(id, out int position))
                {
                    return NotFound();
                }

                if (position < 0 || position >= index.symbols.Count)
                {
                    return NotFound();
                }

                var symbol = index.symbols[position];
                var info = symbol.GetDeclaredSymbolInfo(index.huffman, index.assemblies, index.projects);
                var url = info.GetUrl();

                return Content(url, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                var text = Markup.Note(ex.ToString());
                return NotFound(text);
            }
        }

        private bool TryGetSymbolId(string text, out ulong id)
        {
            id = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (TryParseHexStringToULong(text, out id))
            {
                return true;
            }

            id = GetMD5HashULong(text, 16);
            return true;
        }

        public static bool TryParseHexStringToULong(string text, out ulong id)
        {
            id = 0;

            if (text == null || text.Length != 16)
            {
                return false;
            }

            for (int i = 0; i < 8; i++)
            {
                if (!byte.TryParse(text.Substring(14 - i * 2, 2), NumberStyles.AllowHexSpecifier, provider: null, out byte hexByte))
                {
                    return false;
                }

                id = id << 8 | hexByte;
            }

            return true;
        }

        public static ulong GetMD5HashULong(string input, int digits)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(bytes);
                return BitConverter.ToUInt64(hashBytes, 0);
            }
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