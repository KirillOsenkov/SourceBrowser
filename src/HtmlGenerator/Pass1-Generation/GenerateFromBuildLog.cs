using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.SourceBrowser.BuildLogParser;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class GenerateFromBuildLog
    {
        public static readonly Dictionary<string, string> AssemblyNameToFilePathMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void GenerateInvocation(CompilerInvocation invocation,
            IReadOnlyDictionary<string, string> serverPathMappings = null,
            HashSet<string> processedAssemblyList = null,
            HashSet<string> assemblyNames = null,
            Folder<ProjectSkeleton> solutionExplorerRoot = null)
        {
            try
            {
                if (invocation.Language == "TypeScript")
                {
                    Log.Write("TypeScript invocation", ConsoleColor.Magenta);
                    var typeScriptGenerator = new TypeScriptSupport();
                    typeScriptGenerator.Generate(invocation.TypeScriptFiles, Paths.SolutionDestinationFolder);
                }
                else if (invocation.ProjectFilePath != "-")
                {
                    Log.Write(invocation.ProjectFilePath, ConsoleColor.Cyan);
                    var solutionGenerator = new SolutionGenerator(
                        invocation.ProjectFilePath,
                        invocation.CommandLineArguments,
                        invocation.OutputAssemblyPath,
                        invocation.SolutionRoot,
                        Paths.SolutionDestinationFolder,
                        invocation.NetworkShare);
                    solutionGenerator.ServerPathMappings = serverPathMappings;
                    solutionGenerator.GlobalAssemblyList = assemblyNames;
                    solutionGenerator.Generate(processedAssemblyList, solutionExplorerRoot);
                }
                else
                {
                    Log.Write(invocation.OutputAssemblyPath, ConsoleColor.Magenta);
                    var solutionGenerator = new SolutionGenerator(
                        invocation.OutputAssemblyPath,
                        Paths.SolutionDestinationFolder);
                    solutionGenerator.Generate();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Generating invocation: " + invocation.ProjectFilePath + " - " + invocation.OutputAssemblyPath);
            }
        }

        public static IEnumerable<CompilerInvocation> GetAllInvocations(string invocationsFile = null)
        {
            var lines = File.ReadAllLines(invocationsFile);
            for (int i = 0; i < lines.Length; i += 3)
            {
                var compilerInvocation = new CompilerInvocation
                {
                    ProjectFilePath = lines[i],
                    OutputAssemblyPath = lines[i + 1],
                    CommandLineArguments = lines[i + 2],
                    NetworkShare = "",
                    SolutionRoot = "",
                };

                yield return compilerInvocation;
            }
        }

        private static IEnumerable<CompilerInvocation> GetInvocationsToProcess()
        {
            var result = new HashSet<CompilerInvocation>();
            HashSet<string> processedAssemblies = Paths.LoadProcessedAssemblies();

            foreach (var compilerInvocation in GetAllInvocations())
            {
                if (!processedAssemblies.Contains(compilerInvocation.AssemblyName) &&
                    !string.IsNullOrEmpty(compilerInvocation.ProjectFilePath) &&
                    !string.IsNullOrEmpty(compilerInvocation.CommandLineArguments))
                {
                    result.Add(compilerInvocation);
                }
            }

            return result;
        }

        public class CompilerInvocation
        {
            public string ProjectFilePath { get; set; }
            public string ProjectDirectory => ProjectFilePath == null ? "" : Path.GetDirectoryName(ProjectFilePath);
            public string OutputAssemblyPath { get; set; }
            public string CommandLineArguments { get; set; }
            public string NetworkShare { get; set; }
            public string SolutionRoot { get; set; }
            public IEnumerable<string> TypeScriptFiles { get; set; }

            public string AssemblyName
            {
                get
                {
                    return Path.GetFileNameWithoutExtension(OutputAssemblyPath);
                }
            }

            private string language;
            public string Language
            {
                get
                {
                    if (language == null)
                    {
                        if (ProjectFilePath == null && TypeScriptFiles != null)
                        {
                            language = "TypeScript";
                        }
                        else if (".vbproj".Equals(Path.GetExtension(ProjectFilePath), StringComparison.OrdinalIgnoreCase))
                        {
                            language = "Visual Basic";
                        }
                        else
                        {
                            language = "C#";
                        }
                    }

                    return language;
                }

                set
                {
                    language = value;
                }
            }

            private string[] GetCommandLineArguments()
            {
                return CommandLineParser.SplitCommandLineIntoArguments(CommandLineArguments, removeHashComments: false).ToArray();
            }

            private CommandLineArguments parsed;
            public CommandLineArguments Parsed
            {
                get
                {
                    if (parsed != null)
                    {
                        return parsed;
                    }

                    var sdkDirectory = RuntimeEnvironment.GetRuntimeDirectory();
                    var args = GetCommandLineArguments();

                    if (Language == LanguageNames.CSharp)
                    {
                        parsed = CSharpCommandLineParser.Default.Parse(args, ProjectDirectory, sdkDirectory);
                    }
                    else
                    {
                        parsed = VisualBasicCommandLineParser.Default.Parse(args, ProjectDirectory, sdkDirectory);
                    }

                    return parsed;
                }
            }
        }
    }
}
