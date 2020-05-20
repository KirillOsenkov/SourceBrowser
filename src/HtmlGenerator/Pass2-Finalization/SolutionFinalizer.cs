using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class SolutionFinalizer
    {
        public string SolutionDestinationFolder;
        public IEnumerable<ProjectFinalizer> projects;
        public readonly Dictionary<string, ProjectFinalizer> assemblyNameToProjectMap = new Dictionary<string, ProjectFinalizer>();

        public SolutionFinalizer(string rootPath)
        {
            this.SolutionDestinationFolder = rootPath;
            this.projects = DiscoverProjects()
                            .OrderBy(p => p.AssemblyId, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
            CalculateReferencingAssemblies();
        }

        private void CalculateReferencingAssemblies()
        {
            using (Disposable.Timing("Calculating referencing assemblies"))
            {
                foreach (var project in this.projects)
                {
                    assemblyNameToProjectMap.Add(project.AssemblyId, project);
                }

                foreach (var project in this.projects)
                {
                    if (project.ReferencedAssemblies != null)
                    {
                        foreach (var reference in project.ReferencedAssemblies)
                        {
                            if (assemblyNameToProjectMap.TryGetValue(reference, out ProjectFinalizer referencedProject))
                            {
                                referencedProject.ReferencingAssemblies.Add(project.AssemblyId);
                            }
                        }
                    }
                }

                var mostReferencedProjects = projects
                    .OrderByDescending(p => p.ReferencingAssemblies.Count)
                    .Select(p => p.AssemblyId + ";" + p.ReferencingAssemblies.Count)
                    .Take(100)
                    .ToArray();

                var filePath = Path.Combine(this.SolutionDestinationFolder, Constants.TopReferencedAssemblies + ".txt");
                File.WriteAllLines(filePath, mostReferencedProjects);
            }
        }

        private IEnumerable<ProjectFinalizer> DiscoverProjects()
        {
            var directories = Directory.GetDirectories(SolutionDestinationFolder);
            foreach (var directory in directories)
            {
                var referenceDirectory = Path.Combine(directory, Constants.ReferencesFileName);
                if (Directory.Exists(referenceDirectory))
                {
                    ProjectFinalizer finalizer = null;
                    try
                    {
                        finalizer = new ProjectFinalizer(this, directory);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "Failure when creating a ProjectFinalizer for " + directory);
                        finalizer = null;
                    }

                    if (finalizer != null)
                    {
                        yield return finalizer;
                    }
                }
            }
        }

        public void FinalizeProjects(bool emitAssemblyList, Federation federation, Folder<ProjectSkeleton> solutionExplorerRoot = null)
        {
            SortProcessedAssemblies();
            WriteSolutionExplorer(solutionExplorerRoot);
            CreateReferencesFiles();
            CreateMasterDeclarationsIndex();
            CreateProjectMap();
            CreateReferencingProjectLists();
            WriteAggregateStats();
            DeployFilesToRoot(SolutionDestinationFolder, emitAssemblyList, federation);

            if (emitAssemblyList)
            {
                var assemblyNames = projects
                    .Where(projectFinalizer => projectFinalizer.ProjectInfoLine != null)
                    .Select(projectFinalizer => projectFinalizer.AssemblyId).ToList();

                var sorter = GetCustomRootSorter();
                assemblyNames.Sort(sorter);

                Markup.GenerateResultsHtmlWithAssemblyList(SolutionDestinationFolder, assemblyNames);
            }
            else
            {
                Markup.GenerateResultsHtml(SolutionDestinationFolder);
            }
        }

        private Comparison<string> GetCustomRootSorter()
        {
            var file = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "AssemblySortOrder.txt");
            if (!File.Exists(file))
            {
                return (l, r) => StringComparer.OrdinalIgnoreCase.Compare(l, r);
            }

            var lines = File
                .ReadAllLines(file)
                .Select((assemblyName, index) => new KeyValuePair<string, int>(assemblyName, index + 1))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return (l, r) =>
            {
                lines.TryGetValue(l, out int index1);
                lines.TryGetValue(r, out int index2);
                if (index1 == 0 || index2 == 0)
                {
                    return l.CompareTo(r);
                }
                else
                {
                    return index1 - index2;
                }
            };
        }

        public static void SortProcessedAssemblies()
        {
            if (File.Exists(Paths.ProcessedAssemblies))
            {
                var lines = File.ReadAllLines(Paths.ProcessedAssemblies);
                Array.Sort(lines, StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(Paths.ProcessedAssemblies, lines);
            }
        }

        private void CreateReferencingProjectLists()
        {
            using (Disposable.Timing("Writing referencing assemblies"))
            {
                foreach (var project in this.projects)
                {
                    if (project.ReferencingAssemblies.Count > 0 && project.ReferencingAssemblies.Count < 100)
                    {
                        var fileName = Path.Combine(project.ProjectDestinationFolder, Constants.ReferencingAssemblyList + ".txt");
                        File.WriteAllLines(fileName, project.ReferencingAssemblies);
                        PatchProjectExplorer(project);
                    }
                }
            }
        }

        private void PatchProjectExplorer(ProjectFinalizer project)
        {
            if (project.ReferencingAssemblies.Count == 0 || project.ReferencingAssemblies.Count > 100)
            {
                return;
            }

            var fileName = Path.Combine(project.ProjectDestinationFolder, Constants.ProjectExplorer + ".html");
            if (!File.Exists(fileName))
            {
                return;
            }

            var sourceLines = File.ReadAllLines(fileName);
            List<string> lines = new List<string>(sourceLines.Length + project.ReferencingAssemblies.Count + 2);

            RelativeState state = RelativeState.Before;
            foreach (var sourceLine in sourceLines)
            {
                switch (state)
                {
                    case RelativeState.Before:
                        if (sourceLine == "<div class=\"folderTitle\">References</div><div class=\"folder\">")
                        {
                            state = RelativeState.Inside;
                        }

                        break;
                    case RelativeState.Inside:
                        if (sourceLine == "</div>")
                        {
                            state = RelativeState.InsertionPoint;
                        }

                        break;
                    case RelativeState.InsertionPoint:
                        lines.Add("<div class=\"folderTitle\">Used By</div><div class=\"folder\">");

                        foreach (var referencingAssembly in project.ReferencingAssemblies)
                        {
                            string referenceHtml = Markup.GetProjectExplorerReference("/#" + referencingAssembly, referencingAssembly);
                            lines.Add(referenceHtml);
                        }

                        lines.Add("</div>");

                        state = RelativeState.After;
                        break;
                    case RelativeState.After:
                        break;
                    default:
                        break;
                }

                lines.Add(sourceLine);
            }

            File.WriteAllLines(fileName, lines);
        }

        private enum RelativeState
        {
            Before,
            Inside,
            InsertionPoint,
            After
        }

        private void WriteAggregateStats()
        {
            string masterIndexFile = Path.Combine(SolutionDestinationFolder, Constants.ProjectInfoFileName + ".txt");
            var sb = new StringBuilder();

            long totalProjects = 0;
            long totalDocumentCount = 0;
            long totalLinesOfCode = 0;
            long totalBytesOfCode = 0;
            long totalDeclaredSymbolCount = 0;
            long totalDeclaredTypeCount = 0;
            long totalPublicTypeCount = 0;

            foreach (var project in this.projects)
            {
                totalProjects++;
                totalDocumentCount += project.DocumentCount;
                totalLinesOfCode += project.LinesOfCode;
                totalBytesOfCode += project.BytesOfCode;
                totalDeclaredSymbolCount += project.DeclaredSymbolCount;
                totalDeclaredTypeCount += project.DeclaredTypeCount;
                totalPublicTypeCount += project.PublicTypeCount;
            }

            sb.Append("ProjectCount=").AppendLine(totalProjects.WithThousandSeparators());
            sb.Append("DocumentCount=").AppendLine(totalDocumentCount.WithThousandSeparators());
            sb.Append("LinesOfCode=").AppendLine(totalLinesOfCode.WithThousandSeparators());
            sb.Append("BytesOfCode=").AppendLine(totalBytesOfCode.WithThousandSeparators());
            sb.Append("DeclaredSymbols=").AppendLine(totalDeclaredSymbolCount.WithThousandSeparators());
            sb.Append("DeclaredTypes=").AppendLine(totalDeclaredTypeCount.WithThousandSeparators());
            sb.Append("PublicTypes=").AppendLine(totalPublicTypeCount.WithThousandSeparators());

            File.WriteAllText(masterIndexFile, sb.ToString(), Encoding.UTF8);
        }

        private void CreateReferencesFiles()
        {
            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                project =>
                {
                    try
                    {
                        project.CreateReferencesFiles();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "CreateReferencesFiles failed for project: " + project.AssemblyId);
                    }
                });
        }

        private void DeployFilesToRoot(
            string destinationFolder,
            bool emitAssemblyList,
            Federation federation)
        {
            Markup.WriteReferencesNotFoundFile(destinationFolder);
        }

        public void CreateProjectMap(string outputPath = null)
        {
            var projects = this.projects
                // can't exclude assemblies without project because symbols rely on assembly index
                // and they just take the index from this.projects (see below)
                //.Where(p => p.ProjectInfoLine != null)
                .ToArray();
            Serialization.WriteProjectMap(
                outputPath ?? SolutionDestinationFolder,
                projects.Select(p => Tuple.Create(p.AssemblyId, p.ProjectInfoLine)),
                projects.ToDictionary(p => p.AssemblyId, p => p.ReferencingAssemblies.Count));
        }

        public void CreateMasterDeclarationsIndex(string outputPath = null)
        {
            var declaredSymbols = new List<DeclaredSymbolInfo>();
            ////var declaredTypes = new List<DeclaredSymbolInfo>();

            using (Measure.Time("Merging declared symbols"))
            {
                ushort assemblyNumber = 0;
                foreach (var project in this.projects)
                {
                    foreach (var symbolInfo in project.DeclaredSymbols.Values)
                    {
                        symbolInfo.AssemblyNumber = assemblyNumber;
                        declaredSymbols.Add(symbolInfo);

                        ////if (SymbolKindText.IsType(symbolInfo.Kind))
                        ////{
                        ////    declaredTypes.Add(symbolInfo);
                        ////}
                    }

                    assemblyNumber++;
                }
            }

            Serialization.WriteDeclaredSymbols(declaredSymbols, outputPath ?? SolutionDestinationFolder);
            ////NamespaceExplorer.WriteNamespaceExplorer(declaredTypes, outputPath ?? rootPath);
        }
    }
}
