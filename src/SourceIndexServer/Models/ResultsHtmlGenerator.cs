using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class ResultsHtmlGenerator
    {
        private readonly Query query;
        private readonly StringBuilder sb = new StringBuilder();

        public ResultsHtmlGenerator(Query query)
        {
            this.query = query;
        }

        public string Generate(Stopwatch sw = null, Index index = null, string usageStats = null)
        {
            sb.Clear();

            if (query.HasDiagnostics)
            {
                sb.AppendLine(Markup.Note(query.Diagnostics));
                AppendAffiliateLinks(query);
                return sb.ToString();
            }

            if (!query.HasResults)
            {
                var symbolSearchTerm = query.GetSearchTermForSymbolSearch();
                if (!string.IsNullOrEmpty(symbolSearchTerm) && symbolSearchTerm.Length < 3)
                {
                    sb.AppendLine(Markup.Note("Please specify at least 3 characters for the symbol name, otherwise too many results would be returned"));
                }
                else
                {
                    sb.AppendLine(Markup.Note("No results found"));
                }

                AppendAffiliateLinks(query);
                return sb.ToString();
            }

            WriteSymbolResults(index);
            WriteAssemblyResults(index);
            WriteProjectResults(index);
            WriteGuidResults(index);
            WriteMSBuildPropertiesResults(index);
            WriteMSBuildItemsResults(index);
            WriteMSBuildTargetsResults(index);
            WriteMSBuildTasksResults(index);

            if (sw != null)
            {
                string message = "Generated in " + sw.ElapsedMilliseconds + " ms.";
                if (usageStats != null)
                {
                    message += "<br>" + usageStats;
                }

                message = Markup.Note(message);
                sb.AppendLine(message);
            }

            return sb.ToString();
        }

        private void WriteMSBuildPropertiesResults(Index index)
        {
            if (query.ResultMSBuildProperties == null || !query.ResultMSBuildProperties.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "MSBuild Properties");
            string count = query.ResultMSBuildProperties.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string header = "MSBuild properties that match '" + query.OriginalString + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", header);
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "MSBuild Properties");

            foreach (var text in query.ResultMSBuildProperties)
            {
                var url = text;
                WriteLine(
                    "<a href=\"{0}/{1}/{2}.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{2}</div>",
                    Constants.MSBuildPropertiesAssembly,
                    Constants.ReferencesFileName,
                    text);
                WriteLine("</div></a>");
            }

            WriteLine("</div></div>");
        }

        private void WriteMSBuildItemsResults(Index index)
        {
            if (query.ResultMSBuildItems == null || !query.ResultMSBuildItems.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "MSBuild Items");
            string count = query.ResultMSBuildItems.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string header = "MSBuild items that match '" + query.OriginalString + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", header);
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "MSBuild Items");

            foreach (var text in query.ResultMSBuildItems)
            {
                var url = text;
                WriteLine(
                    "<a href=\"{0}/{1}/{2}.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{2}</div>",
                    Constants.MSBuildItemsAssembly,
                    Constants.ReferencesFileName,
                    text);
                WriteLine("</div></a>");
            }

            WriteLine("</div></div>");
        }

        private void WriteMSBuildTargetsResults(Index index)
        {
            if (query.ResultMSBuildTargets == null || !query.ResultMSBuildTargets.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "MSBuild Targets");
            string count = query.ResultMSBuildTargets.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string header = "MSBuild targets that match '" + query.OriginalString + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", header);
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "MSBuild Targets");

            foreach (var text in query.ResultMSBuildTargets)
            {
                var url = text;
                WriteLine(
                    "<a href=\"{0}/{1}/{2}.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{2}</div>",
                    Constants.MSBuildTargetsAssembly,
                    Constants.ReferencesFileName,
                    text);
                WriteLine("</div></a>");
            }

            WriteLine("</div></div>");
        }

        private void WriteMSBuildTasksResults(Index index)
        {
            if (query.ResultMSBuildTasks == null || !query.ResultMSBuildTasks.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "MSBuild Tasks");
            string count = query.ResultMSBuildTasks.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string header = "MSBuild tasks that match '" + query.OriginalString + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", header);
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "MSBuild Tasks");

            foreach (var text in query.ResultMSBuildTasks)
            {
                var url = text;
                WriteLine(
                    "<a href=\"{0}/{1}/{2}.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{2}</div>",
                    Constants.MSBuildTasksAssembly,
                    Constants.ReferencesFileName,
                    text);
                WriteLine("</div></a>");
            }

            WriteLine("</div></div>");
        }

        private void AppendAffiliateLinks(Query query)
        {
            WriteLine(Markup.P("Try also searching on:"));
            var term = query.OriginalString;
            term = Markup.UrlEncodeAndHtmlEscape(term);
            WriteLine("<ul>");

            // Read the AffiliateLinks file and display links one by one.
            foreach (string line in AffiliateUrls)
            {
                AppendAffiliateLink(line + term);
            }

            WriteLine("</ul>");
        }

        private void AppendAffiliateLink(string url)
        {
            WriteLine(Markup.Li(Markup.A(url)));
        }

        private static readonly string affiliateLinksFilePath = Path.Combine(Index.RootPath, "AffiliateLinks.txt");
        private static string[] affiliateUrls;
        private static string[] AffiliateUrls
        {
            get
            {
                if (affiliateUrls == null)
                {
                    try
                    {
                        if (File.Exists(affiliateLinksFilePath))
                        {
                            affiliateUrls = File.ReadAllLines(affiliateLinksFilePath);
                        }
                    }
                    catch (System.Exception)
                    {
                    }

                    if (affiliateUrls == null)
                    {
                        affiliateUrls = new string[0];
                    }
                }

                return affiliateUrls;
            }
        }

        private void WriteSymbolResults(Index index)
        {
            if (query.ResultSymbols == null || !query.ResultSymbols.Any())
            {
                return;
            }

            GenerateResultCount();

            var groups = query.ResultSymbols
                .GroupBy(s => s.AssemblyName)
                .Select(g => new
                {
                    AssemblyName = g.Key,
                    Results = g,
                    AssemblyWeight = GetAssemblyWeight(g),
                    NumberOfReferences = GetNumberOfReferences(g.Key, index)
                })
                .OrderBy(g => g.AssemblyWeight)
                .ThenByDescending(g => g.NumberOfReferences);
            foreach (var symbolsInAssembly in groups)
            {
                WriteAssembly(symbolsInAssembly.Results);
            }
        }

        private int GetNumberOfReferences(string assemblyName, Index index)
        {
            return index.GetReferencingAssembliesCount(assemblyName);
        }

        private static int GetAssemblyWeight(IEnumerable<DeclaredSymbolInfo> resultsInAssembly)
        {
            return resultsInAssembly.Min(d => d.Weight);
        }

        private void WriteAssemblyResults(Index index = null)
        {
            if (query.ResultAssemblies == null || !query.ResultAssemblies.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "Assemblies");
            string count = query.ResultAssemblies.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string assemblySearchTerm = query.GetSearchTermForAssemblySearch() ?? "";
            if (!string.IsNullOrEmpty(assemblySearchTerm))
            {
                assemblySearchTerm = "that start with '" + assemblySearchTerm + "' ";
            }

            string assemblyHeader = "Assemblies " + assemblySearchTerm + "(" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", assemblyHeader);
            //WriteLine(sb, "<div class=\"resultGroupProjectPath\">{0}</div>", Markup.HtmlEscape(symbolsInAssembly.First().ProjectFilePath));
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "Assemblies");

            foreach (var assembly in query.ResultAssemblies)
            {
                var url = assembly;
                WriteLine("<a href=\"/#{0}\" target=\"_top\"><div class=\"resultItem\"><div class=\"resultLine\">{0}</div>", url.AssemblyName);
                var projectKey = url.ProjectKey;
                if (projectKey >= 0)
                {
                    WriteLine("<div class=\"resultDescription\">{0}</div></div></a>", index.projects[projectKey]);
                }
                else
                {
                    WriteLine("</div></a>");
                }
            }

            WriteLine("</div></div>");
        }

        private void WriteProjectResults(Index index = null)
        {
            if (query.ResultProjects == null || !query.ResultProjects.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "Projects");
            string count = query.ResultProjects.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string assemblyHeader = "Projects in '" + query.GetSearchTermForProjectSearch() + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", assemblyHeader);
            //WriteLine(sb, "<div class=\"resultGroupProjectPath\">{0}</div>", Markup.HtmlEscape(symbolsInAssembly.First().ProjectFilePath));
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "Projects");

            foreach (var project in query.ResultProjects)
            {
                var url = project;
                Write(
                    "<a href=\"{0}/ProjectExplorer.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{1}</div>",
                    url.AssemblyName,
                    index.projects[url.ProjectKey]);
                WriteLine("<div class=\"resultDescription\">{0}</div></div></a>", Markup.HtmlEscape(url.AssemblyName));
            }

            WriteLine("</div></div>");
        }

        private void WriteGuidResults(Index index)
        {
            if (query.ResultGuids == null || !query.ResultGuids.Any())
            {
                return;
            }

            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", "Guids");
            string count = query.ResultGuids.Count.ToString();
            if (count == Index.MaxRawResults.ToString())
            {
                count = "showing first " + Index.MaxRawResults.ToString();
            }

            string guidHeader = "Guids that match '" + query.OriginalString + "' (" + count + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", guidHeader);
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", "Guids");

            foreach (var guidText in query.ResultGuids)
            {
                var url = guidText;
                WriteLine(
                    "<a href=\"{0}/{1}/{2}.html\" target=\"n\"><div class=\"resultItem\"><div class=\"resultLine\">{2}</div>",
                    Constants.GuidAssembly,
                    Constants.ReferencesFileName,
                    guidText);
                WriteLine("</div></a>");
            }

            WriteLine("</div></div>");
        }

        private void GenerateResultCount()
        {
            int count = query.ResultSymbols.Count();
            string message = count + string.Format(" result{0} found:", count == 1 ? "" : "s");
            if (count >= Index.MaxRawResults)
            {
                if (query.PotentialRawResults > count)
                {
                    count = query.PotentialRawResults;
                }

                message = "Displaying top " + Index.MaxRawResults + " results out of " + count + ":";
            }
            else if (count == 0)
            {
                message = "No results found.";
            }

            var url = string.Format("/#q={0}", Markup.UrlEncodeAndHtmlEscape(query.OriginalString));
            message = Markup.A(url, message, "_top");

            sb.AppendLine(Markup.Note(message));
        }

        private void WriteAssembly(IGrouping<string, DeclaredSymbolInfo> symbolsInAssembly)
        {
            WriteLine("<div class=\"resultGroup\">");
            WriteLine("<a href=\"javascript:void(0)\" class=\"resultGroupHeader\" onClick=\"toggle(this, '{0}');\">", symbolsInAssembly.Key);
            string assemblyHeader = symbolsInAssembly.Key;
            ////assemblyHeader = assemblyHeader + " (" + symbolsInAssembly.Count().ToString() + ")";
            WriteLine("<div class=\"resultGroupAssemblyName\">{0}</div>", assemblyHeader);
            WriteLine("<div class=\"resultGroupProjectPath\">{0}</div>", Markup.HtmlEscape(symbolsInAssembly.First().ProjectFilePath));
            WriteLine("</a>");
            WriteLine("<div id=\"{0}\">", symbolsInAssembly.Key);

            foreach (var symbol in symbolsInAssembly)
            {
                Markup.WriteSymbol(symbol, sb);
            }

            WriteLine("</div></div>");
        }

        private void Write(string format, params object[] args)
        {
            sb.Append(string.Format(format, args));
        }

        private void WriteLine(string format, params object[] args)
        {
            var line = format;
            if (args.Length > 0)
            {
                line = string.Format(line, args);
            }

            sb.AppendLine(line);
        }
    }
}
