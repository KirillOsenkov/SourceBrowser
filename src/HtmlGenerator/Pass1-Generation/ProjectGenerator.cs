using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        private readonly string assemblyAttributesFileName;

        public Project Project { get; private set; }
        public Dictionary<string, List<Tuple<string, long>>> SymbolIDToListOfLocationsMap { get; private set; }
        public Dictionary<ISymbol, string> DeclaredSymbols { get; private set; }
        public Dictionary<ISymbol, ISymbol> BaseMembers { get; private set; }
        public MultiDictionary<ISymbol, ISymbol> ImplementedInterfaceMembers { get; set; }

        public string ProjectDestinationFolder { get; private set; }
        public string AssemblyName { get; private set; }
        public SolutionGenerator SolutionGenerator { get; private set; }
        public string ProjectSourcePath { get; set; }
        public string ProjectFilePath { get; private set; }
        public List<string> OtherFiles { get; set; }
        public IEnumerable<MEF.ISymbolVisitor> PluginSymbolVisitors { get; private set; }
        public IEnumerable<MEF.ITextVisitor> PluginTextVisitors { get; private set; }

        public ProjectGenerator(SolutionGenerator solutionGenerator, Project project) : this()
        {
            this.SolutionGenerator = solutionGenerator;
            this.Project = project;
            this.ProjectFilePath = project.FilePath ?? solutionGenerator.ProjectFilePath;
            this.DeclaredSymbols = new Dictionary<ISymbol, string>();
            this.BaseMembers = new Dictionary<ISymbol, ISymbol>();
            this.ImplementedInterfaceMembers = new MultiDictionary<ISymbol, ISymbol>();
            this.assemblyAttributesFileName = MetadataAsSource.GeneratedAssemblyAttributesFileName + (project.Language == LanguageNames.CSharp ? ".cs" : ".vb");
            PluginSymbolVisitors = SolutionGenerator.PluginAggregator?.ManufactureSymbolVisitors(project).ToArray();
            PluginTextVisitors = SolutionGenerator.PluginAggregator?.ManufactureTextVisitors(project).ToArray();
        }

        /// <summary>
        /// This constructor is used for non-C#/VB projects such as "MSBuildFiles"
        /// </summary>
        public ProjectGenerator(string folderName, string solutionDestinationFolder) : this()
        {
            ProjectDestinationFolder = Path.Combine(solutionDestinationFolder, folderName);
            Directory.CreateDirectory(Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName));
        }

        private void AddHtmlFilesToRedirectMap()
        {
            var files = Directory
                            .GetFiles(ProjectDestinationFolder, "*.html", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = file.Substring(ProjectDestinationFolder.Length + 1).Replace('\\', '/');
                relativePath = relativePath.Substring(0, relativePath.Length - 5); // strip .html
                AddFileToRedirectMap(relativePath);
                OtherFiles.Add(relativePath);
            }
        }

        private void AddFileToRedirectMap(string filePath)
        {
            lock (SymbolIDToListOfLocationsMap)
            {
                SymbolIDToListOfLocationsMap.Add(
                    SymbolIdService.GetId(filePath),
                    new List<Tuple<string, long>> { Tuple.Create(filePath, 0L) });
            }
        }

        private ProjectGenerator()
        {
            this.SymbolIDToListOfLocationsMap = new Dictionary<string, List<Tuple<string, long>>>();
            this.OtherFiles = new List<string>();
        }

        public async Task Generate()
        {
            try
            {
                if (string.IsNullOrEmpty(ProjectFilePath))
                {
                    Log.Exception("ProjectFilePath is empty: " + Project.ToString());
                    return;
                }

                ProjectDestinationFolder = GetProjectDestinationPath(Project, SolutionGenerator.SolutionDestinationFolder);
                if (ProjectDestinationFolder == null)
                {
                    Log.Exception("Errors evaluating project: " + Project.Id);
                    return;
                }

                Log.Write(ProjectDestinationFolder, ConsoleColor.DarkCyan);

                ProjectSourcePath = Paths.MakeRelativeToFolder(ProjectFilePath, SolutionGenerator.SolutionSourceFolder);

                if (File.Exists(Path.Combine(ProjectDestinationFolder, Constants.DeclaredSymbolsFileName + ".txt")))
                {
                    // apparently someone already generated a project with this assembly name - their assembly wins
                    Log.Exception(string.Format(
                        "A project with assembly name {0} was already generated, skipping current project: {1}",
                        this.AssemblyName,
                        this.ProjectFilePath), isSevere: false);
                    return;
                }

                if (Configuration.CreateFoldersOnDisk)
                {
                    Directory.CreateDirectory(ProjectDestinationFolder);
                }

                var documents = Project.Documents.Where(IncludeDocument).ToList();

                var generationTasks = Partitioner.Create(documents)
                    .GetPartitions(Environment.ProcessorCount)
                    .Select(partition =>
                        Task.Run(async () =>
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                  await GenerateDocument(partition.Current);
                                }
                            }
                        }));

                await Task.WhenAll(generationTasks);

                foreach (var document in documents)
                {
                    OtherFiles.Add(Paths.GetRelativeFilePathInProject(document));
                }

                if (Configuration.WriteProjectAuxiliaryFilesToDisk)
                {
                    GenerateProjectFile();
                    GenerateDeclarations();
                    GenerateBaseMembers();
                    GenerateImplementedInterfaceMembers();
                    GenerateProjectInfo();
                    GenerateReferencesDataFiles(
                        this.SolutionGenerator.SolutionDestinationFolder,
                        ReferencesByTargetAssemblyAndSymbolId);
                    GenerateSymbolIDToListOfDeclarationLocationsMap(
                        ProjectDestinationFolder,
                        SymbolIDToListOfLocationsMap);
                    GenerateReferencedAssemblyList();
                    GenerateUsedReferencedAssemblyList();
                    GenerateProjectExplorer();
                    GenerateNamespaceExplorer();
                    GenerateIndex();
                }

                var compilation = Project.GetCompilationAsync().Result;
                var diagnostics = compilation.GetDiagnostics().Select(d => d.ToString()).ToArray();
                if (diagnostics.Length > 0)
                {
                    var diagnosticsTxt = Path.Combine(this.ProjectDestinationFolder, "diagnostics.txt");
                    File.WriteAllLines(diagnosticsTxt, diagnostics);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Project generation failed for: " + ProjectSourcePath);
            }
        }

        public void GenerateNonProjectFolder()
        {
            AddHtmlFilesToRedirectMap();
            GenerateDeclarations();
            GenerateSymbolIDToListOfDeclarationLocationsMap(
                ProjectDestinationFolder,
                SymbolIDToListOfLocationsMap);
        }

        private void GenerateNamespaceExplorer()
        {
            Log.Write("Namespace Explorer...");
            var symbols = this.DeclaredSymbols.Keys.OfType<INamedTypeSymbol>()
                .Select(s => new DeclaredSymbolInfo(s, this.AssemblyName));
            NamespaceExplorer.WriteNamespaceExplorer(this.AssemblyName, symbols, ProjectDestinationFolder);
        }

        private Task GenerateDocument(Document document)
        {
            try
            {
                var documentGenerator = new DocumentGenerator(this, document);
                return documentGenerator.Generate();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Document generation failed for: " + (document.FilePath ?? document.ToString()));
                return Task.FromResult(e);
            }
        }

        private void GenerateIndex()
        {
            Log.Write("Index.html...");
            var index = Path.Combine(ProjectDestinationFolder, "index.html");
            var sb = new StringBuilder();
            Markup.WriteProjectIndex(sb, Project.AssemblyName);
            File.WriteAllText(index, sb.ToString());
        }

        private bool IsCSharp
        {
            get
            {
                return Project.Language == LanguageNames.CSharp;
            }
        }

        private bool IncludeDocument(Document document)
        {
            if (document.Name == assemblyAttributesFileName)
            {
                return false;
            }

            return true;
        }

        private string GetProjectDestinationPath(Project project, string solutionDestinationPath)
        {
            var assemblyName = project.AssemblyName;
            if (assemblyName == "<Error>")
            {
                return null;
            }

            AssemblyName = SymbolIdService.GetAssemblyId(assemblyName);
            string subfolder = Path.Combine(solutionDestinationPath, AssemblyName);
            return subfolder;
        }
    }
}
