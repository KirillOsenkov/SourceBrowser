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
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            var projects = new List<string>();
            var properties = new Dictionary<string, string>();
            var emitAssemblyList = false;
            var doNotIncludeReferencedProjects = false;
            var force = false;
            var noBuiltInFederations = false;
            var offlineFederations = new Dictionary<string, string>();
            var federations = new HashSet<string>();
            var serverPathMappings = new Dictionary<string, string>();
            var pluginBlacklist = new List<string>();
            var rootPath = (string)null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/out:"))
                {
                    Paths.SolutionDestinationFolder = Path.GetFullPath(arg.Substring("/out:".Length).StripQuotes());
                    continue;
                }

                if (arg.StartsWith("/serverPath:"))
                {
                    // Allowed forms:
                    // /serverPath:a=b
                    // /serverPath:"a=b" (for backwards compatibility)
                    // /serverPath:"a=1"="b=2"
                    // /serverPath:"a"="b" (for consistency to make it easy to produce a safe form)

                    var match = Regex.Match(
                        arg.Substring("/serverPath:".Length),
                        @"\A(?:
                            # Each side may be quoted or unquoted but may only contain '=' if quoted
                            (?:(?<from>[^""=]*)|""(?<from>[^""]*)"")=(?:(?<to>[^""=]*)|""(?<to>[^""]*)"")
                            |
                            # Backwards compatibility, not advertised as an option because it doesn't allow '=' even though there are quotes
                            ""(?<from>[^""=]*)=(?<to>[^""=]*)""
                        )\Z", RegexOptions.IgnorePatternWhitespace);

                    if (!match.Success)
                    {
                        Log.Write("Server path argument usage: /serverPath:\"path to local repository root\"=\"root URL\"" + Environment.NewLine +
                                  "Quotes are optional if you have no spaces or equals signs but recommended. Paths relative to the local repository root will be appended to the root URL.", ConsoleColor.Red);
                        continue;
                    }

                    serverPathMappings.Add(Path.GetFullPath(match.Groups["from"].Value), match.Groups["to"].Value);
                    continue;
                }

                if (arg == "/force")
                {
                    force = true;
                    continue;
                }

                if (arg.StartsWith("/in:"))
                {
                    string inputPath = arg.Substring("/in:".Length).StripQuotes();
                    try
                    {
                        if (!File.Exists(inputPath))
                        {
                            continue;
                        }

                        string[] paths = File.ReadAllLines(inputPath);
                        foreach (string path in paths)
                        {
                            AddProject(projects, path);
                        }
                    }
                    catch
                    {
                        Log.Write("Invalid argument: " + arg, ConsoleColor.Red);
                    }

                    continue;
                }

                if (arg.StartsWith("/p:"))
                {
                    var match = Regex.Match(arg, "/p:(?<name>[^=]+)=(?<value>.+)");
                    if (match.Success)
                    {
                        var propertyName = match.Groups["name"].Value;
                        var propertyValue = match.Groups["value"].Value;
                        properties.Add(propertyName, propertyValue);
                        continue;
                    }
                }

                if (arg == "/assemblylist")
                {
                    emitAssemblyList = true;
                    continue;
                }

                if (string.Equals(arg, "/donotincludereferencedprojects", StringComparison.OrdinalIgnoreCase))
                {
                    doNotIncludeReferencedProjects = true;
                    continue;
                }

                if (arg == "/nobuiltinfederations")
                {
                    noBuiltInFederations = true;
                    Log.Message("Disabling built-in federations.");
                    continue;
                }

                if (arg.StartsWith("/federation:"))
                {
                    string server = arg.Substring("/federation:".Length);
                    Log.Message($"Adding federation '{server}'.");
                    federations.Add(server);
                    continue;
                }

                if (arg.StartsWith("/offlinefederation:"))
                {
                    var match = Regex.Match(arg, "/offlinefederation:(?<server>[^=]+)=(?<file>.+)");
                    if (match.Success)
                    {
                        var server = match.Groups["server"].Value;
                        var assemblyListFileName = match.Groups["file"].Value;
                        offlineFederations[server] = assemblyListFileName;
                        Log.Message($"Adding federation '{server}' (offline from '{assemblyListFileName}').");
                        continue;
                    }
                    continue;
                }

                if (string.Equals(arg, "/noplugins", StringComparison.OrdinalIgnoreCase))
                {
                    SolutionGenerator.LoadPlugins = false;
                    continue;
                }

                if (string.Equals(arg, "/useplugins", StringComparison.OrdinalIgnoreCase))
                {
                    SolutionGenerator.LoadPlugins = true;
                    continue;
                }

                if (arg.StartsWith("/noplugin:"))
                {
                    pluginBlacklist.Add(arg.Substring("/noplugin:".Length));
                    continue;
                }

                if (arg == "/excludetests")
                {
                    SolutionGenerator.ExcludeTests = true;
                    continue;
                }

                if (arg.StartsWith("/root:", StringComparison.Ordinal))
                {
                    rootPath = Path.GetFullPath(arg.Substring("/root:".Length).StripQuotes());
                    continue;
                }

                try
                {
                    AddProject(projects, arg);
                }
                catch (Exception ex)
                {
                    Log.Write("Exception: " + ex.ToString(), ConsoleColor.Red);
                }
            }

            if (projects.Count == 0)
            {
                PrintUsage();
                return;
            }

            if (rootPath is object)
            {
                foreach (var project in projects)
                {
                    if (!Paths.IsOrContains(rootPath, project))
                    {
                        Log.Exception("If /root is specified, it must be an ancestor folder of all specified projects.", isSevere: true);
                        return;
                    }
                }
            }

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
            Paths.PrepareDestinationFolder(force);

            Paths.SolutionDestinationFolder = Path.Combine(Paths.SolutionDestinationFolder, "index"); //The actual index files need to be written to the "index" subdirectory

            Directory.CreateDirectory(Paths.SolutionDestinationFolder);

            Log.ErrorLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.ErrorLogFile);
            Log.MessageLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.MessageLogFile);

            using (Disposable.Timing("Generating website"))
            {
                var federation = new Federation();

                if (!noBuiltInFederations)
                {
                    federation.AddFederations(Federation.DefaultFederatedIndexUrls);
                }

                federation.AddFederations(federations);

                foreach (var entry in offlineFederations)
                {
                    federation.AddFederation(entry.Key, entry.Value);
                }

                IndexSolutions(projects, properties, federation, serverPathMappings, pluginBlacklist, doNotIncludeReferencedProjects, rootPath);
                FinalizeProjects(emitAssemblyList, federation);
                WebsiteFinalizer.Finalize(websiteDestination, emitAssemblyList, federation);
            }
            Log.Close();
        }

        private static void AddProject(List<string> projects, string path)
        {
            var project = Path.GetFullPath(path);
            if (IsSupportedProject(project))
            {
                projects.Add(project);
            }
            else
            {
                Log.Exception("Project not found or not supported: " + path, isSevere: false);
            }
        }

        private static bool IsSupportedProject(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            return filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".binlog", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
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
            Dictionary<string, string> properties,
            Federation federation,
            Dictionary<string, string> serverPathMappings,
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
