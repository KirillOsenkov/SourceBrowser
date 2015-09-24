using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.SourceBrowser.Common;
using Folder = Microsoft.SourceBrowser.HtmlGenerator.Folder<Microsoft.CodeAnalysis.Project>;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    partial class SolutionGenerator
    {
        public void GenerateSolutionExplorer(
            IEnumerable<Project> projects,
            bool flattenProjectList = false,
            Comparison<string> customRootSorter = null)
        {
            if (!ProjectFilePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Dictionary<string, IEnumerable<string>> projectToSolutionFolderMap = null;
            if (!flattenProjectList)
            {
                projectToSolutionFolderMap = GetProjectToSolutionFolderMap(ProjectFilePath);
            }

            Folder root = new Folder();
            var processedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in projects)
            {
                if (!processedAssemblyNames.Add(project.AssemblyName))
                {
                    // filter out multiple projects with the same assembly name
                    continue;
                }

                if (flattenProjectList)
                {
                    AddProjectToFolder(root, project);
                }
                else
                {
                    AddProjectToFolder(root, project, projectToSolutionFolderMap);
                }
            }

            if (flattenProjectList)
            {
                if (customRootSorter == null)
                {
                    customRootSorter = (l, r) => StringComparer.OrdinalIgnoreCase.Compare(l, r);
                }

                root.Sort((l, r) => customRootSorter(l.AssemblyName, r.AssemblyName));
            }
            else
            {
                root.Sort((l, r) => StringComparer.OrdinalIgnoreCase.Compare(l.Name, r.Name));
            }

            using (var writer = new StreamWriter(Path.Combine(SolutionDestinationFolder, Constants.SolutionExplorer + ".html")))
            {
                Log.Write("Solution Explorer...");
                Markup.WriteSolutionExplorerPrefix(writer);
                WriteFolder(root, writer);
                Markup.WriteSolutionExplorerSuffix(writer);
            }
        }

        private void WriteFolder(Folder folder, StreamWriter writer)
        {
            if (folder.Folders != null)
            {
                foreach (var subfolder in folder.Folders.Values)
                {
                    writer.WriteLine(@"<div class=""folderTitle"">{0}</div><div class=""folder"">", subfolder.Name);
                    WriteFolder(subfolder, writer);
                    writer.WriteLine("</div>");
                }
            }

            if (folder.Items != null)
            {
                foreach (var project in folder.Items)
                {
                    WriteProject(project.AssemblyName, writer);
                }
            }
        }

        private void WriteProject(string assemblyName, StreamWriter writer)
        {
            var projectExplorerText = GetProjectExplorerText(assemblyName);
            if (!string.IsNullOrEmpty(projectExplorerText))
            {
                writer.WriteLine(projectExplorerText);
            }
        }

        private string GetProjectExplorerText(string assemblyName)
        {
            var fileName = Path.Combine(SolutionDestinationFolder, assemblyName, Constants.ProjectExplorer + ".html");
            if (!File.Exists(fileName))
            {
                return null;
            }

            var text = File.ReadAllText(fileName);
            var startText = "<div id=\"rootFolder\"";
            var start = text.IndexOf(startText) + startText.Length;
            var end = text.IndexOf("<script>");
            text = text.Substring(start, end - start);
            text = "<div" + text;
            text = text.Replace(@"</div><div>", string.Format("</div><div class=\"folder\" data-assembly=\"{0}\">", assemblyName));
            text = text.Replace("projectCS", "projectCSInSolution");
            text = text.Replace("projectVB", "projectVBInSolution");

            var projectInfoStart = text.IndexOf("<p class=\"projectInfo");
            if (projectInfoStart != -1)
            {
                var projectInfoEnd = text.IndexOf("</p>", projectInfoStart) + 4;
                if (projectInfoEnd != -1)
                {
                    text = text.Remove(projectInfoStart, projectInfoEnd - projectInfoStart);
                }
            }

            return text;
        }

        private void AddProjectToFolder(Folder root, Project project, Dictionary<string, IEnumerable<string>> projectToSolutionFolderMap)
        {
            var fullPath = project.FilePath;
            IEnumerable<string> folders = null;

            // it is possible that the solution has more projects than mentioned in the .sln file
            // because Roslyn might add more projects from project references that aren't mentioned
            // in the .sln
            projectToSolutionFolderMap.TryGetValue(fullPath, out folders);
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
            string solutionFolder = Path.GetDirectoryName(solutionFilePath);
            var projectGuidToFullPath = new Dictionary<Guid, string>();
            var projectToParent = new Dictionary<Guid, Guid>();
            var projectFullPathToFolders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            dynamic solutionFile = GetSolutionFile(solutionFilePath);
            IEnumerable projectBlocks = GetProperty<IEnumerable>(solutionFile, "ProjectBlocks");
            foreach (var projectBlock in projectBlocks)
            {
                Guid projectGuid = GetProperty<Guid>(projectBlock, "ProjectGuid");
                var path = GetProperty<string>(projectBlock, "ProjectPath");
                if (string.IsNullOrEmpty(path))
                {
                    path = GetProperty<string>(projectBlock, "ProjectName");
                }

                projectGuidToFullPath[projectGuid] = path;
            }

            IEnumerable<object> globalSectionBlocks = GetProperty<IEnumerable<object>>(solutionFile, "GlobalSectionBlocks");
            var nestedProjects = globalSectionBlocks.FirstOrDefault(
                (Func<object, bool>)(section => GetProperty<string>(section, "ParenthesizedName") == "NestedProjects"));
            if (nestedProjects != null)
            {
                foreach (var kvp in GetProperty<IEnumerable<KeyValuePair<string, string>>>(nestedProjects, "KeyValuePairs"))
                {
                    Guid child = Guid.Parse(kvp.Key);
                    Guid parent = Guid.Parse(kvp.Value);
                    projectToParent[child] = parent;
                }
            }

            var result = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var projectBlock in projectBlocks)
            {
                Guid projectGuid = GetProperty<Guid>(projectBlock, "ProjectGuid");
                string projectName = GetProperty<string>(projectBlock, "ProjectName");
                string projectPath = GetProperty<string>(projectBlock, "ProjectPath");
                if (string.Equals(projectName, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    // not interested in solution folders
                    continue;
                }

                string projectFullPath = Path.GetFullPath(Path.Combine(solutionFolder, projectPath));
                List<string> folders = new List<string>();
                Guid parentGuid;
                while (projectToParent.TryGetValue(projectGuid, out parentGuid))
                {
                    string fullPath = null;
                    if (!projectGuidToFullPath.TryGetValue(parentGuid, out fullPath))
                    {
                        break;
                    }

                    var folderName = Path.GetFileName(fullPath);
                    folders.Add(folderName);
                    projectGuid = parentGuid;
                }

                folders.Reverse();

                result.Add(projectFullPath, folders);
            }

            return result;
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(propertyName).GetValue(instance);
            return (T)property;
        }

        private static dynamic GetSolutionFile(string filePath)
        {
            var assembly = typeof(MSBuildWorkspace).Assembly;
            var type = assembly.GetType("Microsoft.CodeAnalysis.MSBuild.SolutionFile");
            var method = type.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public);
            dynamic solutionFile;
            using (var reader = new StreamReader(filePath))
            {
                solutionFile = method.Invoke(null, new object[] { reader });
            }

            return solutionFile;
        }
    }
}
