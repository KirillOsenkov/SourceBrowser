using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var options = CommandLineOptions.Parse(args);

            if (options.Projects.Count == 0)
            {
                PrintUsage();
                return;
            }

            Paths.SolutionDestinationFolder = options.SolutionDestinationFolder;
            SolutionGenerator.LoadPlugins = options.LoadPlugins;
            SolutionGenerator.ExcludeTests = options.ExcludeTests;

            AssertTraceListener.Register();
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceExceptionHandler.HandleFirstChanceException;

            // This loads the real MSBuild from the toolset so that all targets and SDKs can be found
            // as if a real build is happening
            MSBuildLocator.RegisterDefaults();

            if (Paths.SolutionDestinationFolder == null)
            {
                Paths.SolutionDestinationFolder = Path.Combine(Microsoft.SourceBrowser.Common.Paths.BaseAppFolder, "index");
            }

            var websiteDestination = Paths.SolutionDestinationFolder;

            // Warning, this will delete and recreate your destination folder
            Paths.PrepareDestinationFolder(options.Force);

            Paths.SolutionDestinationFolder = Path.Combine(Paths.SolutionDestinationFolder, "index"); //The actual index files need to be written to the "index" subdirectory

            Directory.CreateDirectory(Paths.SolutionDestinationFolder);

            Log.ErrorLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.ErrorLogFile);
            Log.MessageLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.MessageLogFile);

            using (Disposable.Timing("Generating website"))
            {
                var federation = new Federation();

                if (!options.NoBuiltInFederations)
                {
                    federation.AddFederations(Federation.DefaultFederatedIndexUrls);
                }

                federation.AddFederations(options.Federations);

                foreach (var entry in options.OfflineFederations)
                {
                    federation.AddFederation(entry.Key, entry.Value);
                }

                IndexSolutions(options.Projects, options.Properties, federation, options.ServerPathMappings, options.PluginBlacklist, options.DoNotIncludeReferencedProjects, options.RootPath);
                FinalizeProjects(options.EmitAssemblyList, federation);
                WebsiteFinalizer.Finalize(websiteDestination, options.EmitAssemblyList, federation);
            }
            Log.Close();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: HtmlGenerator "
                + "[/out:<outputdirectory>] "
                + "[/force] "
                + "[/useplugins] "
                + "[/noplugins] "
                + "[/noplugin:Git] "
                + "<pathtosolution1.csproj|vbproj|sln|binlog|buildlog|dll|exe> [more solutions/projects..] "
                + "[/root:<root folder to enable relative .sln folders>] "
                + "[/in:<filecontaingprojectlist>] "
                + "[/nobuiltinfederations] "
                + "[/offlinefederation:server=assemblyListFile] "
                + "[/assemblylist]"
                + "[/excludetests]" +
                "" +
                "Plugins are now off by default.");
        }

        private static readonly Folder<ProjectSkeleton> mergedSolutionExplorerRoot = new Folder<ProjectSkeleton>();

        private static IEnumerable<string> GetAssemblyNames(string filePath)
        {
            if (filePath.EndsWith(".binlog", System.StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".buildlog", System.StringComparison.OrdinalIgnoreCase))
            {
                var invocations = BinLogCompilerInvocationsReader.ExtractInvocations(filePath);
                return invocations.Select(i => Path.GetFileNameWithoutExtension(i.Parsed.OutputFileName)).ToArray();
            }

            return AssemblyNameExtractor.GetAssemblyNames(filePath);
        }

        private static void IndexSolutions(
            IEnumerable<string> solutionFilePaths,
            IReadOnlyDictionary<string, string> properties,
            Federation federation,
            IReadOnlyDictionary<string, string> serverPathMappings,
            IEnumerable<string> pluginBlacklist,
            bool doNotIncludeReferencedProjects = false,
            string rootPath = null)
        {
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in solutionFilePaths)
            {
                using (Disposable.Timing("Reading assembly names from " + path))
                {
                    foreach (var assemblyName in GetAssemblyNames(path))
                    {
                        assemblyNames.Add(assemblyName);
                    }
                }
            }

            var processedAssemblyList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in solutionFilePaths)
            {
                var solutionFolder = mergedSolutionExplorerRoot;

                if (rootPath is object)
                {
                    var relativePath = Paths.MakeRelativeToFolder(Path.GetDirectoryName(path), rootPath);
                    var segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var segment in segments)
                    {
                        solutionFolder = solutionFolder.GetOrCreateFolder(segment);
                    }
                }

                using (Disposable.Timing("Generating " + path))
                {
                    if (path.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase))
                    {
                        var invocations = BinLogCompilerInvocationsReader.ExtractInvocations(path);
                        foreach (var invocation in invocations)
                        {
                            GenerateFromBuildLog.GenerateInvocation(
                                invocation,
                                serverPathMappings,
                                processedAssemblyList,
                                assemblyNames,
                                solutionFolder);
                        }
                        
                        continue;
                    }

                    using (var solutionGenerator = new SolutionGenerator(
                        path,
                        Paths.SolutionDestinationFolder,
                        properties: properties.ToImmutableDictionary(),
                        federation: federation,
                        serverPathMappings: serverPathMappings,
                        pluginBlacklist: pluginBlacklist,
                        doNotIncludeReferencedProjects: doNotIncludeReferencedProjects))
                    {
                        solutionGenerator.GlobalAssemblyList = assemblyNames;
                        solutionGenerator.Generate(processedAssemblyList, solutionFolder);
                    }
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private static void FinalizeProjects(bool emitAssemblyList, Federation federation)
        {
            GenerateLooseFilesProject(Constants.MSBuildFiles, Paths.SolutionDestinationFolder);
            GenerateLooseFilesProject(Constants.TypeScriptFiles, Paths.SolutionDestinationFolder);
            using (Disposable.Timing("Finalizing references"))
            {
                try
                {
                    var solutionFinalizer = new SolutionFinalizer(Paths.SolutionDestinationFolder);
                    solutionFinalizer.FinalizeProjects(emitAssemblyList, federation, mergedSolutionExplorerRoot);
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

    internal static class WebsiteFinalizer
    {
        public static void Finalize(string destinationFolder, bool emitAssemblyList, Federation federation)
        {
            string sourcePath = Assembly.GetEntryAssembly().Location;
            sourcePath = Path.GetDirectoryName(sourcePath);
            string basePath = sourcePath;
            sourcePath = Path.Combine(sourcePath, "Web");
            if (!Directory.Exists(sourcePath))
            {
                return;
            }

            sourcePath = Path.GetFullPath(sourcePath);
            FileUtilities.CopyDirectory(sourcePath, destinationFolder);

            StampOverviewHtmlWithDate(destinationFolder);

            if (emitAssemblyList)
            {
                ToggleSolutionExplorerOff(destinationFolder);
            }

            SetExternalUrlMap(destinationFolder, federation);
        }

        private static void StampOverviewHtmlWithDate(string destinationFolder)
        {
            var source = Path.Combine(destinationFolder, "wwwroot", "overview.html");
            var dst = Path.Combine(destinationFolder, "index", "overview.html");
            if (File.Exists(source))
            {
                var text = File.ReadAllText(source);
                text = StampOverviewHtmlText(text);
                File.WriteAllText(dst, text);
            }
        }

        private static string StampOverviewHtmlText(string text)
        {
            return text.Replace("$(Date)", DateTime.Today.ToString("MMMM d", CultureInfo.InvariantCulture));
        }

        private static void ToggleSolutionExplorerOff(string destinationFolder)
        {
            var source = Path.Combine(destinationFolder, "wwwroot/scripts.js");
            var dst = Path.Combine(destinationFolder, "index/scripts.js");
            if (File.Exists(source))
            {
                var text = File.ReadAllText(source);
                text = text.Replace("/*USE_SOLUTION_EXPLORER*/true/*USE_SOLUTION_EXPLORER*/", "false");
                File.WriteAllText(dst, text);
            }
        }

        private static void SetExternalUrlMap(string destinationFolder, Federation federation)
        {
            var source = Path.Combine(destinationFolder, "wwwroot/scripts.js");
            var dst = Path.Combine(destinationFolder, "index/scripts.js");
            if (File.Exists(source))
            {
                var sb = new StringBuilder();
                foreach (var server in federation.GetServers())
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append("\"");
                    sb.Append(server);
                    sb.Append("\"");
                }

                if (sb.Length > 0)
                {
                    var text = File.ReadAllText(source);
                    text = Regex.Replace(text, @"/\*EXTERNAL_URL_MAP\*/.*/\*EXTERNAL_URL_MAP\*/", sb.ToString());
                    File.WriteAllText(dst, text);
                }
            }
        }
    }
}
