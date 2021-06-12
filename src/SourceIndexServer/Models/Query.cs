using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class Query
    {
        private readonly StringBuilder diagnostics = new StringBuilder();

        public readonly List<string> Paths = new List<string>();
        public readonly List<string> DotSeparatedNames = new List<string>();
        public readonly List<string> Names = new List<string>();
        public readonly List<string> SymbolKinds = new List<string>();

        public List<Interpretation> Interpretations { get; set; }
        public int PotentialRawResults { get; set; }

        public List<DeclaredSymbolInfo> ResultSymbols { get; private set; }
        public List<AssemblyInfo> ResultAssemblies { get; private set; }
        public List<AssemblyInfo> ResultProjects { get; private set; }
        public List<string> ResultGuids { get; private set; }
        public List<string> ResultMSBuildProperties { get; private set; }
        public List<string> ResultMSBuildItems { get; private set; }
        public List<string> ResultMSBuildTargets { get; private set; }
        public List<string> ResultMSBuildTasks { get; private set; }

        private readonly HashSet<DeclaredSymbolInfo> resultSymbolsSet = new HashSet<DeclaredSymbolInfo>();
        private static readonly string[] supportedFileExtensions =
        {
            "cs",
            "vb",
            "ts",
            "csproj",
            "vbproj",
            "targets",
            "props",
            "xaml",
            "xml",
            "resx"
        };

        public string OriginalString { get; set; }

        public Query(string queryString)
        {
            ResultSymbols = new List<DeclaredSymbolInfo>();
            ResultAssemblies = new List<AssemblyInfo>();
            ResultProjects = new List<AssemblyInfo>();
            Interpretations = new List<Interpretation>();

            OriginalString = queryString;
            Parse(queryString);
        }

        /// <summary>
        /// This constructor is to construct an erroneous query with diagnostics
        /// </summary>
        private Query()
        {
        }

        private void Parse(string queryString)
        {
            var terms = queryString.SplitBySpacesConsideringQuotes();
            foreach (var term in terms)
            {
                Analyze(term);
            }

            BuildInterpretations();
        }

        private void Analyze(string term)
        {
            if (SymbolKindText.IsKnown(term))
            {
                this.SymbolKinds.Add(term);
                return;
            }

            int dot = term.IndexOf('.');
            int backslash = term.IndexOf('\\');

            if (backslash >= 0 && !this.Paths.Contains(term))
            {
                this.Paths.Add(term);
            }
            else if (dot >= 0 && !this.DotSeparatedNames.Contains(term))
            {
                this.DotSeparatedNames.Add(term);
            }
            else if (!this.Names.Contains(term))
            {
                this.Names.Add(term);
            }
        }

        public bool IsAssemblySearch()
        {
            return SymbolKinds.Contains(SymbolKindText.Assembly);
        }

        private void BuildInterpretations()
        {
            foreach (var name in this.Names.Where(n => n.Length >= 3))
            {
                string text = name;
                var interpretation = new Interpretation();
                bool isQuoted = false;
                text = StripQuotes(text, out isQuoted);
                interpretation.CoreSearchTerm = text;
                interpretation.IsVerbatim = isQuoted;
                foreach (var otherName in this.Names.Where(n => n != text))
                {
                    interpretation.FilterNames.Add(otherName.StripQuotes());
                }

                foreach (var dottedName in this.DotSeparatedNames)
                {
                    interpretation.FilterDotSeparatedNames.Add(dottedName.StripQuotes());
                }

                this.Interpretations.Add(interpretation);
                AddPossibleInterpretationWithoutClrPrefix(interpretation);
            }

            foreach (var dottedName in this.DotSeparatedNames)
            {
                bool isQuoted = false;
                string dottedNameText = StripQuotes(dottedName, out isQuoted);
                var lastPart = GetLastPart(dottedNameText);

                // is it a file name?
                var lastPartLower = lastPart.ToLowerInvariant();
                if (supportedFileExtensions.Any(extension => extension.StartsWith(lastPartLower, StringComparison.Ordinal)))
                {
                    var fileInterpretation = new Interpretation();
                    fileInterpretation.CoreSearchTerm = dottedNameText;
                    fileInterpretation.IsVerbatim = isQuoted;
                    this.Interpretations.Add(fileInterpretation);
                    continue;
                }

                var interpretation = new Interpretation();
                interpretation.CoreSearchTerm = lastPart;
                interpretation.IsVerbatim = isQuoted;
                interpretation.Namespace = dottedNameText;
                foreach (var name in this.Names)
                {
                    interpretation.FilterNames.Add(name.StripQuotes());
                }

                foreach (var otherDottedName in this.DotSeparatedNames.Where(i => i != dottedNameText))
                {
                    interpretation.FilterDotSeparatedNames.Add(otherDottedName.StripQuotes());
                }

                this.Interpretations.Add(interpretation);
                AddPossibleInterpretationWithoutClrPrefix(interpretation);
            }
        }

        private void AddPossibleInterpretationWithoutClrPrefix(Interpretation interpretation)
        {
            var maybeWithoutGetOrSet = InterpretWithoutGetOrSet(interpretation);
            if (maybeWithoutGetOrSet != null)
            {
                this.Interpretations.Add(maybeWithoutGetOrSet);
            }
        }

        // make Schabse happy
        private static readonly string[] prefixes =
        {
            "get_",
            "set_",
            "add_",
            "remove_"
        };

        private Interpretation InterpretWithoutGetOrSet(Interpretation interpretation)
        {
            if (string.IsNullOrEmpty(interpretation.CoreSearchTerm))
            {
                return null;
            }

            foreach (var prefix in prefixes)
            {
                if (interpretation.CoreSearchTerm.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var clone = interpretation.Clone();
                    clone.CoreSearchTerm = clone.CoreSearchTerm.Substring(prefix.Length);
                    if (clone.Namespace != null)
                    {
                        clone.Namespace = clone.Namespace.Replace(prefix, "");
                    }

                    return clone;
                }
            }

            return null;
        }

        public static string StripQuotes(string text, out bool isQuoted)
        {
            isQuoted = false;

            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal) && text.Length > 1)
            {
                text = text.Substring(1, text.Length - 2);
                isQuoted = true;
            }

            return text;
        }

        public bool Filter(DeclaredSymbolInfo symbol)
        {
            return
                FilterSymbolKinds(symbol) &&
                FilterProjects(symbol);
        }

        private bool FilterSymbolKinds(DeclaredSymbolInfo symbol)
        {
            if (!this.SymbolKinds.Any())
            {
                return true;
            }

            foreach (var symbolKind in this.SymbolKinds)
            {
                if (symbol.Kind == symbolKind)
                {
                    return true;
                }

                if (symbolKind == SymbolKindText.Type && SymbolKindText.IsType(symbol.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        private bool FilterProjects(DeclaredSymbolInfo symbol)
        {
            if (!this.Paths.Any())
            {
                return true;
            }

            foreach (var path in this.Paths)
            {
                if (symbol.ProjectFilePath == null || symbol.ProjectFilePath.IndexOf(path, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    return false;
                }
            }

            return true;
        }

        public void AddResultGuids(List<string> result)
        {
            this.ResultGuids = result;
        }

        public void AddResultMSBuildProperties(List<string> result)
        {
            this.ResultMSBuildProperties = result;
        }

        public void AddResultMSBuildItems(List<string> result)
        {
            this.ResultMSBuildItems = result;
        }

        public void AddResultMSBuildTargets(List<string> result)
        {
            this.ResultMSBuildTargets = result;
        }

        public void AddResultMSBuildTasks(List<string> result)
        {
            this.ResultMSBuildTasks = result;
        }

        public void AddDiagnostic(string message)
        {
            diagnostics.AppendLine(message);
        }

        public void AddResultSymbols(List<DeclaredSymbolInfo> matches)
        {
            foreach (var match in matches)
            {
                if (!resultSymbolsSet.Contains(match))
                {
                    this.ResultSymbols.Add(match);
                }
            }
        }

        public void AddResultAssemblies(IEnumerable<AssemblyInfo> matches)
        {
            this.ResultAssemblies.AddRange(matches);
        }

        public void AddResultProjects(List<AssemblyInfo> matches)
        {
            this.ResultProjects.AddRange(matches);
        }

        public static Query Empty(string message)
        {
            var result = new Query();
            result.AddDiagnostic(message);
            return result;
        }

        public bool HasResults
        {
            get
            {
                return
                    (ResultSymbols != null && ResultSymbols.Any()) ||
                    (ResultAssemblies != null && ResultAssemblies.Any()) ||
                    (ResultProjects != null && ResultProjects.Any()) ||
                    (ResultGuids != null && ResultGuids.Any()) ||
                    (ResultMSBuildProperties != null && ResultMSBuildProperties.Any()) ||
                    (ResultMSBuildItems != null && ResultMSBuildItems.Any()) ||
                    (ResultMSBuildTargets != null && ResultMSBuildTargets.Any()) ||
                    (ResultMSBuildTasks != null && ResultMSBuildTasks.Any());
            }
        }

        public string Diagnostics
        {
            get
            {
                return diagnostics.ToString();
            }
        }

        public bool HasDiagnostics
        {
            get
            {
                return diagnostics.Length > 0;
            }
        }

        public string GetSearchTermForSymbolSearch()
        {
            string searchTerm = null;
            if (!this.Names.Any())
            {
                if (this.DotSeparatedNames.Any())
                {
                    var assemblyName = this.DotSeparatedNames[0];
                    searchTerm = GetLastPart(assemblyName);
                }
                else
                {
                    searchTerm = null;
                }
            }
            else
            {
                searchTerm = this.Names[0];
            }

            if (searchTerm != null && searchTerm.Length < 3)
            {
                searchTerm = null;
            }

            return searchTerm;
        }

        private static string GetLastPart(string dotSeparatedName)
        {
            var dotParts = dotSeparatedName.Split('.');
            var result = dotParts[dotParts.Length - 1];
            return result;
        }

        public string GetSearchTermForAssemblySearch()
        {
            string assemblyName = null;
            if (this.DotSeparatedNames.Any())
            {
                assemblyName = this.DotSeparatedNames[0];
            }
            else if (this.Names.Any())
            {
                assemblyName = this.Names[0];
            }

            return assemblyName;
        }

        public string GetSearchTermForProjectSearch()
        {
            string result = null;
            if (this.Paths.Any())
            {
                result = this.Paths[0];
            }

            return result;
        }

        public string GetSearchTermForMSBuildSearch()
        {
            string result = null;
            if (this.Names.Any())
            {
                result = this.Names[0];
            }

            return result;
        }
    }
}