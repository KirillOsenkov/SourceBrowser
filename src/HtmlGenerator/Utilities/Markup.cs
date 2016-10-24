using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Markup
    {
        public static string HtmlEscape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            text = System.Security.SecurityElement.Escape(text);

            // HTML doesn't support XML's &apos;
            // need to use &#39; instead
            // http://blogs.msdn.com/kirillosenkov/archive/2010/03/19/apos-is-in-xml-in-html-use-39.aspx#comments
            // http://www.w3.org/TR/html4/sgml/entities.html
            // http://lists.whatwg.org/pipermail/whatwg-whatwg.org/2005-October/004973.html
            // http://en.wikipedia.org/wiki/List_of_XML_and_HTML_character_entity_references
            // http://fishbowl.pastiche.org/2003/07/01/the_curse_of_apos/
            // http://nedbatchelder.com/blog/200703/random_html_factoid_no_apos.html
            // Don't want to use System.Web.HttpUtility.HtmlEncode
            // because I don't want to take a dependency on System.Web
            text = text.Replace("&apos;", "&#39;");
            text = IntersperseLineBreaks(text);

            return text;
        }

        private static string IntersperseLineBreaks(string text)
        {
            text = text.Replace("\n\r", "\n \r");
            return text;
        }

        public static string HtmlEscape(string text, ref int start, ref int end)
        {
            string trimmed = text.TrimStart(' ');

            // pass -1 to make sure both start and end get offset
            // we don't want start to remain where it was
            Offset(ref start, -1, trimmed.Length - text.Length);
            Offset(ref end, -1, trimmed.Length - text.Length);
            text = trimmed;

            trimmed = text.TrimEnd(' ');
            if (end > trimmed.Length)
            {
                end = trimmed.Length;
            }

            text = trimmed;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    Offset(ref start, sb.Length, 3);
                    Offset(ref end, sb.Length, 3);
                    sb.Append("&lt;");
                }
                else if (text[i] == '>')
                {
                    Offset(ref start, sb.Length, 3);
                    Offset(ref end, sb.Length, 3);
                    sb.Append("&gt;");
                }
                else if (text[i] == '\'')
                {
                    Offset(ref start, sb.Length, 4);
                    Offset(ref end, sb.Length, 4);
                    sb.Append("&#39;");
                }
                else if (text[i] == '\"')
                {
                    Offset(ref start, sb.Length, 5);
                    Offset(ref end, sb.Length, 5);
                    sb.Append("&quot;");
                }
                else if (text[i] == '&')
                {
                    Offset(ref start, sb.Length, 4);
                    Offset(ref end, sb.Length, 4);
                    sb.Append("&amp;");
                }
                else
                {
                    sb.Append(text[i]);
                }
            }

            return sb.ToString();
        }

        private static void Offset(ref int position, int barrier, int offset)
        {
            if (position > barrier)
            {
                position += offset;
            }
        }

        private static string referencesFileHeader = @"<!DOCTYPE html>
<html><head><title>{0}</title><link rel=""stylesheet"" href=""../../styles.css""/><script src=""../../scripts.js""></script></head><body onload=""ro();"">";

        public static void WriteReferencesFileHeader(StreamWriter writer, string title)
        {
            writer.WriteLine(referencesFileHeader, title);
        }

        private static string zeroFileName = "0000000000.html";

        public static void WriteReferencesNotFoundFile(string folder)
        {
            string html = @"<!DOCTYPE html>
<html><head><link rel=""stylesheet"" href=""styles.css""/></head>
<body><div class=""rH"">No references found</div></body></html>";
            string filePath = Path.Combine(folder, zeroFileName);
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, html, Encoding.UTF8);
            }
        }

        public static void WriteRedirectFile(string projectFolder)
        {
            string referencesFolder = Path.Combine(projectFolder, Constants.ReferencesFileName);
            string redirectFileName = Path.Combine(projectFolder, Constants.IDResolvingFileName + ".html");
            if (Directory.Exists(referencesFolder) && !File.Exists(redirectFileName))
            {
                string contents = @"<!DOCTYPE html>
<html>
<head>
<title>Redirecting...</title>
<link rel=""stylesheet"" href=""../styles.css"">
<script src=""../scripts.js""></script>
<script>
redirectToReferences();
</script>
</head>
<body>
<div class=""resultGroupAssemblyName"">{0}</div>
<div class=""note"">Assembly is not indexed. References to the symbol in the indexed assemblies are shown to the left.</div>
</body>
</html>";
                contents = string.Format(contents, Path.GetFileName(projectFolder));
                File.WriteAllText(redirectFileName, contents, Encoding.UTF8);
            }
        }

        public static string GetProjectExplorerReference(string url, string assemblyName)
        {
            return string.Format("<a class=\"reference\" href=\"{0}\" target=\"_top\">{1}</a>", url, assemblyName);
        }

        private static string documentHtmlPrefixTemplate = @"<!DOCTYPE html>
