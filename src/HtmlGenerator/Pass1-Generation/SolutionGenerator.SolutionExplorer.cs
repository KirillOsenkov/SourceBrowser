using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis;
using Folder = Microsoft.SourceBrowser.HtmlGenerator.Folder<Microsoft.CodeAnalysis.Project>;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    partial class SolutionGenerator
    {
        public void AddProjectsToSolutionExplorer(Folder root, IEnumerable<Project> projects)
        {
            Dictionary<string, IEnumerable<string>> projectToSolutionFolderMap = null;
            if (!Configuration.FlattenSolutionExplorer)
            {
                projectToSolutionFolderMap = GetProjectToSolutionFolderMap(ProjectFilePath);
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

            // it is possible that the solution has more projects than mentioned in the .sln file
            // because Roslyn might add more projects from project references that aren't mentioned
            // in the .sln
            projectToSolutionFolderMap?.TryGetValue(fullPath, out folders);
            AddProjectToFolder(root, project, folders);
        }

        private void AddProjectToFolder(Folder folder, Project project, IEnumerable<string> folders = null)
        {
            if (folders == null || !folders.Any())
            {
                folder.Add(project);
            }
            else
            {
                var subfolder = folder.GetOrCreateFolder(folders.First());
                AddProjectToFolder(subfolder, project, folders.Skip(1));
            }
        }

        private static Dictionary<string, IEnumerable<string>> GetProjectToSolutionFolderMap(string solutionFilePath)
        {
            if (!solutionFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var solutionFile = SolutionFile.Parse(solutionFilePath);

            var result = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in solutionFile.ProjectsInOrder)
            {
                if (project.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    continue;
                }

                var path = GetAbsoluteFilePath(project);
                var parentFolderChain = GetParentFolderChain(solutionFile, project);

                result.Add(path, parentFolderChain);
            }

            return result;
        }

        private static string GetAbsoluteFilePath(ProjectInSolution project)
        {
            var path = project.AbsolutePath;
            if (string.IsNullOrEmpty(path))
            {
                path = project.ProjectName;
            }

            return path;
        }

        private static List<string> GetParentFolderChain(SolutionFile solutionFile, ProjectInSolution project)
        {
            var parentFolderChain = new List<string>();
            var parentGuid = project.ParentProjectGuid;

            while (!string.IsNullOrEmpty(parentGuid) && solutionFile.ProjectsByGuid.TryGetValue(parentGuid, out ProjectInSolution parentFolder) && parentFolder != null)
            {
                parentFolderChain.Add(parentFolder.ProjectName);
                parentGuid = parentFolder.ParentProjectGuid;
            }

            parentFolderChain.Reverse();
            return parentFolderChain;
        }
    }
}
