using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class SolutionGenerator
    {
        public string SolutionSourceFolder { get; private set; }
        public string SolutionDestinationFolder { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string ServerPath { get; set; }
        public string NetworkShare { get; private set; }
        private Federation Federation { get; set; }
        private readonly HashSet<string> typeScriptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Solution solution;

        public SolutionGenerator(
            string solutionFilePath,
            string solutionDestinationFolder,
            string serverPath = null,
            ImmutableDictionary<string, string> properties = null,
            Federation federation = null)
        {
            this.SolutionSourceFolder = Path.GetDirectoryName(solutionFilePath);
            this.SolutionDestinationFolder = solutionDestinationFolder;
            this.ProjectFilePath = solutionFilePath;
            this.ServerPath = serverPath;
            this.solution = CreateSolution(solutionFilePath, properties);
            this.Federation = federation;
        }

        public SolutionGenerator(
            string projectFilePath,
            string commandLineArguments,
            string outputAssemblyPath,
            string solutionSourceFolder,
            string solutionDestinationFolder,
            string serverPath,
            string networkShare)
        {
            this.ProjectFilePath = projectFilePath;
            string projectName = Path.GetFileNameWithoutExtension(projectFilePath);
            string language = projectFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ?
                LanguageNames.VisualBasic : LanguageNames.CSharp;
            this.SolutionSourceFolder = solutionSourceFolder;
            this.SolutionDestinationFolder = solutionDestinationFolder;
            this.ServerPath = serverPath;
            this.NetworkShare = networkShare;
            string projectSourceFolder = Path.GetDirectoryName(projectFilePath);

            this.solution = CreateSolution(
                commandLineArguments,
                projectName,
                language,
                projectSourceFolder,
                outputAssemblyPath);
        }

        private static MSBuildWorkspace CreateWorkspace(ImmutableDictionary<string, string> propertiesOpt = null)
        {
            propertiesOpt = propertiesOpt ?? ImmutableDictionary<string, string>.Empty;

            // Explicitly add "CheckForSystemRuntimeDependency = true" property to correctly resolve facade references.
            // See https://github.com/dotnet/roslyn/issues/560
            propertiesOpt = propertiesOpt.Add("CheckForSystemRuntimeDependency", "true");
            propertiesOpt = propertiesOpt.Add("VisualStudioVersion", "14.0");

            return MSBuildWorkspace.Create(properties: propertiesOpt, hostServices: WorkspaceHacks.Pack);
        }

        private static Solution CreateSolution(
            string commandLineArguments,
            string projectName,
            string language,
            string projectSourceFolder,
            string outputAssemblyPath)
        {
            var workspace = CreateWorkspace();
            var projectInfo = CommandLineProject.CreateProjectInfo(
                projectName,
                language,
                commandLineArguments,
                projectSourceFolder,
                workspace);
            var solution = workspace.CurrentSolution.AddProject(projectInfo);

            solution = RemoveNonExistingFiles(solution);
            solution = AddAssemblyAttributesFile(language, outputAssemblyPath, solution);
            solution = DisambiguateSameNameLinkedFiles(solution);

            solution.Workspace.WorkspaceFailed += WorkspaceFailed;

            return solution;
        }

        private static Solution DisambiguateSameNameLinkedFiles(Solution solution)
        {
            foreach (var projectId in solution.ProjectIds.ToArray())
            {
                var project = solution.GetProject(projectId);
                solution = DisambiguateSameNameLinkedFiles(project);
            }

            return solution;
        }

        /// <summary>
        /// If there are two linked files both outside the project cone, and they have same names,
        /// they will logically appear as the same file in the project root. To disambiguate, we
        /// remove both files from the project's root and re-add them each into a folder chain that
        /// is formed from the full path of each document.
        /// </summary>
        private static Solution DisambiguateSameNameLinkedFiles(Project project)
        {
            var nameMap = project.Documents.Where(d => !d.Folders.Any()).ToLookup(d => d.Name);
            foreach (var conflictedItemGroup in nameMap.Where(g => g.Count() > 1))
            {
                foreach (var conflictedDocument in conflictedItemGroup)
                {
                    project = project.RemoveDocument(conflictedDocument.Id);
                    string filePath = conflictedDocument.FilePath;
                    DocumentId newId = DocumentId.CreateNewId(project.Id, filePath);
                    var folders = filePath.Split('\\').Select(p => p.TrimEnd(':'));
                    project = project.Solution.AddDocument(
                        newId,
                        conflictedDocument.Name,
                        conflictedDocument.GetTextAsync().Result,
                        folders,
                        filePath).GetProject(project.Id);
                }
            }

            return project.Solution;
        }

        private static Solution RemoveNonExistingFiles(Solution solution)
        {
            foreach (var projectId in solution.ProjectIds.ToArray())
            {
                var project = solution.GetProject(projectId);
                solution = RemoveNonExistingDocuments(project);

                project = solution.GetProject(projectId);
                solution = RemoveNonExistingReferences(project);
            }

            return solution;
        }

        private static Solution RemoveNonExistingDocuments(Project project)
        {
            foreach (var documentId in project.DocumentIds.ToArray())
            {
                var document = project.GetDocument(documentId);
                if (!File.Exists(document.FilePath))
                {
                    Log.Message("Document doesn't exist on disk: " + document.FilePath);
                    project = project.RemoveDocument(documentId);
                }
            }

            return project.Solution;
        }

        private static Solution RemoveNonExistingReferences(Project project)
        {
            foreach (var metadataReference in project.MetadataReferences.ToArray())
            {
                if (!File.Exists(metadataReference.Display))
                {
                    Log.Message("Reference assembly doesn't exist on disk: " + metadataReference.Display);
                    project = project.RemoveMetadataReference(metadataReference);
                }
            }

            return project.Solution;
        }

        private static Solution AddAssemblyAttributesFile(string language, string outputAssemblyPath, Solution solution)
        {
            if (!File.Exists(outputAssemblyPath))
            {
                Log.Exception("AddAssemblyAttributesFile: assembly doesn't exist: " + outputAssemblyPath);
                return solution;
            }

            var assemblyAttributesFileText = MetadataReading.GetAssemblyAttributesFileText(
                assemblyFilePath: outputAssemblyPath,
                language: language);
            if (assemblyAttributesFileText != null)
            {
                var extension = language == LanguageNames.CSharp ? ".cs" : ".vb";
                var newAssemblyAttributesDocumentName = MetadataAsSource.GeneratedAssemblyAttributesFileName + extension;
                var existingAssemblyAttributesFileName = "AssemblyAttributes" + extension;

                var project = solution.Projects.First();
                if (project.Documents.All(d => d.Name != existingAssemblyAttributesFileName || d.Folders.Count != 0))
                {
                    var document = project.AddDocument(
                        newAssemblyAttributesDocumentName,
                        assemblyAttributesFileText);
                    solution = document.Project.Solution;
                }
            }

            return solution;
        }

        public static string CurrentAssemblyName = null;

        /// <returns>true if only part of the solution was processed and the method needs to be called again, false if all done</returns>
        public bool Generate(HashSet<string> assemblyList = null)
        {
            if (solution == null)
            {
                // we failed to open the solution earlier; just return
                Log.Message("Solution is null: " + this.ProjectFilePath);
                return false;
            }

            var allProjects = solution.Projects.ToArray();
            if (allProjects.Length == 0)
            {
                Log.Exception("Solution " + this.ProjectFilePath + " has 0 projects - this is suspicious");
            }

            var projectsToProcess = allProjects
                .Where(p => assemblyList == null || !assemblyList.Contains(p.AssemblyName))
                .ToArray();
            var currentBatch = projectsToProcess
                .ToArray();
            foreach (var project in currentBatch)
            {
                try
                {
                    CurrentAssemblyName = project.AssemblyName;

                    var generator = new ProjectGenerator(this, project);
                    generator.Generate().GetAwaiter().GetResult();

                    File.AppendAllText(Paths.ProcessedAssemblies, project.AssemblyName + Environment.NewLine, Encoding.UTF8);
                    if (assemblyList != null)
                    {
                        assemblyList.Add(project.AssemblyName);
                    }
                }
                finally
                {
                    CurrentAssemblyName = null;
                }
            }

            new TypeScriptSupport().Generate(typeScriptFiles);

            if (currentBatch.Length > 1)
            {
                GenerateSolutionExplorer(
                    currentBatch,
                    flattenProjectList: false,
                    customRootSorter: GetCustomRootSorter());
            }

            return currentBatch.Length < projectsToProcess.Length;
        }

        public void GenerateResultsHtml(IEnumerable<string> assemblyList)
        {
            var sb = new StringBuilder();
            var sorter = GetCustomRootSorter();
            var assemblyNames = assemblyList.ToList();
            assemblyNames.Sort(sorter);

            sb.AppendLine(Markup.GetResultsHtmlPrefix());

            //foreach (var assemblyName in assemblyNames)
            //{
            //    sb.AppendFormat(@"<a href=""/#{0},namespaces"" target=""_top""><div class=""resultItem""><div class=""resultLine"">{0}</div></div></a>", assemblyName);
            //    sb.AppendLine();
            //}

            sb.AppendLine(Markup.GetResultsHtmlSuffix());

            File.WriteAllText(Path.Combine(SolutionDestinationFolder, "results.html"), sb.ToString());
        }

        public Comparison<string> GetCustomRootSorter()
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
                int index1, index2;
                lines.TryGetValue(l, out index1);
                lines.TryGetValue(r, out index2);
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

        private void SetFieldValue(object instance, string fieldName, object value)
        {
            var type = instance.GetType();
            var fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfo.SetValue(instance, null);
        }

        public void GenerateExternalReferences(HashSet<string> assemblyList)
        {
            var externalReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in solution.Projects)
            {
                var references = project.MetadataReferences
                    .OfType<PortableExecutableReference>()
                    .Where(m => File.Exists(m.FilePath))
                    .Where(m => !assemblyList.Contains(Path.GetFileNameWithoutExtension(m.FilePath)))
                    .Where(m => !IsPartOfSolution(Path.GetFileNameWithoutExtension(m.FilePath)))
                    .Where(m => GetExternalAssemblyIndex(Path.GetFileNameWithoutExtension(m.FilePath)) == -1)
                    .Select(m => Path.GetFullPath(m.FilePath));
                foreach (var reference in references)
                {
                    externalReferences[Path.GetFileNameWithoutExtension(reference)] = reference;
                }
            }

            foreach (var externalReference in externalReferences)
            {
                Log.Write(externalReference.Key, ConsoleColor.Magenta);
                var solutionGenerator = new SolutionGenerator(
                    externalReference.Value,
                    Paths.SolutionDestinationFolder);
                solutionGenerator.Generate(assemblyList);
            }
        }

        public bool IsPartOfSolution(string assemblyName)
        {
            if (solution == null)
            {
                // if we don't have a solution, it's probably metadata as source or standalone project
                // In either case we have the source for this assembly, so consider it part of the
                // "big solution"
                return true;
            }

            return solution.Projects.Any(
                p => StringComparer.OrdinalIgnoreCase.Equals(
                    p.AssemblyName,
                    assemblyName));
        }

        public int GetExternalAssemblyIndex(string assemblyName)
        {
            if (Federation == null)
            {
                return -1;
            }

            return Federation.GetExternalAssemblyIndex(assemblyName);
        }

        private Solution CreateSolution(string solutionFilePath, ImmutableDictionary<string, string> propertiesOpt = null)
        {
            try
            {
                Solution solution = null;
                if (solutionFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    var workspace = CreateWorkspace(propertiesOpt);
                    workspace.SkipUnrecognizedProjects = true;
                    workspace.WorkspaceFailed += WorkspaceFailed;
                    solution = workspace.OpenSolutionAsync(solutionFilePath).GetAwaiter().GetResult();
                }
                else if (
                    solutionFilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    solutionFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                {
                    var workspace = CreateWorkspace(propertiesOpt);
                    workspace.WorkspaceFailed += WorkspaceFailed;
                    solution = workspace.OpenProjectAsync(solutionFilePath).GetAwaiter().GetResult().Solution;
                }
                else if (
                    solutionFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    solutionFilePath.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase) ||
                    solutionFilePath.EndsWith(".netmodule", StringComparison.OrdinalIgnoreCase))
                {
                    solution = MetadataAsSource.LoadMetadataAsSourceSolution(solutionFilePath);
                    if (solution != null)
                    {
                        solution.Workspace.WorkspaceFailed += WorkspaceFailed;
                    }
                }

                if (solution == null)
                {
                    return null;
                }

                return solution;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Failed to open solution: " + solutionFilePath);
                return null;
            }
        }

        private static void WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            var message = e.Diagnostic.Message;
            if (message.StartsWith("Could not find file") || message.StartsWith("Could not find a part of the path"))
            {
                return;
            }

            if (message.StartsWith("The imported project "))
            {
                return;
            }

            if (message.Contains("because the file extension '.shproj'"))
            {
                return;
            }

            var project = ((Workspace)sender).CurrentSolution.Projects.FirstOrDefault();
            if (project != null)
            {
                message = message + " Project: " + project.Name;
            }

            Log.Exception("Workspace failed: " + message);
            Log.Write(message, ConsoleColor.Red);
        }

        public void AddTypeScriptFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            filePath = Path.GetFullPath(filePath);
            this.typeScriptFiles.Add(filePath);
        }
    }
}