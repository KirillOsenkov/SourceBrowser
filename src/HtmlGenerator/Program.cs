using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    using Microsoft.SourceBrowser.HtmlGenerator.Utilities;

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            WebProxyAuthenticator.Authenticate("http://referencesource.microsoft.com");

            var projects = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("/out:"))
                {
                    Paths.SolutionDestinationFolder = arg.Substring("/out:".Length).StripQuotes();
                    continue;
                }

                try
                {
                    var project = Path.GetFullPath(arg);
                    if (File.Exists(project))
                    {
                        if (project.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                            project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                            project.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                        {
                            projects.Add(project.StripQuotes());
                        }
                    }
                }
                catch
                {
                }
            }

            if (projects.Count == 0)
            {
                PrintUsage();
                return;
            }

            AssertTraceListener.Register();
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceExceptionHandler.HandleFirstChanceException;

            if (Paths.SolutionDestinationFolder == null)
            {
                Paths.SolutionDestinationFolder = Path.Combine(Microsoft.SourceBrowser.Common.Paths.BaseAppFolder, "Index");
            }

            Log.ErrorLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.ErrorLogFile);
            Log.MessageLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.MessageLogFile);

            // Warning, this will delete and recreate your destination folder
            Paths.PrepareDestinationFolder();

            using (Disposable.Timing("All"))
            {
                IndexSolutions(projects);
                FinalizeProjects();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage: HtmlGenerator [/out:<outputdirectory>] <pathtosolution1.csproj|vbproj|sln> [more solutions/projects..]");
        }

        private static void IndexSolutions(IEnumerable<string> solutionFilePaths)
        {
            foreach (var path in solutionFilePaths)
            {
                IndexSolution(new SolutionInfo
                {
                    SlnPath = path,
                }, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private static void IndexSolution(SolutionInfo solutionInfo, HashSet<string> assemblyList)
        {
            using (Disposable.Timing("Generating projects for " + Path.GetFileName(solutionInfo.SlnPath)))
            {
                bool needToCallAgain = false;

                // we need to call several times because it only processes projects one batch at a time
                // to avoid OutOfMemory on really large solutions
                do
                {
                    GC.Collect();
                    var solutionGenerator = new SolutionGenerator(
                        solutionInfo.SlnPath,
                        Paths.SolutionDestinationFolder,
                        solutionInfo.UrlRoot,
                        solutionInfo.MSBuildProperties != null ? solutionInfo.MSBuildProperties.ToImmutableDictionary() : null,
                        new Federation(
                            "http://referencesource.microsoft.com",
                            "http://source.roslyn.io"));
                    needToCallAgain = solutionGenerator.Generate(assemblyList);
                    solutionGenerator.GenerateResultsHtml(assemblyList);
                } while (needToCallAgain);
            }
        }

        private static void FinalizeProjects()
        {
            GenerateLooseFilesProject(Constants.MSBuildFiles, Paths.SolutionDestinationFolder);
            GenerateLooseFilesProject(Constants.TypeScriptFiles, Paths.SolutionDestinationFolder);
            using (Disposable.Timing("Finalizing references"))
            {
                try
                {
                    var solutionFinalizer = new SolutionFinalizer(Paths.SolutionDestinationFolder);
                    solutionFinalizer.FinalizeProjects();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "Failure while finalizing projects");
                }
            }
        }

        private static void GenerateLooseFilesProject(string projectName, string solutionDestinationPath)
        {
            var projectGenerator = new ProjectGenerator(projectName, solutionDestinationPath);
            projectGenerator.GenerateNonProjectFolder();
        }
    }
}
