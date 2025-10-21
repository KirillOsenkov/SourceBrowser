using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.SourceBrowser.Common
{
    public static class AssemblyNameExtractor
    {
        private static readonly object projectCollectionLock = new object();

        private static readonly Regex assemblyNameRegex = new Regex(@"<(?:Module)?AssemblyName>((\w|\.|\$|\(|\)|-)+)</(?:Module)?AssemblyName>", RegexOptions.Compiled);
        private static readonly Regex rootNamespaceRegex = new Regex(@"<RootNamespace>((\w|\.)+)</RootNamespace>", RegexOptions.Compiled);

        public static async Task<IEnumerable<string>> GetAssemblyNamesAsync(string projectOrSolutionFilePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(projectOrSolutionFilePath))
            {
                return null;
            }

            if (projectOrSolutionFilePath.EndsWith(".sln", System.StringComparison.OrdinalIgnoreCase) ||
                projectOrSolutionFilePath.EndsWith(".slnx", System.StringComparison.OrdinalIgnoreCase))
            {
                return await GetAssemblyNamesFromSolutionAsync(projectOrSolutionFilePath, cancellationToken);
            }
            else if (projectOrSolutionFilePath.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase) ||
                     projectOrSolutionFilePath.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    Path.GetFileNameWithoutExtension(projectOrSolutionFilePath)
                };
            }
            else
            {
                return new[] { GetAssemblyNameFromProject(projectOrSolutionFilePath) };
            }
        }

        public static string GetAssemblyNameFromProject(string projectFilePath)
        {
            string assemblyName = null;

            // first try regular expressions for the fast case
            var projectText = File.ReadAllText(projectFilePath);
            var match = assemblyNameRegex.Match(projectText);
            if (match.Groups.Count >= 2)
            {
                assemblyName = match.Groups[1].Value;

                if (assemblyName == "$(RootNamespace)")
                {
                    match = rootNamespaceRegex.Match(projectText);
                    if (match.Groups.Count >= 2)
                    {
                        assemblyName = match.Groups[1].Value;
                    }
                }

                return assemblyName;
            }

            // if regexes didn't work, try reading the XML ourselves
            var doc = XDocument.Load(projectFilePath);
            const string ns = "http://schemas.microsoft.com/developer/msbuild/2003";
            var propertyGroups = doc.Descendants(XName.Get("PropertyGroup", ns));
            var assemblyNameElement = propertyGroups.SelectMany(g => g.Elements(XName.Get("AssemblyName", ns))).LastOrDefault();
            if (assemblyNameElement != null && !assemblyNameElement.Value.Contains("$"))
            {
                return assemblyNameElement.Value;
            }

            var projectFileName = Path.GetFileNameWithoutExtension(projectFilePath);

            lock (projectCollectionLock)
            {
                try
                {
                    var project = ProjectCollection.GlobalProjectCollection.LoadProject(
                        projectFilePath,
                        toolsVersion: null);

                    assemblyName = project.GetPropertyValue("AssemblyName");
                    if (assemblyName?.Length == 0)
                    {
                        assemblyName = projectFileName;
                    }

                    if (assemblyName != null)
                    {
                        return assemblyName;
                    }
                }
                catch
                {
                }
                finally
                {
                    ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
                }
            }

            return projectFileName;
        }

        public static async Task<IEnumerable<string>> GetAssemblyNamesFromSolutionAsync(string solutionFilePath, CancellationToken cancellationToken)
        {
            string solutionDirectory = Path.GetDirectoryName(solutionFilePath);

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionFilePath);
            SolutionModel solutionModel = await serializer.OpenAsync(solutionFilePath, cancellationToken);

            var assemblies = new List<string>(solutionModel.SolutionProjects.Count);
            foreach (var projectModel in solutionModel.SolutionProjects)
            {
                try
                {
                    string projectFilePath = Path.Combine(solutionDirectory, projectModel.FilePath);
                    string assembly = GetAssemblyNameFromProject(projectFilePath);
                    assemblies.Add(assembly);
                }
                catch
                {
                }
            }

            return assemblies;
        }
    }
}
