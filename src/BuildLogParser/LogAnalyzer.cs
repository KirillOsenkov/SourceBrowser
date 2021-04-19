using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.BuildLogParser
{
    public class LogAnalyzer
    {
        public Dictionary<string, string> intermediateAssemblyPathToOutputAssemblyPathMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MultiDictionary<string, string> assemblyNameToProjectFilePathsMap =
            new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> projectFilePathToAssemblyNameMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private HashSet<CompilerInvocation> finalInvocations =
            new HashSet<CompilerInvocation>(CompilerInvocation.Comparer);

        public static HashSet<string> cacheOfKnownExistingBinaries =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public readonly Dictionary<string, List<string>> ambiguousFinalDestinations =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static MultiDictionary<string, CompilerInvocation> ambiguousInvocations =
            new MultiDictionary<string, CompilerInvocation>(StringComparer.OrdinalIgnoreCase, null);

        public static MultiDictionary<string, CompilerInvocation> nonExistingReferencesToCompilerInvocationMap =
            new MultiDictionary<string, CompilerInvocation>();

        public LogAnalyzer()
        {
            cacheOfKnownExistingBinaries.Clear();
            nonExistingReferencesToCompilerInvocationMap.Clear();
        }

        public static void DisposeStatics()
        {
            cacheOfKnownExistingBinaries.Clear();
            cacheOfKnownExistingBinaries = null;
            ambiguousInvocations.Clear();
            ambiguousInvocations = null;
            nonExistingReferencesToCompilerInvocationMap.Clear();
            nonExistingReferencesToCompilerInvocationMap = null;
        }

        public static IEnumerable<CompilerInvocation> GetInvocations(string logFilePath)
        {
            return GetInvocations(logFiles: new[] { logFilePath });
        }

        public static IEnumerable<CompilerInvocation> GetInvocations(Options options = null, IEnumerable<string> logFiles = null)
        {
            var analyzer = new LogAnalyzer();
            var invocationBuckets = new Dictionary<string, IEnumerable<CompilerInvocation>>(StringComparer.OrdinalIgnoreCase);
            using (Disposable.Timing("Analyzing log files"))
            {
#if true
                Parallel.ForEach(logFiles, logFile =>
                {
                    var set = analyzer.AnalyzeLogFile(logFile);
                    lock (invocationBuckets)
                    {
                        invocationBuckets.Add(Path.GetFileNameWithoutExtension(logFile), set);
                    }
                });
#else
                foreach (var logFile in logFiles)
                {
                    var set = analyzer.AnalyzeLogFile(logFile);
                    lock (invocationBuckets)
                    {
                        invocationBuckets.Add(Path.GetFileNameWithoutExtension(logFile), set);
                    }
                }
#endif
            }

            var buckets = invocationBuckets.OrderBy(kvp => kvp.Key).ToArray();
            foreach (var bucket in buckets)
            {
                foreach (var invocation in bucket.Value)
                {
                    analyzer.SelectFinalInvocation(invocation);
                }
            }

            FixOutputPaths(analyzer);

            if (options != null && options.SanityCheck)
            {
                using (Disposable.Timing("Sanity check"))
                {
                    SanityCheck(analyzer, options);
                }
            }

            return analyzer.Invocations;
        }

        public class Options
        {
            public bool CheckForOrphans = true;
            public bool CheckForMissingOutputBinary = true;
            public bool CheckForNonExistingReferences = false;
            public bool SanityCheck = true;
        }

        private static void FixOutputPaths(LogAnalyzer analyzer)
        {
            foreach (var invocation in analyzer.Invocations)
            {
                // TypeScript
                if (invocation.OutputAssemblyPath == null)
                {
                    continue;
                }

                if (invocation.OutputAssemblyPath.StartsWith(".", StringComparison.Ordinal) || invocation.OutputAssemblyPath.StartsWith("\\", StringComparison.Ordinal))
                {
                    invocation.OutputAssemblyPath = Path.GetFullPath(
                        Path.Combine(
                            Path.GetDirectoryName(invocation.ProjectFilePath),
                            invocation.OutputAssemblyPath));
                }

                invocation.OutputAssemblyPath = Path.GetFullPath(invocation.OutputAssemblyPath);
                invocation.ProjectFilePath = Path.GetFullPath(invocation.ProjectFilePath);
            }
        }

        private static void SanityCheck(LogAnalyzer analyzer, Options options = null)
        {
            var dupes = analyzer.Invocations
                .Where(i => i.AssemblyName != null)
                .GroupBy(i => i.AssemblyName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).ToArray();
            if (dupes.Any())
            {
                foreach (var dupe in dupes)
                {
                    Log.Exception("=== Dupes: " + dupe.Key);
                    foreach (var value in dupe)
                    {
                        Log.Exception(value.ToString());
                    }
                }
            }

            var ambiguousProjects = analyzer.assemblyNameToProjectFilePathsMap.Where(kvp => kvp.Value.Count > 1).ToArray();
            if (ambiguousProjects.Any())
            {
                foreach (var ambiguousProject in ambiguousProjects)
                {
                    Log.Exception("Multiple projects for the same assembly name: " + ambiguousProject.Key);
                    foreach (var value in ambiguousProject.Value)
                    {
                        Log.Exception(value);
                    }
                }
            }

            var ambiguousIntermediatePaths = analyzer.intermediateAssemblyPathToOutputAssemblyPathMap
                .GroupBy(kvp => Path.GetFileNameWithoutExtension(kvp.Key), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .OrderByDescending(g => g.Count());
            if (ambiguousIntermediatePaths.Any())
            {
            }

            if (analyzer.ambiguousFinalDestinations.Any())
            {
            }

            foreach (var assemblyName in ambiguousInvocations.Keys.ToArray())
            {
                var values = ambiguousInvocations[assemblyName].ToArray();
                bool shouldRemove = true;
                for (int i = 1; i < values.Length; i++)
                {
                    if (!values[i].OutputAssemblyPath.Equals(values[0].OutputAssemblyPath))
                    {
                        // if entries in a bucket are different, we keep the bucket to report it at the end
                        shouldRemove = false;
                        break;
                    }
                }

                // remove buckets where all entries are exactly the same
                if (shouldRemove)
                {
                    ambiguousInvocations.Remove(assemblyName);
                }
            }

            if (ambiguousInvocations.Any())
            {
                foreach (var ambiguousInvocation in ambiguousInvocations)
                {
                    Log.Exception("Ambiguous invocations for the same assembly name: " + ambiguousInvocation.Key);
                    foreach (var value in ambiguousInvocation.Value)
                    {
                        Log.Exception(value.ToString());
                    }
                }
            }

            if (options.CheckForNonExistingReferences)
            {
                DumpNonExistingReferences();
            }
        }

        private static void DumpNonExistingReferences()
        {
            foreach (var kvp in nonExistingReferencesToCompilerInvocationMap)
            {
                Log.Exception(string.Format("Non existing reference {0} in {1} invocations", kvp.Key, kvp.Value.Count));
            }
        }

        public static void SanityCheckAfterMetadataAsSource(IEnumerable<CompilerInvocation> invocations, Options options = null)
        {
            var allInvocationAssemblyNames = new HashSet<string>(
                invocations.Select(i => i.AssemblyName),
                StringComparer.OrdinalIgnoreCase);
            var allReferenceAssemblyNames = new HashSet<string>(
                invocations
                .SelectMany(i => i.ReferencedBinaries)
                .Select(b => Path.GetFileNameWithoutExtension(b)), StringComparer.OrdinalIgnoreCase);
            allReferenceAssemblyNames.ExceptWith(allInvocationAssemblyNames);

            //var invocationsWithUnindexedReferences = analyzer.Invocations
            //    .Where(i => i.ReferencedBinaries.Any(b => !allInvocationAssemblyNames.Contains(Path.GetFileNameWithoutExtension(b))))
            //    .Select(i => Tuple.Create(i, i.ReferencedBinaries.Where(b => !allInvocationAssemblyNames.Contains(Path.GetFileNameWithoutExtension(b))).ToArray()))
            //    .ToArray();
            //if (invocationsWithUnindexedReferences.Length > 0)
            //{
            //    throw new InvalidOperationException("Invocation with unindexed references: " + invocationsWithUnindexedReferences.First().Item1.ProjectFilePath);
            //}

            if (options == null || options.CheckForMissingOutputBinary)
            {
                var invocationsWhereBinaryDoesntExist = invocations.Where(
                    i => !File.Exists(i.OutputAssemblyPath)).ToArray();
                if (invocationsWhereBinaryDoesntExist.Length > 0)
                {
                    throw new InvalidOperationException("Invocation where output binary doesn't exist: " + invocationsWhereBinaryDoesntExist.First().OutputAssemblyPath);
                }
            }
        }

        public IEnumerable<CompilerInvocation> AnalyzeLogFile(string logFile)
        {
            Log.Write(logFile);
            return ProcessLogFileLines(logFile);
        }

        private IEnumerable<CompilerInvocation> ProcessLogFileLines(string logFile)
        {
            var invocations = new HashSet<CompilerInvocation>();

            var lines = File.ReadLines(logFile);
            foreach (var currentLine in lines)
            {
                string line = currentLine;
                line = line.Trim();

                if (ProcessCopyingFileFrom(line))
                {
                    continue;
                }

                if (ProcessDoneBuildingProject(line))
                {
                    continue;
                }

                if (ProcessInvocation(line, i => invocations.Add(i)))
                {
                    continue;
                }
            }

            return invocations;
        }

        private bool ProcessCopyingFileFrom(string line)
        {
            if (line.Contains("Copying file from \"") || line.Contains("Moving file from \""))
            {
                int from = line.IndexOf('\\') + 1;
                int to = line.IndexOf("\" to \"", StringComparison.Ordinal);
                string intermediateAssemblyPath = line.Substring(from, to - from);
                if (intermediateAssemblyPath.Contains(".."))
                {
                    intermediateAssemblyPath = Path.GetFullPath(intermediateAssemblyPath);
                }

                string outputAssemblyPath = line.Substring(to + 6, line.Length - to - 8);

                if (!outputAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !outputAssemblyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !outputAssemblyPath.EndsWith(".netmodule", StringComparison.OrdinalIgnoreCase))
                {
                    // not even an assembly, we don't care about it
                    return true;
                }

                var assemblyName = Path.GetFileNameWithoutExtension(outputAssemblyPath);

                if ((outputAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    outputAssemblyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    outputAssemblyPath.EndsWith(".module", StringComparison.OrdinalIgnoreCase) ||
                    outputAssemblyPath.EndsWith(".netmodule", StringComparison.OrdinalIgnoreCase)) &&
                    !outputAssemblyPath.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) &&
                    !outputAssemblyPath.EndsWith(".XmlSerializers.dll", StringComparison.OrdinalIgnoreCase))
                {
                    int tempPlacingIndex = intermediateAssemblyPath.IndexOf(@"\\TempPlacing", StringComparison.Ordinal);
                    if (tempPlacingIndex > -1)
                    {
                        intermediateAssemblyPath = intermediateAssemblyPath.Remove(tempPlacingIndex, 13);
                    }

                    intermediateAssemblyPath = intermediateAssemblyPath.Replace(@"\\", @"\");
                    lock (this.intermediateAssemblyPathToOutputAssemblyPathMap)
                    {
                        if (!this.intermediateAssemblyPathToOutputAssemblyPathMap.TryGetValue(intermediateAssemblyPath, out string existing))
                        {
                            this.intermediateAssemblyPathToOutputAssemblyPathMap[intermediateAssemblyPath] = outputAssemblyPath;
                        }
                        else if (!string.Equals(existing, outputAssemblyPath))
                        {
                            if (!ambiguousFinalDestinations.TryGetValue(assemblyName, out List<string> bucket))
                            {
                                bucket = new List<string>();
                                ambiguousFinalDestinations.Add(assemblyName, bucket);
                                bucket.Add(existing);
                            }

                            bucket.Add(outputAssemblyPath);

                            if (outputAssemblyPath.Length < existing.Length)
                            {
                                this.intermediateAssemblyPathToOutputAssemblyPathMap[intermediateAssemblyPath] = outputAssemblyPath;
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }

        private bool ProcessDoneBuildingProject(string line)
        {
            var doneBuildingProject = line.IndexOf("Done Building Project", StringComparison.Ordinal);
            if (doneBuildingProject > -1)
            {
                string projectFilePath = ExtractProjectFilePath(line, doneBuildingProject);

                if (!File.Exists(projectFilePath))
                {
                    Log.Message("Project doesn't exist: " + projectFilePath);
                    return true;
                }

                string outputAssemblyName = GetAssemblyNameFromProject(projectFilePath);
                if (string.IsNullOrWhiteSpace(outputAssemblyName))
                {
                    return true;
                }

                lock (this.projectFilePathToAssemblyNameMap)
                {
                    if (!this.projectFilePathToAssemblyNameMap.ContainsKey(projectFilePath))
                    {
                        lock (this.assemblyNameToProjectFilePathsMap)
                        {
                            this.assemblyNameToProjectFilePathsMap.Add(outputAssemblyName, projectFilePath);
                        }

                        this.projectFilePathToAssemblyNameMap[projectFilePath] = outputAssemblyName;
                    }
                }

                return true;
            }

            return false;
        }

        private string GetAssemblyNameFromProject(string projectFilePath)
        {
            string assemblyName = null;

            lock (projectFilePathToAssemblyNameCache)
            {
                if (projectFilePathToAssemblyNameCache.TryGetValue(projectFilePath, out assemblyName))
                {
                    return assemblyName;
                }
            }

            assemblyName = AssemblyNameExtractor.GetAssemblyNameFromProject(projectFilePath);

            if (assemblyName == null)
            {
                Log.Exception("Couldn't extract AssemblyName from project: " + projectFilePath);
            }
            else
            {
                lock (projectFilePathToAssemblyNameCache)
                {
                    projectFilePathToAssemblyNameCache[projectFilePath] = assemblyName;
                }
            }

            return assemblyName;
        }

        private bool ProcessInvocation(string line, Action<CompilerInvocation> collector)
        {
            bool csc = false;
            bool vbc = false;
            bool tsc = false;

            csc = line.IndexOf("csc", StringComparison.OrdinalIgnoreCase) != -1;
            if (csc &&
                (line.IndexOf(@"\csc.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\csc2.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\rcsc.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\rcsc2.exe ", StringComparison.OrdinalIgnoreCase) != -1))
            {
                AddInvocation(line, collector);
                return true;
            }

            vbc = line.IndexOf("vbc", StringComparison.OrdinalIgnoreCase) != -1;
            if (vbc &&
                (line.IndexOf(@"\vbc.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\vbc2.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\rvbc.exe ", StringComparison.OrdinalIgnoreCase) != -1 ||
                 line.IndexOf(@"\rvbc2.exe ", StringComparison.OrdinalIgnoreCase) != -1))
            {
                AddInvocation(line, collector);
                return true;
            }

            tsc = line.IndexOf("\tsc.exe ", StringComparison.OrdinalIgnoreCase) != -1;
            if (tsc)
            {
                AddTypeScriptInvocation(line, collector);
                return true;
            }

            return false;
        }

        private void AddTypeScriptInvocation(string line, Action<CompilerInvocation> collector)
        {
            var invocation = CompilerInvocation.CreateTypeScript(line);
            collector(invocation);
        }

        private static void AddInvocation(string line, Action<CompilerInvocation> collector)
        {
            var invocation = new CompilerInvocation(line);
            collector(invocation);
            lock (cacheOfKnownExistingBinaries)
            {
                foreach (var reference in invocation.ReferencedBinaries)
                {
                    cacheOfKnownExistingBinaries.Add(reference);
                }
            }
        }

        private void AssignProjectFilePath(CompilerInvocation invocation)
        {
            HashSet<string> projectFilePaths = null;
            lock (this.assemblyNameToProjectFilePathsMap)
            {
                if (this.assemblyNameToProjectFilePathsMap.TryGetValue(invocation.AssemblyName, out projectFilePaths))
                {
                    invocation.ProjectFilePath = projectFilePaths.First();
                }
            }
        }

        private void AssignOutputAssemblyPath(CompilerInvocation invocation)
        {
            string outputAssemblyFilePath = null;

            lock (this.intermediateAssemblyPathToOutputAssemblyPathMap)
            {
                if (this.intermediateAssemblyPathToOutputAssemblyPathMap.TryGetValue(invocation.IntermediateAssemblyPath, out outputAssemblyFilePath))
                {
                    outputAssemblyFilePath = Path.GetFullPath(outputAssemblyFilePath);
                    invocation.OutputAssemblyPath = outputAssemblyFilePath;
                    var realAssemblyName = Path.GetFileNameWithoutExtension(outputAssemblyFilePath);
                    if (invocation.AssemblyName != realAssemblyName)
                    {
                        invocation.AssemblyName = realAssemblyName;
                    }
                }
                else
                {
                    invocation.UnknownIntermediatePath = true;
                }
            }
        }

        private static Dictionary<string, string> projectFilePathToAssemblyNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal void SelectFinalInvocation(CompilerInvocation invocation)
        {
            if (invocation.Language == "TypeScript")
            {
                lock (finalInvocations)
                {
                    finalInvocations.Add(invocation);
                }

                return;
            }

            AssignProjectFilePath(invocation);
            AssignOutputAssemblyPath(invocation);

            if (invocation.UnknownIntermediatePath)
            {
                Log.Exception("Unknown intermediate path: " + invocation.IntermediateAssemblyPath);
            }

            lock (finalInvocations)
            {
                if (finalInvocations.Contains(invocation))
                {
                    if (!ambiguousInvocations.ContainsKey(invocation.AssemblyName))
                    {
                        var existing = finalInvocations.First(i => StringComparer.OrdinalIgnoreCase.Equals(i.AssemblyName, invocation.AssemblyName));
                        ambiguousInvocations.Add(existing.AssemblyName, existing);
                    }

                    ambiguousInvocations.Add(invocation.AssemblyName, invocation);
                }

                finalInvocations.Add(invocation);
            }
        }

        private string ExtractProjectFilePath(string line, int start)
        {
            start += 23;
            int end = line.IndexOf('"', start + 1);
            string projectFilePath = line.Substring(start, end - start);
            return projectFilePath;
        }

        public static void WriteInvocationsToFile(IEnumerable<CompilerInvocation> invocations, string fileName)
        {
            var projects = invocations
                .Where(i => i.ProjectFilePath != null && i.ProjectFilePath.Length >= 3)
                .Select(i => i.ProjectFilePath.Substring(3))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var sortedInvocations = invocations
                .OrderBy(i => i.AssemblyName, StringComparer.OrdinalIgnoreCase);

            var assemblies = sortedInvocations
                .Select(i => i.AssemblyName);

            var assemblyPaths = GetAssemblyPaths(sortedInvocations);
            assemblyPaths = assemblyPaths
                .OrderBy(s => Path.GetFileName(s));

            var lines = sortedInvocations
                .SelectMany(i => new[] { i.ProjectFilePath ?? "-", i.OutputAssemblyPath, i.CommandLine });

            var path = Path.GetDirectoryName(fileName);
            var assembliesTxt = Path.Combine(path, "Assemblies.txt");
            var projectsTxt = Path.Combine(path, "Projects.txt");
            var assemblyPathsTxt = Path.Combine(path, "AssemblyPaths.txt");

            File.WriteAllLines(fileName, lines);
            File.WriteAllLines(projectsTxt, projects);
            File.WriteAllLines(assembliesTxt, assemblies);
            File.WriteAllLines(assemblyPathsTxt, assemblyPaths);
        }

        private static IEnumerable<string> GetAssemblyPaths(IEnumerable<CompilerInvocation> invocations)
        {
            var assemblyNameToFilePathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var invocation in invocations)
            {
                AddAssemblyToMap(assemblyNameToFilePathMap, invocation.OutputAssemblyPath);

                foreach (var reference in invocation.ReferencedBinaries)
                {
                    AddAssemblyToMap(assemblyNameToFilePathMap, reference);
                }
            }

            return assemblyNameToFilePathMap.Values;
        }

        private static void AddAssemblyToMap(Dictionary<string, string> assemblyNameToFilePathMap, string reference)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(reference);
            if (!assemblyNameToFilePathMap.TryGetValue(assemblyName, out string existing) ||
                existing.Length > reference.Length ||
                (existing.Length == reference.Length && string.Compare(existing, reference, StringComparison.Ordinal) < 0))
            {
                assemblyNameToFilePathMap[assemblyName] = reference;
            }
        }

        public IEnumerable<CompilerInvocation> Invocations
        {
            get
            {
                return this.finalInvocations;
            }
        }

        public static void AddMetadataAsSourceAssemblies(List<CompilerInvocation> invocations)
        {
            var indexedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var invocation in invocations)
            {
                indexedAssemblies.Add(invocation.AssemblyName);
            }

            var notIndexedAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binary in invocations.SelectMany(i => i.ReferencedBinaries))
            {
                var assemblyName = Path.GetFileNameWithoutExtension(binary);
                if (!indexedAssemblies.Contains(assemblyName) && ShouldIncludeNotIndexedAssembly(binary))
                {
                    if (!notIndexedAssemblies.TryGetValue(assemblyName, out string existing) ||
                        binary.Length < existing.Length ||
                        (binary.Length == existing.Length && string.Compare(binary, existing, StringComparison.Ordinal) > 0))
                    {
                        // make sure we always prefer the .dll that has shortest file path on disk
                        // Not only to disambiguate in a stable fashion, but also it's a good heuristic
                        // Shorter paths seem to be more widely used and are less obscure.
                        notIndexedAssemblies[assemblyName] = binary;
                    }
                }
            }

            foreach (var notIndexedAssembly in notIndexedAssemblies)
            {
                var invocation = new CompilerInvocation()
                {
                    AssemblyName = notIndexedAssembly.Key,
                    CommandLine = "-",
                    OutputAssemblyPath = notIndexedAssembly.Value,
                    ProjectFilePath = "-"
                };
                invocations.Add(invocation);
            }
        }

        private static bool ShouldIncludeNotIndexedAssembly(string binary) => File.Exists(binary);

        public static void AddNonExistingReference(
            CompilerInvocation compilerInvocation,
            string nonExistingReferenceFilePath)
        {
            lock (nonExistingReferencesToCompilerInvocationMap)
            {
                nonExistingReferencesToCompilerInvocationMap.Add(
                        nonExistingReferenceFilePath,
                        compilerInvocation);
            }
        }
    }
}