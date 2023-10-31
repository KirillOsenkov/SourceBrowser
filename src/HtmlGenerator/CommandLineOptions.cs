using Microsoft.SourceBrowser.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public sealed class CommandLineOptions
    {
        public CommandLineOptions(
            string solutionDestinationFolder,
            IReadOnlyList<string> projects,
            IReadOnlyDictionary<string, string> properties,
            bool emitAssemblyList,
            bool doNotIncludeReferencedProjects,
            bool force,
            bool noBuiltInFederations,
            IReadOnlyDictionary<string, string> offlineFederations,
            IReadOnlyCollection<string> federations,
            IReadOnlyDictionary<string, string> serverPathMappings,
            IReadOnlyList<string> pluginBlacklist,
            bool loadPlugins,
            bool excludeTests,
            string rootPath,
            bool includeSourceGeneratedDocuments)
        {
            SolutionDestinationFolder = solutionDestinationFolder;
            Projects = projects;
            Properties = properties;
            EmitAssemblyList = emitAssemblyList;
            DoNotIncludeReferencedProjects = doNotIncludeReferencedProjects;
            Force = force;
            NoBuiltInFederations = noBuiltInFederations;
            OfflineFederations = offlineFederations;
            Federations = federations;
            ServerPathMappings = serverPathMappings;
            PluginBlacklist = pluginBlacklist;
            LoadPlugins = loadPlugins;
            ExcludeTests = excludeTests;
            RootPath = rootPath;
            IncludeSourceGeneratedDocuments = includeSourceGeneratedDocuments;
        }

        public string SolutionDestinationFolder { get; }
        public IReadOnlyList<string> Projects { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }
        public bool EmitAssemblyList { get; }
        public bool DoNotIncludeReferencedProjects { get; }
        public bool IncludeSourceGeneratedDocuments { get; }
        public bool Force { get; }
        public bool NoBuiltInFederations { get; }
        public IReadOnlyDictionary<string, string> OfflineFederations { get; }
        public IReadOnlyCollection<string> Federations { get; }
        public IReadOnlyDictionary<string, string> ServerPathMappings { get; }
        public IReadOnlyList<string> PluginBlacklist { get; }
        public bool LoadPlugins { get; }
        public bool ExcludeTests { get; }
        public string RootPath { get; }

        public static CommandLineOptions Parse(params string[] args)
        {
            var solutionDestinationFolder = (string)null;
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
            var loadPlugins = false;
            var excludeTests = false;
            var includeSourceGeneratedDocuments = true;
            var rootPath = (string)null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/out:", StringComparison.Ordinal))
                {
                    solutionDestinationFolder = Path.GetFullPath(arg.Substring("/out:".Length).StripQuotes());
                    continue;
                }

                if (arg.StartsWith("/serverPath:", StringComparison.Ordinal))
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

                if (arg.StartsWith("/in:", StringComparison.Ordinal))
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

                if (arg.StartsWith("/p:", StringComparison.Ordinal))
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

                if (arg.StartsWith("/federation:", StringComparison.Ordinal))
                {
                    string server = arg.Substring("/federation:".Length);
                    Log.Message($"Adding federation '{server}'.");
                    federations.Add(server);
                    continue;
                }

                if (arg.StartsWith("/offlinefederation:", StringComparison.Ordinal))
                {
                    var match = Regex.Match(arg, "/offlinefederation:(?<server>[^=]+)=(?<file>.+)");
                    if (match.Success)
                    {
                        var server = match.Groups["server"].Value;
                        var assemblyListFileName = match.Groups["file"].Value;
                        offlineFederations[server] = assemblyListFileName;
                        Log.Message($"Adding federation '{server}' (offline from '{assemblyListFileName}').");
                    }
                    continue;
                }

                if (string.Equals(arg, "/noplugins", StringComparison.OrdinalIgnoreCase))
                {
                    loadPlugins = false;
                    continue;
                }

                if (string.Equals(arg, "/useplugins", StringComparison.OrdinalIgnoreCase))
                {
                    loadPlugins = true;
                    continue;
                }

                if (arg.StartsWith("/noplugin:", StringComparison.Ordinal))
                {
                    pluginBlacklist.Add(arg.Substring("/noplugin:".Length));
                    continue;
                }

                if (arg == "/excludetests")
                {
                    excludeTests = true;
                    continue;
                }
                
                if (arg == "/excludeSourceGeneratedDocuments")
                {
                    includeSourceGeneratedDocuments = false;
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
                    Log.Write("Exception: " + ex, ConsoleColor.Red);
                }
            }

            if (rootPath is object)
            {
                foreach (var project in projects)
                {
                    if (!Paths.IsOrContains(rootPath, project))
                    {
                        Log.Exception("If /root is specified, it must be an ancestor folder of all specified projects.", isSevere: true);
                        projects.Clear();
                        break;
                    }
                }
            }

            return new CommandLineOptions(
                solutionDestinationFolder,
                projects,
                properties,
                emitAssemblyList,
                doNotIncludeReferencedProjects,
                force,
                noBuiltInFederations,
                offlineFederations,
                federations,
                serverPathMappings,
                pluginBlacklist,
                loadPlugins,
                excludeTests,
                rootPath,
                includeSourceGeneratedDocuments);
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
    }
}
