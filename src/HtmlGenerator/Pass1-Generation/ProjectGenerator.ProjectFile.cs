using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    partial class ProjectGenerator
    {
        private Project msbuildProject;

        private void GenerateProjectFile()
        {
            var projectExtension = Path.GetExtension(ProjectFilePath);
            if (!File.Exists(ProjectFilePath) ||
                ".dll".Equals(projectExtension, StringComparison.OrdinalIgnoreCase) ||
                ".winmd".Equals(projectExtension, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ProjectCollection projectCollection = null;

            try
            {
                var title = Path.GetFileName(ProjectFilePath);
                var destinationFileName = Path.Combine(ProjectDestinationFolder, title) + ".html";

                AddDeclaredSymbolToRedirectMap(SymbolIDToListOfLocationsMap, SymbolIdService.GetId(title), title, 0);

                // ProjectCollection caches the environment variables it reads at startup
                // and doesn't re-get them later. We need a new project collection to read
                // the latest set of environment variables.
                projectCollection = new ProjectCollection();
                this.msbuildProject = new Project(
                    ProjectFilePath,
                    null,
                    null,
                    projectCollection,
                    ProjectLoadSettings.IgnoreMissingImports);

                var msbuildSupport = new MSBuildSupport(this);
                msbuildSupport.Generate(ProjectFilePath, destinationFileName, msbuildProject, true);

                GenerateXmlFiles(msbuildProject);

                GenerateXamlFiles(msbuildProject);

                GenerateResxFiles(msbuildProject);

                GenerateTypeScriptFiles(msbuildProject);

                OtherFiles.Add(title);
            }
            catch (Exception ex)
            {
                Log.Exception("Exception during Project file generation: " + ProjectFilePath + "\r\n" + ex.ToString());
            }
            finally
            {
                if (projectCollection != null)
                {
                    projectCollection.UnloadAllProjects();
                    projectCollection.Dispose();
                }
            }
        }

        private void GenerateTypeScriptFiles(Project msbuildProject)
        {
            var typeScriptCompileItems = msbuildProject.GetItems("TypeScriptCompile");
            foreach (var typeScriptFile in typeScriptCompileItems)
            {
                GenerateTypeScriptFile(typeScriptFile.EvaluatedInclude);
            }
        }

        private void GenerateXmlFiles(Project msbuildProject)
        {
            GenerateXmlFilesOfProjectItemType(msbuildProject, "Resource");
        }

        private void GenerateXamlFiles(Project msbuildProject)
        {
            GenerateXmlFilesOfProjectItemType(msbuildProject, "Page");
        }

        private void GenerateResxFiles(Project msbuildProject)
        {
            GenerateXmlFilesOfProjectItemType(msbuildProject, "EmbeddedResource");
        }

        private void GenerateXmlFilesOfProjectItemType(Project msbuildProject, string itemType)
        {
            var resxItems = msbuildProject.GetItems(itemType);
            foreach (var resxItem in resxItems)
            {
                var resxFile = resxItem.EvaluatedInclude;
                GenerateXmlFile(resxFile);
            }
        }

        private void GenerateTypeScriptFile(string filePath)
        {
            if (File.Exists(filePath = NormalizeFilePath(filePath)))
            {
                AddOtherFileRelativeToProject(filePath);
                SolutionGenerator.AddTypeScriptFile(filePath);
            }
        }

        private void GenerateXmlFile(string filePath)
        {
            if (File.Exists(filePath = NormalizeFilePath(filePath)))
            {
                var relativePath = AddOtherFileRelativeToProject(filePath);

                var destinationHtmlFile = Path.Combine(ProjectDestinationFolder, relativePath) + ".html";
                new XmlSupport(this).Generate(filePath, destinationHtmlFile, relativePath);

                AddDeclaredSymbolToRedirectMap(SymbolIDToListOfLocationsMap, SymbolIdService.GetId(relativePath), relativePath, 0);
            }
        }

        private string NormalizeFilePath(string filePath)
        {
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(Path.GetDirectoryName(ProjectFilePath), filePath);
            }

            return Path.GetFullPath(filePath);
        }

        private string AddOtherFileRelativeToProject(string filePath)
        {
            var relativePath = Paths.MakeRelativeToFile(filePath, ProjectFilePath);
            relativePath = relativePath.Replace("..", "parent");

            OtherFiles.Add(relativePath);

            return relativePath;
        }
    }
}
