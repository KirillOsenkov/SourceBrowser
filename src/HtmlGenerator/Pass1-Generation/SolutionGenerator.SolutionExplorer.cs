using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Folder = Microsoft.SourceBrowser.HtmlGenerator.Folder<Microsoft.SourceBrowser.HtmlGenerator.ProjectSkeleton>;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    partial class SolutionGenerator
    {
        public async Task AddProjectsToSolutionExplorerAsync(Folder root, IEnumerable<Project> projects, CancellationToken cancellationToken)
        {
            Dictionary<string, IEnumerable<string>> projectToSolutionFolderMap = null;
            if (!Configuration.FlattenSolutionExplorer)
            {
                projectToSolutionFolderMap = await GetProjectToSolutionFolderMapAsync(ProjectFilePath, cancellationToken);
            }

            foreach (var project in projects)
            {
                if (Configuration.FlattenSolutionExplorer)
                {
                    AddProjectToFolder(root, project);
                }
                else
                {
                    AddProjectToFolder(root, project, projectToSolutionFolderMap);
                }
            }
        }

        private void AddProjectToFolder(Folder root, Project project, Dictionary<string, IEnumerable<string>> projectToSolutionFolderMap)
        {
            var fullPath = project.FilePath;
            IEnumerable<string> folders = null;

            // it is possible that the solution has more projects than mentioned in the .sln/.slnx file
            // because Roslyn might add more projects from project references that aren't mentioned
            // in the .sln/.slnx
            projectToSolutionFolderMap?.TryGetValue(fullPath, out folders);
            AddProjectToFolder(root, project, folders);
        }

        private void AddProjectToFolder(Folder folder, Project project, IEnumerable<string> folders = null)
        {
            if (folders == null || !folders.Any())
            {
                folder.Add(new ProjectSkeleton(project.AssemblyName, project.Name));
            }
            else
            {
                var subfolder = folder.GetOrCreateFolder(folders.First());
                AddProjectToFolder(subfolder, project, folders.Skip(1));
            }
        }

        private static async Task<Dictionary<string, IEnumerable<string>>> GetProjectToSolutionFolderMapAsync(string solutionFilePath, CancellationToken cancellationToken)
        {
            if (!solutionFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
                !solutionFilePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string solutionDirectory = Path.GetDirectoryName(solutionFilePath);

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionFilePath);
            SolutionModel solutionModel = await serializer.OpenAsync(solutionFilePath, cancellationToken);

            var result = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var projectModel in solutionModel.SolutionProjects)
            {
                var path = GetAbsoluteFilePath(solutionDirectory, projectModel);
                var parentFolderChain = GetParentFolderChain(solutionModel, projectModel);

                result.Add(path, parentFolderChain);
            }

            return result;
        }

        private static string GetAbsoluteFilePath(string solutionDirectory, SolutionProjectModel projectModel)
        {
            var projectFilePath = projectModel.FilePath;

            if (string.IsNullOrEmpty(projectFilePath))
            {
                projectFilePath = projectModel.DisplayName;
            }

            return Path.Combine(solutionDirectory, projectFilePath);
        }

        private static List<string> GetParentFolderChain(SolutionModel solutionModel, SolutionProjectModel projectModel)
        {
            var parentFolderChain = new List<string>();
            var folderModel = projectModel.Parent;

            while (folderModel is not null)
            {
                parentFolderChain.Add(folderModel.Name);
                folderModel = folderModel.Parent;
            }

            parentFolderChain.Reverse();
            return parentFolderChain;
        }
    }
}
