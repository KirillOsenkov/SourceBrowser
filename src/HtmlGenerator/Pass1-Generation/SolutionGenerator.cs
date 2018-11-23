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
    public partial class SolutionGenerator : IDisposable
    {
        public string SolutionSourceFolder { get; private set; }
        public string SolutionDestinationFolder { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string ServerPath { get; set; }
        public IReadOnlyDictionary<string, string> ServerPathMappings { get; }
        public string NetworkShare { get; private set; }
        private Federation Federation { get; set; }
        public IEnumerable<string> PluginBlacklist { get; private set; }
        private readonly HashSet<string> typeScriptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public MEF.PluginAggregator PluginAggregator;

        /// <summary>
        /// List of all assembly names included in the index, from all solutions
        /// </summary>
        public HashSet<string> GlobalAssemblyList { get; set; }

        private Solution solution;
        private Workspace workspace;

        public SolutionGenerator(
            string solutionFilePath,
            string solutionDestinationFolder,
            string serverPath = null,
            ImmutableDictionary<string, string> properties = null,
            Federation federation = null,
            IReadOnlyDictionary<string, string> serverPathMappings = null,
            IEnumerable<string> pluginBlacklist = null)
        {
            this.SolutionSourceFolder = Path.GetDirectoryName(solutionFilePath);
            this.SolutionDestinationFolder = solutionDestinationFolder;
            this.ProjectFilePath = solutionFilePath;
            this.ServerPath = serverPath;
            ServerPathMappings = serverPathMappings;
            this.solution = CreateSolution(solutionFilePath, properties);
            this.Federation = federation ?? new Federation();
            this.PluginBlacklist = pluginBlacklist ?? Enumerable.Empty<string>();

            if (LoadPlugins)
            {
                SetupPluginAggregator();
            }
        }

        public static bool LoadPlugins { get; set; } = true;

        private void SetupPluginAggregator()
        {
            var settings = System.Configuration.ConfigurationManager.AppSettings;
            var configs = settings
                .AllKeys
                .Where(k => k.Contains(':'))                            //Ignore keys that don't have a colon to indicate which plugin they go to
                .Select(k => Tuple.Create(k.Split(':'), settings[k]))   //Get the data -- split the key to get the plugin name and setting name, look up the key to get the value
                .GroupBy(t => t.Item1[0])                               //Group the settings based on which plugin they're for
                .ToDictionary(
                    group => group.Key,                                 //Index the outer dictionary based on plugin
                    group => group.ToDictionary(
                        t => t.Item1[1],                                //Index the inner dictionary based on setting name
                        t => t.Item2                                    //The actual value of the setting
                    )
                );
            PluginAggregator = new MEF.PluginAggregator(configs, new Utilities.PluginLogger(), PluginBlacklist);
            FirstChanceExceptionHandler.IgnoreModules(PluginAggregator.Select(p => p.PluginModule));
            PluginAggregator.Init();
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
            SetupPluginAggregator();

            this.solution = CreateSolution(
                commandLineArguments,
                projectName,
                language,
                projectSourceFolder,
                outputAssemblyPath);
        }

        public IEnumerable<string> GetAssemblyNames()
        {
            if (solution != null)
            {
                return solution.Projects.Select(p => p.AssemblyName);
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        }

        private static MSBuildWorkspace CreateWorkspace(ImmutableDictionary<string, string> propertiesOpt = null)
        {
            propertiesOpt = propertiesOpt ?? ImmutableDictionary<string, string>.Empty;

            // Explicitly add "CheckForSystemRuntimeDependency = true" property to correctly resolve facade references.
            // See https://github.com/dotnet/roslyn/issues/560
            propertiesOpt = propertiesOpt.Add("CheckForSystemRuntimeDependency", "true");
            propertiesOpt = propertiesOpt.Add("VisualStudioVersion", "15.0");
            propertiesOpt = propertiesOpt.Add("AlwaysCompileMarkupFilesInSeparateDomain", "false");

            var w = MSBuildWorkspace.Create(properties: propertiesOpt);
            w.LoadMetadataForReferencedProjects = true;
            return w;
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
            solution = DeduplicateProjectReferences(solution);

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
                        assemblyAttributesFileText,
                        filePath: newAssemblyAttributesDocumentName);
                    solution = document.Project.Solution;
                }
            }

            return solution;
        }

        private static Solution DeduplicateProjectReferences(Solution solution)
        {
            foreach (var projectId in solution.ProjectIds.ToArray())
            {
                var project = solution.GetProject(projectId);

                var distinctProjectReferences = project.AllProjectReferences.Distinct().ToArray();
                if (distinctProjectReferences.Length < project.AllProjectReferences.Count)
                {
                    var duplicates = project.AllProjectReferences.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
                    foreach (var duplicate in duplicates)
                    {
                        Log.Write($"Duplicate project reference to {duplicate.ProjectId.ToString()} in project: {project.Name}", ConsoleColor.Yellow);
                    }

                    var newProject = project.WithProjectReferences(distinctProjectReferences);
                    solution = newProject.Solution;
                }
            }

            return solution;
        }

        public static string CurrentAssemblyName = null;

        /// <returns>true if only part of the solution was processed and the method needs to be called again, false if all done</returns>
        public bool Generate(HashSet<string> processedAssemblyList = null, Folder<Project> solutionExplorerRoot = null)
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
                .Where(p => processedAssemblyList == null || !processedAssemblyList.Contains(p.AssemblyName))
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
                    processedAssemblyList?.Add(project.AssemblyName);
                }
                finally
                {
                    CurrentAssemblyName = null;
                }
            }

            new TypeScriptSupport().Generate(typeScriptFiles, SolutionDestinationFolder);

            AddProjectsToSolutionExplorer(
                solutionExplorerRoot,
                currentBatch);

            return currentBatch.Length < projectsToProcess.Length;
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
                    .Where(m => File.Exists(m.FilePath) &&
                                !assemblyList.Contains(Path.GetFileNameWithoutExtension(m.FilePath)) &&
                                !IsPartOfSolution(Path.GetFileNameWithoutExtension(m.FilePath)) &&
                                GetExternalAssemblyIndex(Path.GetFileNameWithoutExtension(m.FilePath)) == -1
                    )
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
                    Paths.SolutionDestinationFolder,
                    pluginBlacklist: PluginBlacklist);
                solutionGenerator.Generate(assemblyList);
            }
        }

        public bool IsPartOfSolution(string assemblyName)
        {
            if (GlobalAssemblyList == null)
            {
                // if for some reason we don't know a global list, assume everything is in the solution
                // this is better than the alternative
                return true;
            }

            return GlobalAssemblyList.Contains(assemblyName);
        }

        public int GetExternalAssemblyIndex(string assemblyName)
        {
            if (Federation == null)
            {
                return -1;
            }

            return Federation.GetExternalAssemblyIndex(assemblyName);
        }

        private Solution CreateSolution(string solutionFilePath, ImmutableDictionary<string, string> properties = null)
        {
            try
            {
                Solution solution = null;
                if (solutionFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    properties = AddSolutionProperties(properties, solutionFilePath);
                    var workspace = CreateWorkspace(properties);
                    workspace.SkipUnrecognizedProjects = true;
                    workspace.WorkspaceFailed += WorkspaceFailed;
                    solution = workspace.OpenSolutionAsync(solutionFilePath).GetAwaiter().GetResult();
                    solution = DeduplicateProjectReferences(solution);
                    this.workspace = workspace;
                }
                else if (
                    solutionFilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    solutionFilePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                {
                    var workspace = CreateWorkspace(properties);
                    workspace.WorkspaceFailed += WorkspaceFailed;
                    solution = workspace.OpenProjectAsync(solutionFilePath).GetAwaiter().GetResult().Solution;
                    solution = DeduplicateProjectReferences(solution);
                    this.workspace = workspace;
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
                        workspace = solution.Workspace;
                    }
                }

                return solution;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Failed to open solution: " + solutionFilePath);
                return null;
            }
        }

        private ImmutableDictionary<string, string> AddSolutionProperties(ImmutableDictionary<string, string> properties, string solutionFilePath)
        {
            // http://referencesource.microsoft.com/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/bin_/amd64/Microsoft.Common.CurrentVersion.targets,296
            properties = properties ?? ImmutableDictionary<string, string>.Empty;
            properties = properties.Add("SolutionName", Path.GetFileNameWithoutExtension(solutionFilePath));
            properties = properties.Add("SolutionFileName", Path.GetFileName(solutionFilePath));
            properties = properties.Add("SolutionPath", solutionFilePath);
            properties = properties.Add("SolutionDir", Path.GetDirectoryName(solutionFilePath));
            properties = properties.Add("SolutionExt", Path.GetExtension(solutionFilePath));
            return properties;
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

        public void Dispose()
        {
            if (workspace != null)
            {
                workspace.Dispose();
                workspace = null;
            }
        }
    }
}