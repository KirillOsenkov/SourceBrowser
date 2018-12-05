using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        public void CreateReferencesFiles()
        {
            BackpatchUnreferencedDeclarations(referencesFolder);
            Markup.WriteRedirectFile(ProjectDestinationFolder);
            GenerateFinalReferencesFiles(referencesFolder);
        }

        public void GenerateFinalReferencesFiles(string referencesFolder)
        {
            var files = Directory.GetFiles(referencesFolder, "*.txt");
            if (files.Length == 0)
            {
                return;
            }

            Log.Write("Creating references files for " + this.AssemblyId);
            Parallel.ForEach(
                files,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                referencesFile =>
            {
                try
                {
                    GenerateReferencesFile(referencesFile);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "Failed to generate references file for: " + referencesFile);
                }
            });
        }

        private void GenerateReferencesFile(string referencesFile)
        {
            string[] referencesLines = File.ReadAllLines(referencesFile, Encoding.UTF8);
            string rawReferencesFile = referencesFile;
            referencesFile = Path.ChangeExtension(referencesFile, ".html");

            var referenceKindGroups = CreateReferences(referencesLines, out int totalReferenceCount, out string symbolName);

            using (var writer = new StreamWriter(referencesFile, append: false, encoding: Encoding.UTF8))
            {
                Markup.WriteReferencesFileHeader(writer, symbolName);

                if (this.AssemblyId != Constants.MSBuildItemsAssembly &&
                    this.AssemblyId != Constants.MSBuildPropertiesAssembly &&
                    this.AssemblyId != Constants.MSBuildTargetsAssembly &&
                    this.AssemblyId != Constants.MSBuildTasksAssembly &&
                    this.AssemblyId != Constants.GuidAssembly)
                {
                    string symbolId = Path.GetFileNameWithoutExtension(referencesFile);
                    var id = Serialization.HexStringToULong(symbolId);
                    WriteBaseMember(id, writer);
                    WriteImplementedInterfaceMembers(id, writer);
                }

                foreach (var referenceKind in referenceKindGroups.OrderBy(t => (int)t.Item1))
                {
                    string formatString = "";

                    switch (referenceKind.Item1)
                    {
                        case ReferenceKind.Reference:
                            formatString = "{0} reference{1} to {2}";
                            break;
                        case ReferenceKind.DerivedType:
                            formatString = "{0} type{1} derived from {2}";
                            break;
                        case ReferenceKind.InterfaceInheritance:
                            formatString = "{0} interface{1} inheriting from {2}";
                            break;
                        case ReferenceKind.InterfaceImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Read:
                            formatString = "{0} read{1} of {2}";
                            break;
                        case ReferenceKind.Write:
                            formatString = "{0} write{1} to {2}";
                            break;
                        case ReferenceKind.Instantiation:
                            formatString = "{0} instantiation{1} of {2}";
                            break;
                        case ReferenceKind.Override:
                            formatString = "{0} override{1} of {2}";
                            break;
                        case ReferenceKind.InterfaceMemberImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.GuidUsage:
                            formatString = "{0} usage{1} of Guid {2}";
                            break;
                        case ReferenceKind.EmptyArrayAllocation:
                            formatString = "{0} allocation{1} of empty arrays";
                            break;
                        case ReferenceKind.MSBuildPropertyAssignment:
                            formatString = "{0} assignment{1} to MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildPropertyUsage:
                            formatString = "{0} usage{1} of MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildItemAssignment:
                            formatString = "{0} assignment{1} to MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildItemUsage:
                            formatString = "{0} usage{1} of MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildTargetDeclaration:
                            formatString = "{0} declaration{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTargetUsage:
                            formatString = "{0} usage{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTaskDeclaration:
                            formatString = "{0} import{1} of MSBuild task {2}";
                            break;
                        case ReferenceKind.MSBuildTaskUsage:
                            formatString = "{0} call{1} to MSBuild task {2}";
                            break;
                        default:
                            throw new NotImplementedException("Missing case for " + referenceKind.Item1);
                    }

                    var referencesOfSameKind = referenceKind.Item2.OrderBy(g => g.Item1);
                    totalReferenceCount = CountItems(referenceKind);
                    string headerText = string.Format(
                        formatString,
                        totalReferenceCount,
                        totalReferenceCount == 1 ? "" : "s",
                        symbolName);

                    Write(writer, string.Format(@"<div class=""rH"">{0}</div>", headerText));

                    foreach (var sameAssemblyReferencesGroup in referencesOfSameKind)
                    {
                        string assemblyName = sameAssemblyReferencesGroup.Item1;
                        Write(writer, "<div class=\"rA\">{0} ({1})</div>", assemblyName, CountItems(sameAssemblyReferencesGroup));
                        Write(writer, "<div class=\"rG\" id=\"{0}\">", assemblyName);

                        foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Item2.OrderBy(g => g.Item1))
                        {
                            Write(writer, "<div class=\"rF\">");
                            WriteLine(writer, "<div class=\"rN\">{0} ({1})</div>", sameFileReferencesGroup.Item1, CountItems(sameFileReferencesGroup));

                            foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Item2)
                            {
                                var url = sameLineReferencesGroup.First().Url;
                                Write(writer, "<a href=\"{0}\">", url);

                                Write(writer, "<b>{0}</b>", sameLineReferencesGroup.Key);
                                MergeOccurrences(writer, sameLineReferencesGroup);
                                WriteLine(writer, "</a>");
                            }

                            WriteLine(writer, "</div>");
                        }

                        WriteLine(writer, "</div>");
                    }
                }

                Write(writer, "</body></html>");
            }

            File.Delete(rawReferencesFile);
        }

        private void WriteImplementedInterfaceMembers(ulong symbolId, StreamWriter writer)
        {
            if (!ImplementedInterfaceMembers.TryGetValue(symbolId, out HashSet<Tuple<string, ulong>> implementedInterfaceMembers))
            {
                return;
            }

            Write(writer, string.Format(@"<div class=""rH"">Implemented interface member{0}:</div>", implementedInterfaceMembers.Count > 1 ? "s" : ""));

            foreach (var implementedInterfaceMember in implementedInterfaceMembers)
            {
                var assemblyName = implementedInterfaceMember.Item1;
                var interfaceSymbolId = implementedInterfaceMember.Item2;

                if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out ProjectFinalizer baseProject))
                {
                    return;
                }

                if (baseProject.DeclaredSymbols.TryGetValue(interfaceSymbolId, out DeclaredSymbolInfo symbol))
                {
                    var sb = new StringBuilder();
                    Markup.WriteSymbol(symbol, sb);
                    writer.Write(sb.ToString());
                }
            }
        }

        private void WriteBaseMember(ulong symbolId, StreamWriter writer)
        {
            if (!BaseMembers.TryGetValue(symbolId, out Tuple<string, ulong> baseMemberLink))
            {
                return;
            }

            Write(writer, @"<div class=""rH"">Base:</div>");

            var assemblyName = baseMemberLink.Item1;
            var baseSymbolId = baseMemberLink.Item2;

            if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out ProjectFinalizer baseProject))
            {
                return;
            }

            if (baseProject.DeclaredSymbols.TryGetValue(baseSymbolId, out DeclaredSymbolInfo symbol))
            {
                var sb = new StringBuilder();
                Markup.WriteSymbol(symbol, sb);
                writer.Write(sb.ToString());
            }
        }

        private static int CountItems(Tuple<string, IEnumerable<IGrouping<int, Reference>>> sameFileReferencesGroup)
        {
            int count = 0;

            foreach (var line in sameFileReferencesGroup.Item2)
            {
                foreach (var occurrence in line)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountItems(
            Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>> resultsInAssembly)
        {
            int count = 0;
            foreach (var file in resultsInAssembly.Item2)
            {
                count += CountItems(file);
            }

            return count;
        }

        private static int CountItems(
            Tuple<ReferenceKind, IEnumerable<Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>>>> results)
        {
            int count = 0;
            foreach (var item in results.Item2)
            {
                count += CountItems(item);
            }

            return count;
        }

        private static
            IEnumerable<Tuple<ReferenceKind,
                IEnumerable<Tuple<string,
                    IEnumerable<Tuple<string,
                        IEnumerable<IGrouping<int, Reference>>
                    >>
                >>
            >> CreateReferences(
            string[] referencesLines,
            out int totalReferenceCount,
            out string referencedSymbolName)
        {
            totalReferenceCount = 0;
            referencedSymbolName = null;

            var list = new List<Reference>(referencesLines.Length / 2);

            for (int i = 0; i < referencesLines.Length; i += 2)
            {
                var reference = new Reference(referencesLines[i], referencesLines[i + 1]);
                list.Add(reference);
                totalReferenceCount++;
                if (referencedSymbolName == null &&
                    reference.ToSymbolName != "this" &&
                    reference.ToSymbolName != "base" &&
                    reference.ToSymbolName != "var" &&
                    reference.ToSymbolName != "UsingTask" &&
                    reference.ToSymbolName != "[")
                {
                    referencedSymbolName = reference.ToSymbolName;
                }
            }

            var result = list.GroupBy
            (
                r0 => r0.Kind,
                (kind, referencesOfSameKind) => Tuple.Create
                (
                    kind,
                    referencesOfSameKind.GroupBy
                    (
                        r1 => r1.FromAssemblyId,
                        (assemblyName, referencesInSameAssembly) => Tuple.Create
                        (
                            assemblyName,
                            referencesInSameAssembly.GroupBy
                            (
                                r2 => r2.FromLocalPath,
                                (filePath, referencesInSameFile) => Tuple.Create
                                (
                                    filePath,
                                    referencesInSameFile.GroupBy
                                    (
                                        r3 => r3.ReferenceLineNumber
                                    )
                                )
                            )
                        )
                    )
                )
            );

            return result;
        }

        private static void MergeOccurrences(StreamWriter writer, IEnumerable<Reference> referencesOnTheSameLine)
        {
            var text = referencesOnTheSameLine.First().ReferenceLineText;
            referencesOnTheSameLine = referencesOnTheSameLine.OrderBy(r => r.ReferenceColumnStart);
            int current = 0;
            foreach (var occurrence in referencesOnTheSameLine)
            {
                if (occurrence.ReferenceColumnStart < 0 ||
                    occurrence.ReferenceColumnStart >= text.Length ||
                    occurrence.ReferenceColumnEnd <= occurrence.ReferenceColumnStart)
                {
                    string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                    message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                    message += "\r\ntext = " + text;
                    Log.Exception("MergeOccurrences1: " + message);
                }

                if (occurrence.ReferenceColumnStart > current)
                {
                    if (current < 0 ||
                        current >= text.Length ||
                        occurrence.ReferenceColumnStart < current ||
                        occurrence.ReferenceColumnStart >= text.Length)
                    {
                        string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                        message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                        message += "\r\ntext = " + text;
                        message += "\r\ncurrent = " + current;
                        Log.Exception("MergeOccurrences2: " + message);
                    }
                    else
                    {
                        Write(writer, text.Substring(current, occurrence.ReferenceColumnStart - current));
                    }
                }

                Write(writer, "<i>");
                Write(writer, text.Substring(occurrence.ReferenceColumnStart, occurrence.ReferenceColumnEnd - occurrence.ReferenceColumnStart));
                Write(writer, "</i>");
                current = occurrence.ReferenceColumnEnd;
            }

            if (current < text.Length)
            {
                Write(writer, text.Substring(current, text.Length - current));
            }
        }

        private static void Write(StreamWriter sw, string text)
        {
            sw.Write(text);
        }

        private static void Write(StreamWriter sw, string format, params object[] args)
        {
            sw.Write(string.Format(format, args));
        }

        private static void WriteLine(StreamWriter sw, string text)
        {
            sw.WriteLine(text);
        }

        private static void WriteLine(StreamWriter sw, string format, params object[] args)
        {
            sw.WriteLine(string.Format(format, args));
        }
    }
}
