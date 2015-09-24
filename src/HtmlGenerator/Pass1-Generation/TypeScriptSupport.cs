using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.SourceBrowser.Common;
using Newtonsoft.Json;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class TypeScriptSupport
    {
        private static readonly HashSet<string> alreadyProcessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<Reference>> references;
        private List<string> declarations;
        public Dictionary<string, List<Tuple<string, long>>> SymbolIDToListOfLocationsMap { get; private set; }
        public static readonly string ProjectDestinationFolder = Path.Combine(Paths.SolutionDestinationFolder, Constants.TypeScriptFiles);

        public void Generate(IEnumerable<string> typeScriptFiles)
        {
            if (typeScriptFiles == null || !typeScriptFiles.Any())
            {
                return;
            }

            var libdts = Path.Combine(Common.Paths.BaseAppFolder, "TypeScript", "lib.d.ts");

            declarations = new List<string>();
            references = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
            SymbolIDToListOfLocationsMap = new Dictionary<string, List<Tuple<string, long>>>();

            var list = new List<string>();

            if (!typeScriptFiles.Any(f => string.Equals(Path.GetFileName(f), "lib.d.ts", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(libdts);
            }

            foreach (var file in typeScriptFiles)
            {
                if (!alreadyProcessed.Contains(file))
                {
                    list.Add(file);
                }
            }

            GenerateCore(list);

            ProjectGenerator.GenerateReferencesDataFilesToAssembly(
                Paths.SolutionDestinationFolder,
                Constants.TypeScriptFiles,
                references);

            declarations.Sort();

            Serialization.WriteDeclaredSymbols(
                ProjectDestinationFolder,
                declarations);

            ProjectGenerator.GenerateSymbolIDToListOfDeclarationLocationsMap(
                ProjectDestinationFolder,
                SymbolIDToListOfLocationsMap);
        }

        private void GenerateCore(IEnumerable<string> typeScriptFiles)
        {
            var output = Path.Combine(Common.Paths.BaseAppFolder, "output");
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            var json = JsonConvert.SerializeObject(typeScriptFiles);
            var argumentsJson = Path.Combine(Common.Paths.BaseAppFolder, "TypeScriptAnalyzerArguments.json");
            File.WriteAllText(argumentsJson, json);

            var analyzerJs = Path.Combine(Common.Paths.BaseAppFolder, @"TypeScript\analyzer.js");

            var result = new ProcessLaunchService().RunAndRedirectOutput("Microsoft.SourceBrowser.TypeScriptAnalyzer.exe", argumentsJson);

            foreach (var file in Directory.GetFiles(output))
            {
                if (Path.GetFileNameWithoutExtension(file) == "ok")
                {
                    continue;
                }

                if (Path.GetFileNameWithoutExtension(file) == "error")
                {
                    var errorContent = File.ReadAllText(file);
                    Log.Exception(DateTime.Now.ToString() + " " + errorContent);
                    return;
                }

                var text = File.ReadAllText(file);
                AnalyzedFile analysis = JsonConvert.DeserializeObject<AnalyzedFile>(text);

                EnsureFileGeneratedAndGetUrl(analysis);
            }
        }

        public void EnsureFileGeneratedAndGetUrl(AnalyzedFile analysis)
        {
            string localFileSystemPath = analysis.fileName;
            localFileSystemPath = Path.GetFullPath(localFileSystemPath);

            string destinationFilePath = GetDestinationFilePath(localFileSystemPath);

            if (!File.Exists(destinationFilePath))
            {
                Generate(localFileSystemPath, destinationFilePath, analysis.syntacticClassifications, analysis.semanticClassifications);
            }
        }

        public static string GetDestinationFilePath(string sourceFilePath)
        {
            var url = sourceFilePath + ".html";
            url = url.Replace(":", "");
            url = url.Replace(" ", "");
            url = url.Replace(@"\bin\", @"\bin_\");
            if (url.StartsWith(@"\\"))
            {
                url = url.Substring(2);
            }

            url = Constants.TypeScriptFiles + @"\" + url;

            url = Path.Combine(Paths.SolutionDestinationFolder, url);
            return url;
        }

        private void Generate(
            string sourceFilePath,
            string destinationHtmlFilePath,
            ClassifiedRange[] syntacticRanges,
            ClassifiedRange[] semanticRanges)
        {
            Log.Write(destinationHtmlFilePath);
            var sb = new StringBuilder();

            var lines = File.ReadAllLines(sourceFilePath);
            var text = File.ReadAllText(sourceFilePath);
            var lineCount = lines.Length;
            var lineLengths = TextUtilities.GetLineLengths(text);

            var ranges = PrepareRanges(syntacticRanges, semanticRanges, text);

            var relativePathToRoot = Paths.CalculateRelativePathToRoot(destinationHtmlFilePath, Paths.SolutionDestinationFolder);

            var prefix = Markup.GetDocumentPrefix(Path.GetFileName(sourceFilePath), relativePathToRoot, lineCount, "ix");
            sb.Append(prefix);

            var displayName = GetDisplayName(destinationHtmlFilePath);
            var assemblyName = "TypeScriptFiles";

            var url = "/#" + assemblyName + "/" + displayName.Replace('\\', '/');
            displayName = @"\\" + displayName;

            var file = string.Format("File: <a id=\"filePath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a><br/>", url, displayName);
            var row = string.Format("<tr><td>{0}</td></tr>", file);
            Markup.WriteLinkPanel(s => sb.AppendLine(s), row);

            // pass a value larger than 0 to generate line numbers statically at HTML generation time
            var table = Markup.GetTablePrefix();
            sb.AppendLine(table);

            var localSymbolIdMap = new Dictionary<string, int>();

            foreach (var range in ranges)
            {
                range.lineNumber = TextUtilities.GetLineNumber(range.start, lineLengths);
                var line = TextUtilities.GetLineFromPosition(range.start, text);
                range.column = range.start - line.Item1;
                range.lineText = text.Substring(line.Item1, line.Item2);

                GenerateRange(sb, range, destinationHtmlFilePath, localSymbolIdMap);
            }

            var suffix = Markup.GetDocumentSuffix();
            sb.AppendLine(suffix);

            var folder = Path.GetDirectoryName(destinationHtmlFilePath);
            Directory.CreateDirectory(folder);
            File.WriteAllText(destinationHtmlFilePath, sb.ToString());
        }

        public static ClassifiedRange[] PrepareRanges(
            ClassifiedRange[] syntacticRanges,
            ClassifiedRange[] semanticRanges,
            string text)
        {
            foreach (var range in semanticRanges)
            {
                range.IsSemantic = true;
            }

            var rangesSortedByStart = syntacticRanges
                .Concat(semanticRanges)
                .Where(r => r.length > 0)
                .OrderBy(r => r.start)
                .ToArray();

            var midpoints = rangesSortedByStart
                .Select(r => r.start)
                .Concat(
                    rangesSortedByStart
                    .Select(r => r.end))
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            var ranges = RemoveIntersectingRanges(
                text,
                rangesSortedByStart,
                midpoints);

            ranges = RemoveOverlappingRanges(
                text,
                ranges);

            ranges = RangeUtilities.FillGaps(
                text,
                ranges,
                r => r.start,
                r => r.length,
                (s, l, t) => new ClassifiedRange(t, s, l));

            foreach (var range in ranges)
            {
                if (range.text == null)
                {
                    range.text = text.Substring(range.start, range.length);
                }
            }

            return ranges;
        }

        public static ClassifiedRange[] RemoveOverlappingRanges(string text, ClassifiedRange[] ranges)
        {
            var output = new List<ClassifiedRange>(ranges.Length);

            for (int i = 0; i < ranges.Length; i++)
            {
                ClassifiedRange best = ranges[i];
                while (i < ranges.Length - 1 && ranges[i].start == ranges[i + 1].start)
                {
                    best = ChooseBetterRange(best, ranges[i + 1]);
                    i++;
                }

                output.Add(best);
            }

            return output.ToArray();
        }

        private static ClassifiedRange ChooseBetterRange(ClassifiedRange left, ClassifiedRange right)
        {
            if (left == null)
            {
                return right;
            }

            if (right == null)
            {
                return left;
            }

            if (left.IsSemantic != right.IsSemantic)
            {
                if (left.IsSemantic)
                {
                    right.classification = left.classification;
                    return right;
                }
                else
                {
                    left.classification = right.classification;
                    return left;
                }
            }

            ClassifiedRange victim = left;
            ClassifiedRange winner = right;

            if (left.classification == "comment")
            {
                victim = left;
                winner = right;
            }

            if (right.classification == "comment")
            {
                victim = right;
                winner = left;
            }

            if (winner.hyperlinks == null && victim.hyperlinks != null)
            {
                winner.hyperlinks = victim.hyperlinks;
            }

            if (winner.classification == "text")
            {
                winner.classification = victim.classification;
            }

            return winner;
        }

        public static ClassifiedRange[] RemoveIntersectingRanges(
            string text,
            ClassifiedRange[] rangesSortedByStart,
            int[] midpoints)
        {
            var result = new List<ClassifiedRange>();

            int currentEndpoint = 0;
            int currentRangeIndex = 0;
            for (; currentEndpoint < midpoints.Length && currentRangeIndex < rangesSortedByStart.Length;)
            {
                while (
                    currentRangeIndex < rangesSortedByStart.Length &&
                    rangesSortedByStart[currentRangeIndex].start == midpoints[currentEndpoint])
                {
                    var currentRange = rangesSortedByStart[currentRangeIndex];
                    if (currentRange.end == midpoints[currentEndpoint + 1])
                    {
                        result.Add(currentRange);
                    }
                    else
                    {
                        int endpoint = currentEndpoint;
                        do
                        {
                            result.Add(
                                new ClassifiedRange(
                                    text,
                                    midpoints[endpoint],
                                    midpoints[endpoint + 1] - midpoints[endpoint],
                                    currentRange));
                            endpoint++;
                        }
                        while (endpoint < midpoints.Length && midpoints[endpoint] < currentRange.end);
                    }

                    currentRangeIndex++;
                }

                currentEndpoint++;
            }

            return result.ToArray();
        }

        private void GenerateRange(
            StringBuilder sb,
            ClassifiedRange range,
            string destinationFilePath,
            Dictionary<string, int> localSymbolIdMap)
        {
            var html = range.text;
            html = Markup.HtmlEscape(html);

            var localRelativePath = destinationFilePath.Substring(
                Path.Combine(
                    Paths.SolutionDestinationFolder,
                    Constants.TypeScriptFiles).Length + 1);
            localRelativePath = localRelativePath.Substring(0, localRelativePath.Length - ".html".Length);

            string classAttributeValue = GetSpanClass(range.classification);
            HtmlElementInfo hyperlinkInfo = GenerateLinks(range, destinationFilePath, localSymbolIdMap);

            if (hyperlinkInfo == null)
            {
                if (classAttributeValue == null)
                {
                    sb.Append(html);
                    return;
                }

                if (classAttributeValue == "k")
                {
                    sb.Append("<b>" + html + "</b>");
                    return;
                }
            }

            var elementName = "span";
            if (hyperlinkInfo != null)
            {
                elementName = hyperlinkInfo.Name;
            }

            sb.Append("<" + elementName);
            bool overridingClassAttributeSpecified = false;
            if (hyperlinkInfo != null)
            {
                foreach (var attribute in hyperlinkInfo.Attributes)
                {
                    const string typeScriptFilesR = "/TypeScriptFiles/R/";
                    if (attribute.Key == "href" && attribute.Value.StartsWith(typeScriptFilesR))
                    {
                        var streamPosition = sb.Length + 7 + typeScriptFilesR.Length; // exact offset into <a href="HERE
                        ProjectGenerator.AddDeclaredSymbolToRedirectMap(
                            SymbolIDToListOfLocationsMap,
                            attribute.Value.Substring(typeScriptFilesR.Length, 16),
                            localRelativePath,
                            streamPosition);
                    }

                    AddAttribute(sb, attribute.Key, attribute.Value);
                    if (attribute.Key == "class")
                    {
                        overridingClassAttributeSpecified = true;
                    }
                }
            }

            if (!overridingClassAttributeSpecified)
            {
                AddAttribute(sb, "class", classAttributeValue);
            }

            sb.Append('>');

            sb.Append(html);
            sb.Append("</" + elementName + ">");
        }

        private void AddAttribute(StringBuilder sb, string name, string value)
        {
            if (value != null)
            {
                sb.Append(" " + name + "=\"" + value + "\"");
            }
        }

        private int GetLocalId(string symbolId, Dictionary<string, int> localSymbolIdMap)
        {
            int localId = 0;
            if (!localSymbolIdMap.TryGetValue(symbolId, out localId))
            {
                localId = localSymbolIdMap.Count;
                localSymbolIdMap.Add(symbolId, localId);
            }

            return localId;
        }

        private HtmlElementInfo GenerateLinks(ClassifiedRange range, string destinationHtmlFilePath, Dictionary<string, int> localSymbolIdMap)
        {
            HtmlElementInfo result = null;

            var localRelativePath = destinationHtmlFilePath.Substring(
                Path.Combine(
                    Paths.SolutionDestinationFolder,
                    Constants.TypeScriptFiles).Length);

            if (!string.IsNullOrEmpty(range.definitionSymbolId))
            {
                var definitionSymbolId = SymbolIdService.GetId(range.definitionSymbolId);

                if (range.IsSymbolLocalOnly())
                {
                    var localId = GetLocalId(definitionSymbolId, localSymbolIdMap);
                    result = new HtmlElementInfo
                    {
                        Name = "span",
                        Attributes =
                        {
                            { "id", "r" + localId + " rd" },
                            { "class", "r" + localId + " r" }
                        }
                    };
                    return result;
                }

                result = new HtmlElementInfo
                {
                    Name = "a",
                    Attributes =
                    {
                        { "id", definitionSymbolId },
                        { "href", "/TypeScriptFiles/" + Constants.ReferencesFileName + "/" + definitionSymbolId + ".html" },
                        { "target", "n" }
                    }
                };

                var searchString = range.searchString;
                if (!string.IsNullOrEmpty(searchString) && searchString.Length > 2)
                {
                    lock (declarations)
                    {
                        searchString = searchString.StripQuotes();
                        if (IsWellFormed(searchString))
                        {
                            var declaration = string.Join(";",
                                searchString, // symbol name
                                definitionSymbolId, // 8-byte symbol ID
                                range.definitionKind, // kind (e.g. "class")
                                Markup.EscapeSemicolons(range.fullName), // symbol full name and signature
                                GetGlyph(range.definitionKind) // glyph number
                            );
                            declarations.Add(declaration);
                        }
                    }
                }
            }

            if (range.hyperlinks == null || range.hyperlinks.Length == 0)
            {
                return result;
            }

            var hyperlink = range.hyperlinks[0];
            var symbolId = SymbolIdService.GetId(hyperlink.symbolId);

            if (range.IsSymbolLocalOnly() || localSymbolIdMap.ContainsKey(symbolId))
            {
                var localId = GetLocalId(symbolId, localSymbolIdMap);
                result = new HtmlElementInfo
                {
                    Name = "span",
                    Attributes =
                    {
                        { "class", "r" + localId + " r" }
                    }
                };
                return result;
            }

            var hyperlinkDestinationFile = Path.GetFullPath(hyperlink.sourceFile);
            hyperlinkDestinationFile = GetDestinationFilePath(hyperlinkDestinationFile);

            string href = "";
            if (!string.Equals(hyperlinkDestinationFile, destinationHtmlFilePath, StringComparison.OrdinalIgnoreCase))
            {
                href = Paths.MakeRelativeToFile(hyperlinkDestinationFile, destinationHtmlFilePath);
                href = href.Replace('\\', '/');
            }

            href = href + "#" + symbolId;

            if (result == null)
            {
                result = new HtmlElementInfo
                {
                    Name = "a",
                    Attributes =
                    {
                        { "href", href },
                        { "target", "s" },
                    }
                };
            }
            else if (!result.Attributes.ContainsKey("href"))
            {
                result.Attributes.Add("href", href);
                result.Attributes.Add("target", "s");
            }

            lock (this.references)
            {
                var lineNumber = range.lineNumber + 1;
                var linkToReference = ".." + localRelativePath + "#" + lineNumber.ToString();
                var start = range.column;
                var end = range.column + range.text.Length;
                var lineText = Markup.HtmlEscape(range.lineText, ref start, ref end);
                var reference = new Reference
                {
                    FromAssemblyId = Constants.TypeScriptFiles,
                    ToAssemblyId = Constants.TypeScriptFiles,
                    FromLocalPath = localRelativePath.Substring(0, localRelativePath.Length - ".html".Length).Replace('\\', '/'),
                    Kind = ReferenceKind.Reference,
                    ToSymbolId = symbolId,
                    ToSymbolName = range.text,
                    ReferenceLineNumber = lineNumber,
                    ReferenceLineText = lineText,
                    ReferenceColumnStart = start,
                    ReferenceColumnEnd = end,
                    Url = linkToReference.Replace('\\', '/')
                };

                List<Reference> bucket = null;
                if (!references.TryGetValue(symbolId, out bucket))
                {
                    bucket = new List<Reference>();
                    references.Add(symbolId, bucket);
                }

                bucket.Add(reference);
            }

            return result;
        }

        private bool IsWellFormed(string searchString)
        {
            return searchString.Length > 2
                && !searchString.Contains(";")
                && !searchString.Contains("\n");
        }

        private string GetGlyph(string definitionKind)
        {
            switch (definitionKind)
            {
                case "variable":
                    return "42";
                case "function":
                    return "72";
                case "parameter":
                    return "42";
                case "interface":
                    return "48";
                case "property":
                    return "102";
                case "method":
                    return "72";
                case "type parameter":
                    return "114";
                case "module":
                    return "150";
                case "class":
                    return "0";
                case "constructor":
                    return "72";
                case "enum":
                    return "18";
                case "enum member":
                    return "24";
                case "import":
                    return "6";
                case "get accessor":
                    return "72";
                case "set accessor":
                    return "72";
                default:
                    return "195";
            }
        }

        private string GetSpanClass(string classification)
        {
            if (classification == "keyword")
            {
                return "k";
            }
            else if (classification == "comment")
            {
                return "c";
            }
            else if (classification == "string")
            {
                return "s";
            }
            else if (classification == "class name")
            {
                return "t";
            }
            else if (classification == "enum name")
            {
                return "t";
            }
            else if (classification == "interface name" || classification == "type alias name")
            {
                return "t";
            }
            else if (classification == "type parameter name")
            {
                return "t";
            }
            else if (classification == "module name")
            {
                return "t";
            }

            return null;
        }

        private string GetDisplayName(string destinationHtmlFilePath)
        {
            var result = Path.GetFileNameWithoutExtension(destinationHtmlFilePath);
            var lengthOfPrefixToTrim = Paths.SolutionDestinationFolder.Length + Constants.TypeScriptFiles.Length + 2;
            result = destinationHtmlFilePath.Substring(lengthOfPrefixToTrim, destinationHtmlFilePath.Length - lengthOfPrefixToTrim - 5); // strip ".html"

            return result;
        }
    }
}
