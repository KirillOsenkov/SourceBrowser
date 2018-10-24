using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        public readonly Dictionary<string, Dictionary<string, List<Reference>>> ReferencesByTargetAssemblyAndSymbolId =
            new Dictionary<string, Dictionary<string, List<Reference>>>();

        public IEnumerable<string> UsedReferences { get; private set; }

        public void AddReference(
            string documentDestinationPath,
            SourceText referenceText,
            string destinationAssemblyName,
            ISymbol symbol,
            string symbolId,
            int startPosition,
            int endPosition,
            ReferenceKind kind)
        {
            string referenceString = referenceText.ToString(TextSpan.FromBounds(startPosition, endPosition));
            if (symbol is INamedTypeSymbol && (referenceString == "this" || referenceString == "base"))
            {
                // Don't count "this" or "base" expressions that bind to this type as references
                return;
            }

            var line = referenceText.Lines.GetLineFromPosition(startPosition);
            int start = referenceText.Lines.GetLinePosition(startPosition).Character;
            int end = start + endPosition - startPosition;
            int lineNumber = line.LineNumber + 1;
            string lineText = line.ToString();

            AddReference(
                documentDestinationPath,
                lineText,
                start,
                referenceString.Length,
                lineNumber,
                AssemblyName,
                destinationAssemblyName,
                symbol,
                symbolId,
                kind);
        }

        public void AddReference(
            string documentDestinationPath,
            string lineText,
            int referenceStartOnLine,
            int referenceLength,
            int lineNumber,
            string fromAssemblyName,
            string toAssemblyName,
            ISymbol symbol,
            string symbolId,
            ReferenceKind kind)
        {
            string localPath = Paths.MakeRelativeToFolder(
                documentDestinationPath,
                Path.Combine(SolutionGenerator.SolutionDestinationFolder, fromAssemblyName));
            localPath = Path.ChangeExtension(localPath, null);

            int referenceEndOnLine = referenceStartOnLine + referenceLength;

            lineText = Markup.HtmlEscape(lineText, ref referenceStartOnLine, ref referenceEndOnLine);

            string symbolName = GetSymbolName(symbol, symbolId);

            var reference = new Reference()
            {
                ToAssemblyId = toAssemblyName,
                ToSymbolId = symbolId,
                ToSymbolName = symbolName,
                FromAssemblyId = fromAssemblyName,
                FromLocalPath = localPath,
                ReferenceLineText = lineText,
                ReferenceLineNumber = lineNumber,
                ReferenceColumnStart = referenceStartOnLine,
                ReferenceColumnEnd = referenceEndOnLine,
                Kind = kind
            };

            if (referenceStartOnLine < 0 ||
                referenceStartOnLine >= referenceEndOnLine ||
                referenceEndOnLine > lineText.Length)
            {
                Log.Exception(
                    string.Format("AddReference: start = {0}, end = {1}, lineText = {2}, documentDestinationPath = {3}",
                    referenceStartOnLine,
                    referenceEndOnLine,
                    lineText,
                    documentDestinationPath));
            }

            string linkRelativePath = GetLinkRelativePath(reference);

            reference.Url = linkRelativePath;

            Dictionary<string, List<Reference>> referencesToAssembly = GetReferencesToAssembly(reference.ToAssemblyId);
            List<Reference> referencesToSymbol = GetReferencesToSymbol(reference, referencesToAssembly);
            lock (referencesToSymbol)
            {
                referencesToSymbol.Add(reference);
            }
        }

        private static List<Reference> GetReferencesToSymbol(Reference reference, Dictionary<string, List<Reference>> referencesToAssembly)
        {
            List<Reference> referencesToSymbol;
            lock (referencesToAssembly)
            {
                if (!referencesToAssembly.TryGetValue(reference.ToSymbolId, out referencesToSymbol))
                {
                    referencesToSymbol = new List<Reference>();
                    referencesToAssembly.Add(reference.ToSymbolId, referencesToSymbol);
                }
            }

            return referencesToSymbol;
        }

        private Dictionary<string, List<Reference>> GetReferencesToAssembly(string assembly)
        {
            Dictionary<string, List<Reference>> referencesToAssembly;
            lock (ReferencesByTargetAssemblyAndSymbolId)
            {
                if (!ReferencesByTargetAssemblyAndSymbolId.TryGetValue(assembly, out referencesToAssembly))
                {
                    referencesToAssembly = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
                    ReferencesByTargetAssemblyAndSymbolId.Add(assembly, referencesToAssembly);
                }
            }

            return referencesToAssembly;
        }

        private static string GetLinkRelativePath(Reference reference)
        {
            string linkRelativePath = reference.FromLocalPath.Replace('\\', '/') + ".html#" + reference.ReferenceLineNumber;
            if (reference.ToAssemblyId == reference.FromAssemblyId)
            {
                linkRelativePath = "../" + linkRelativePath;
            }
            else
            {
                linkRelativePath = "../../" + reference.FromAssemblyId + "/" + linkRelativePath;
            }

            return linkRelativePath;
        }

        private static string GetSymbolName(ISymbol symbol, string symbolId)
        {
            string symbolName = null;
            if (symbol != null)
            {
                symbolName = SymbolIdService.GetName(symbol);
                if (symbolName == ".ctor")
                {
                    symbolName = SymbolIdService.GetName(symbol.ContainingType) + " .ctor";
                }
            }
            else
            {
                symbolName = symbolId;
            }

            return symbolName;
        }

        private void GenerateUsedReferencedAssemblyList()
        {
            this.UsedReferences = ReferencesByTargetAssemblyAndSymbolId
                .Select(r => r.Key)
                .Where(a =>
                    a != AssemblyName &&
                    a != Constants.MSBuildPropertiesAssembly &&
                    a != Constants.MSBuildItemsAssembly &&
                    a != Constants.MSBuildTargetsAssembly &&
                    a != Constants.MSBuildTasksAssembly &&
                    a != Constants.GuidAssembly);
            File.WriteAllLines(Path.Combine(ProjectDestinationFolder, Constants.UsedReferencedAssemblyList + ".txt"), this.UsedReferences);
        }

        private void GenerateReferencedAssemblyList()
        {
            Log.Write("Referenced assembly list...");
            var index = Path.Combine(ProjectDestinationFolder, Constants.ReferencedAssemblyList + ".txt");
            var list = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var projectReference in Project.ProjectReferences.OrderBy(p => Project.Solution.GetProject(p.ProjectId).AssemblyName))
            {
                list.Add(Project.Solution.GetProject(projectReference.ProjectId).AssemblyName);
            }

            foreach (var metadataReference in Project.MetadataReferences.OrderBy(m => Path.GetFileNameWithoutExtension(m.Display)))
            {
                list.Add(Path.GetFileNameWithoutExtension(metadataReference.Display));
            }

            File.WriteAllText(index, string.Join(Environment.NewLine, list));
        }

        public static void GenerateReferencesDataFiles(
            string solutionDestinationFolder,
            Dictionary<string, Dictionary<string, List<Reference>>> referencesByTargetAssemblyAndSymbolId)
        {
            Log.Write("References data files...", ConsoleColor.White);

            foreach (var referencesToAssembly in referencesByTargetAssemblyAndSymbolId)
            {
                GenerateReferencesDataFilesToAssembly(
                    solutionDestinationFolder,
                    referencesToAssembly.Key,
                    referencesToAssembly.Value);
            }
        }

        public static void GenerateReferencesDataFilesToAssembly(
            string solutionDestinationFolder,
            string toAssemblyId,
            Dictionary<string, List<Reference>> referencesToAssembly)
        {
            var assemblyReferencesDataFolder = Path.Combine(
                solutionDestinationFolder,
                toAssemblyId,
                Constants.ReferencesFileName);
            Directory.CreateDirectory(assemblyReferencesDataFolder);

            Parallel.ForEach(
                referencesToAssembly,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                referencesToSymbol =>
                    {
                        try
                        {
                            var linkDataFile = Path.Combine(assemblyReferencesDataFolder, referencesToSymbol.Key + ".txt");
                            WriteSymbolReferencesToFile(referencesToSymbol.Value, linkDataFile);
                        }
                        catch (ArgumentException ex)
                        {
                            Log.Exception("ArgumentException in References.Pass1.cs, line 236: " + ex.ToString() + "\r\n\r\n" + "assemblyReferencesDataFolder: " + assemblyReferencesDataFolder + "   referencesToSymbol.Key: " + referencesToSymbol.Key);
                        }
                    });
        }

        public static void WriteSymbolReferencesToFile(IEnumerable<Reference> referencesToSymbol, string linkDataFile)
        {
            using (var writer = new StreamWriter(linkDataFile, append: true, encoding: Encoding.UTF8))
            {
                foreach (var reference in referencesToSymbol)
                {
                    reference.WriteTo(writer);
                }
            }
        }
    }
}
