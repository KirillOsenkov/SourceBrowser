using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.SourceBrowser.HtmlGenerator.Utilities
{
    public static class MSBuildHelper
    {
        private static string GetAssemblyFromProject(string projFile)
        {
            using (ProjectCollection p = new ProjectCollection())
            {
                var sln = p.LoadProject(projFile);
                string output = sln.GetPropertyValue("AssemblyName");

                p.UnloadAllProjects();
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

                return output;
            }
        }
        public static IEnumerable<string> GetAssemblies(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return Enumerable.Empty<string>();
            }

            if (path.EndsWith(".csproj") || path.EndsWith(".vbproj"))
            {
                try
                {
                    return new[] { GetAssemblyFromProject(path) };
                }
                catch
                {
                }
                return Enumerable.Empty<string>();
            }

            if (path.EndsWith(".sln"))
            {
                var s = Microsoft.Build.Construction.SolutionFile.Parse(path);
                List<string> assemblies = new List<string>(s.ProjectsInOrder.Count);
                foreach (var proj in s.ProjectsInOrder)
                {
                    if (proj.ProjectType == SolutionProjectType.SolutionFolder)
                    {
                        continue;
                    }
                    try
                    {
                        string assembly = GetAssemblyFromProject(proj.AbsolutePath);
                        assemblies.Add(assembly);
                    }
                    catch
                    {
                    }
                }
                return assemblies;
            }

            return Enumerable.Empty<string>();
        }
    }
}