<html><head><title>{0}</title><link rel=""stylesheet"" href=""{1}styles.css""><script src=""{1}scripts.js""></script></head>
<body class=""cB"" onload=""{3}({2});"">";
        private static string documentTablePrefix = @"<div class=""cz""><table class=""tb"" cellpadding=""0"" cellspacing=""0""><tr><td valign=""top"" align=""right""><pre id=""glyph"">{1}</pre><td valign=""top"" align=""right""><pre id=""ln"">{0}</pre></td><td valign=""top"" align=""left""><pre id=""code"">";

        public static string GetDocumentPrefix(string title, string relativePathToRoot, int lineCount, string customJSOnloadFunction = "i")
        {
            var html = string.Format(documentHtmlPrefixTemplate, title, relativePathToRoot, lineCount, customJSOnloadFunction);
            return html;
        }

        public static string GetTablePrefix()
        {
            return string.Format(documentTablePrefix, "", "");
        }

        public static string GetTablePrefix(string documentUrl, int pregenerateLineNumbers, string glyphHtml)
        {
            var lineNumberText = GenerateLineNumberText(pregenerateLineNumbers, documentUrl);
            return string.Format(documentTablePrefix, lineNumberText, glyphHtml);
        }

        private static string GenerateLineNumberText(int lineNumbers, string documentUrl)
        {
            if (lineNumbers == 0)
            {
                return string.Empty;
            }

            Func<int, string> FormatLineLinkForDocument = i => FormatLineLink(documentUrl, i);

            return string.Concat(Enumerable.Range(1, lineNumbers).Select(FormatLineLinkForDocument));
        }

        public static string FormatLineLink(string documentUrl, int i)
        {
            return string.Format(
                                "<a id=\"{0}\" href=\"{1}#{0}\" target=\"_top\">{0}</a><br/>",
                                i,
                                documentUrl);
        }

        public static string GetDocumentSuffix()
        {
            return @"</pre></td></tr></table></div></body></html>";
        }

        public static void WriteMetadataToSourceRedirectPrefix(StreamWriter writer)
        {
            string contents = @"<!DOCTYPE html>
<html><head><title>Redirecting...</title><script src=""../scripts.js""></script>
<script>
";
            writer.WriteLine(contents);
        }

        public static void WriteMetadataToSourceRedirectSuffix(StreamWriter writer)
        {
            string contents = @"
</script>
</head><body>
Don't use this page directly, pass #symbolId to get redirected.
</body></html>";
            writer.WriteLine(contents);
        }

        public static void WriteLinkPanel(Action<string> writeLine, params string[] rows)
        {
            writeLine("<div class=\"dH\">");
            writeLine("<table style=\"width: 100%\">");

            foreach (var row in rows)
            {
                writeLine(row);
            }

            writeLine("</table>");
            writeLine("</div>");
        }

        public static void WriteProjectExplorerPrefix(StringBuilder sb, string projectTitle)
        {
            sb.AppendFormat(@"<!DOCTYPE html><html><head><title>{0}</title>
<link rel=""stylesheet"" href=""../styles.css"">
<script src=""../scripts.js""></script>
</head><body class=""projectExplorerBody"">
<div class=""tabChannel""><span class=""activeTab"">Project</span><a class=""inactiveTab"" href=""/#{0},namespaces"" target=""_top"">Namespaces</a></div>
", projectTitle);
        }

        public static void WriteProjectExplorerSuffix(StringBuilder sb)
        {
            sb.AppendLine("<script>initializeProjectExplorer();</script></body></html>");
        }

        public static void WriteSolutionExplorerPrefix(TextWriter writer)
        {
            writer.WriteLine(@"<!DOCTYPE html><html><head><title>Solution Explorer</title><link rel=""stylesheet"" href=""styles.css"" /><script src=""scripts.js""></script></head>
<body class=""solutionExplorerBody"">
    <div>
        <div class=""note"">
            Enter a type or member name or <a href=""/#q=assembly%20"" target=""_top"" class=""blueLink"" onclick=""populateSearchBox('assembly '); return false;"">filter the assembly list</a>.
        </div>
    </div>
<div id=""rootFolder"" style=""display: none;"" class=""folderTitle"">");
        }

        public static void WriteSolutionExplorerSuffix(TextWriter writer)
        {
            writer.WriteLine("</div><script>onSolutionExplorerLoad();</script></body></html>");
        }

        public static void WriteNamespaceExplorerPrefix(string assemblyName, StreamWriter sw, string pathPrefix = "")
        {
            sw.WriteLine(string.Format(@"<!DOCTYPE html><html><head><title>Namespaces</title>
<link rel=""stylesheet"" href=""{0}styles.css"">
<script src=""{0}scripts.js""></script>
</head><body class=""namespaceExplorerBody"">
<div class=""tabChannel""><a class=""inactiveTab"" href=""/#{1}"" target=""_top"">Project</a><span class=""activeTab"">Namespaces</span></div>
", pathPrefix, assemblyName));
        }

        public static void WriteNamespaceExplorerSuffix(StreamWriter sw)
        {
            sw.WriteLine(@"<script>initializeNamespaceExplorer();</script></body></html>");
        }

        public static void WriteProjectIndex(StringBuilder sb, string assemblyName)
        {
            sb.AppendFormat(@"<!DOCTYPE html><html><head><title>Redirecting</title>
<script src=""../scripts.js""></script>
<script>initializeProjectIndex(""../#{0}"");</script>
</head><body></body></html>", assemblyName);
        }

        public static void GenerateResultsHtml(string solutionDestinationFolder)
        {
            var sb = new StringBuilder();

            sb.AppendLine(GetResultsHtmlPrefix());
            sb.AppendLine(GetResultsHtmlSuffix(emitSolutionBrowserLink: false));

            File.WriteAllText(Path.Combine(solutionDestinationFolder, "results.html"), sb.ToString());
        }

        public static void GenerateResultsHtmlWithAssemblyList(string solutionDestinationFolder, IEnumerable<string> assemblyList)
        {
            var sb = new StringBuilder();

            sb.AppendLine(GetResultsHtmlPrefix());

            foreach (var assemblyName in assemblyList)
            {
                sb.AppendFormat(
                  @"<a href=""/#{0},namespaces"" target=""_top""><div class=""resultItem""><div class=""resultLine"">{0}</div></div></a>", assemblyName);
                sb.AppendLine();
            }

            sb.AppendLine(GetResultsHtmlSuffix(emitSolutionBrowserLink: true));

            File.WriteAllText(Path.Combine(solutionDestinationFolder, "results.html"), sb.ToString());
        }

        public static string GetResultsHtmlPrefix()
        {
            return @"<!DOCTYPE html><html><head><title>Results</title>
<link rel=""stylesheet"" href=""styles.css"" />
<script src=""scripts.js""></script>
</head>
<body onload=""onResultsLoad();"">
<div id=""symbols"">
<div class=""note"">
Enter a type or member name or <a href=""/#q=assembly%20"" target=""_top"" class=""blueLink"" onclick=""populateSearchBox('assembly '); return false;"">filter the assembly list</a>.
</div>
<div class=""resultGroup"">
";
        }

        public static string GetResultsHtmlSuffix(bool emitSolutionBrowserLink)
        {
            var solutionExplorerLink = emitSolutionBrowserLink
                ? @"<div class=""note"">Try also browsing the <a href=""solutionexplorer.html"" class=""blueLink"">solution explorer</a>.</div>"
                : null;

            return @"</div></div>" + solutionExplorerLink + @"</body></html>";
        }

        private static string partialTypeDisambiguationFileTemplate = @"<!DOCTYPE html>
<html><head><link rel=""stylesheet"" href=""{0}"">
</head><body><div class=""partialTypeHeader"">Partial Type</div>
{1}
</body></html>";

        public static void GeneratePartialTypeDisambiguationFile(
            string solutionDestinationFolder,
            string projectDestinationFolder,
            string symbolId,
            IEnumerable<string> filePaths)
        {
            string partialFolder = Path.Combine(projectDestinationFolder, Constants.PartialResolvingFileName);
            Directory.CreateDirectory(partialFolder);
            var disambiguationFileName = Path.Combine(partialFolder, symbolId) + ".html";
            string list = string.Join(Environment.NewLine,
                filePaths
                .OrderBy(filePath => Paths.StripExtension(filePath))
                .Select(filePath => "<a href=\"../" + filePath + ".html#" + symbolId + "\"><div class=\"partialTypeLink\">" + filePath + "</div></a>"));
            string content = string.Format(
                partialTypeDisambiguationFileTemplate,
                Paths.GetCssPathFromFile(solutionDestinationFolder, disambiguationFileName),
                list);
            File.WriteAllText(disambiguationFileName, content, Encoding.UTF8);
        }

        public static string EscapeSemicolons(string text)
        {
            text = text.Replace(';', ':');
            text = text.Replace('\r', ' ');
            text = text.Replace('\n', ' ');
            return text;
        }

        public static string A(string url, string displayText, string target = "")
        {
            if (!string.IsNullOrEmpty(target))
            {
                target = string.Format(" target=\"{0}\"", target);
            }
            else
            {
                target = "";
            }

            string result = string.Format("<a class=\"blueLink\" href=\"{0}\"{2}>{1}</a>", url, displayText, target);
            return result;
        }

        public static string Tag(string tag, string content, IEnumerable<KeyValuePair<string, string>> attributes = null)
        {
            var sb = new StringBuilder();

            sb.Append("<");
            sb.Append(tag);

            if (attributes != null && attributes.Any())
            {
                foreach (var kvp in attributes)
                {
                    sb.Append(" ");
                    sb.Append(kvp.Key);
                    sb.Append("=\"");
                    sb.Append(kvp.Value);
                    sb.Append("\"");
                }
            }

            sb.Append(">");
            sb.Append(content);
            sb.Append("</");
            sb.Append(tag);
            sb.Append(">");

            return sb.ToString();
        }

        public static void WriteSymbol(DeclaredSymbolInfo symbol, StringBuilder sb)
        {
            var url = symbol.GetUrl();
            sb.AppendFormat("<a href=\"{0}\" target=\"s\"><div class=\"resultItem\" onClick=\"resultClick(this);\">", url);
            sb.Append("<div class=\"resultLine\">");
            sb.AppendFormat("<img src=\"/content/icons/{0}\" height=\"16\" width=\"16\" />", GetGlyph(symbol) + ".png");
            sb.AppendFormat("<div class=\"resultKind\">{0}</div>", symbol.Kind);
            sb.AppendFormat("<div class=\"resultName\">{0}</div>", Markup.HtmlEscape(symbol.Name));
            sb.AppendLine("</div>");
            sb.AppendFormat("<div class=\"resultDescription\">{0}</div>", Markup.HtmlEscape(symbol.Description));
            sb.AppendLine();
            sb.AppendLine("</div></a>");
        }

        private static string GetGlyph(DeclaredSymbolInfo symbol)
        {
            var result = symbol.Glyph;
            if (result == 196)
            {
                return "CSharp";
            }
            else if (result == 195)
            {
                return "VB";
            }
            else if (result == 227)
            {
                return "xaml";
            }
            else if (result == 228)
            {
                return "TypeScript";
            }

            return result.ToString();
        }
    }
}
