using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class IndexLoader
    {
        public static void ReadIndex(Index index, string rootPath)
        {
            index.indexFinishedPopulating = false;

            bool retry = false;
            do
            {
                try
                {
                    index.ClearAll();
                    ReadFilesCore(index, rootPath);
                    retry = false;
                    index.loadErrorMessage = null;
                }
                catch (Exception ex)
                {
                    index.loadErrorMessage = ex.ToString();
                    retry = true;
                    Thread.Sleep(10000);
                }
            }
            while (retry);

            index.indexFinishedPopulating = true;

            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            }
        }

        private static void ReadFilesCore(Index index, string rootPath)
        {
            using (Measure.Time("Read index"))
            {
                using (Measure.Time("Read project info"))
                {
                    ReadProjectInfo(
                        rootPath,
                        index.assemblies,
                        index.projects,
                        index.projectToAssemblyIndexMap);
                }

                using (Measure.Time("Read declared symbols"))
                {
                    ReadDeclaredSymbols(
                        rootPath,
                        index.symbols,
                        index.assemblies,
                        index.projects,
                        ref index.huffman,
                        ref index.progress);
                }

                ReadGuids(rootPath, index.guids);
                ReadMSBuildProperties(rootPath, index.msbuildProperties);
                ReadMSBuildItems(rootPath, index.msbuildItems);
                ReadMSBuildTargets(rootPath, index.msbuildTargets);
                ReadMSBuildTasks(rootPath, index.msbuildTasks);
            }
        }

        private static void ReadMSBuildProperties(string rootPath, List<string> msbuildProperties)
        {
            var msbuildPropertiesFolder = Path.Combine(rootPath, Constants.MSBuildPropertiesAssembly, Constants.ReferencesFileName);
            if (!Directory.Exists(msbuildPropertiesFolder))
            {
                return;
            }

            foreach (var msbuildPropertiesFile in Directory.GetFiles(msbuildPropertiesFolder))
            {
                var propertyName = Path.GetFileNameWithoutExtension(msbuildPropertiesFile);
                msbuildProperties.Add(propertyName);
            }

            msbuildProperties.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildItems(string rootPath, List<string> msbuildItems)
        {
            var msbuildItemsFolder = Path.Combine(rootPath, Constants.MSBuildItemsAssembly, Constants.ReferencesFileName);
            if (!Directory.Exists(msbuildItemsFolder))
            {
                return;
            }

            foreach (var msbuildPropertiesFile in Directory.GetFiles(msbuildItemsFolder))
            {
                var propertyName = Path.GetFileNameWithoutExtension(msbuildPropertiesFile);
                msbuildItems.Add(propertyName);
            }

            msbuildItems.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildTargets(string rootPath, List<string> msbuildTargets)
        {
            var msbuildTargetsFolder = Path.Combine(rootPath, Constants.MSBuildTargetsAssembly, Constants.ReferencesFileName);
            if (!Directory.Exists(msbuildTargetsFolder))
            {
                return;
            }

            foreach (var msbuildTargetsFile in Directory.GetFiles(msbuildTargetsFolder))
            {
                var targetName = Path.GetFileNameWithoutExtension(msbuildTargetsFile);
                msbuildTargets.Add(targetName);
            }

            msbuildTargets.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildTasks(string rootPath, List<string> msbuildTasks)
        {
            var msbuildTasksFolder = Path.Combine(rootPath, Constants.MSBuildTasksAssembly, Constants.ReferencesFileName);
            if (!Directory.Exists(msbuildTasksFolder))
            {
                return;
            }

            foreach (var msbuildTasksFile in Directory.GetFiles(msbuildTasksFolder))
            {
                var taskName = Path.GetFileNameWithoutExtension(msbuildTasksFile);
                msbuildTasks.Add(taskName);
            }

            msbuildTasks.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadGuids(string rootPath, List<string> guids)
        {
            var guidsFolder = Path.Combine(rootPath, Constants.GuidAssembly, Constants.ReferencesFileName);
            if (!Directory.Exists(guidsFolder))
            {
                return;
            }

            foreach (var guidFile in Directory.GetFiles(guidsFolder))
            {
                var guid = Path.GetFileNameWithoutExtension(guidFile);
                guids.Add(guid);
            }

            guids.Sort();
        }

        public static void ReadProjectInfo(
            string rootPath,
            List<AssemblyInfo> assemblies,
            List<string> projects,
            Dictionary<string, int> projectToAssemblyMap)
        {
            projects.Clear();
            projects.AddRange(Serialization.ReadProjects(rootPath));

            assemblies.Clear();
            assemblies.AddRange(Serialization.ReadAssemblies(rootPath));

            FillProjectToAssemblyMap(assemblies, projects, projectToAssemblyMap);
        }

        private static void FillProjectToAssemblyMap(
            List<AssemblyInfo> assemblies,
            List<string> projects,
            Dictionary<string, int> projectToAssemblyMap)
        {
            for (int i = 0; i < assemblies.Count; i++)
            {
                var projectKey = assemblies[i].ProjectKey;
                if (projectKey >= 0)
                {
                    projectToAssemblyMap[projects[projectKey]] = i;
                }
            }
        }

        public static void ReadDeclaredSymbols(
            string rootPath,
            List<IndexEntry> symbols,
            List<AssemblyInfo> assemblies,
            List<string> projects,
            ref Huffman huffman,
            ref double progress)
        {
            var masterIndexFile = Path.Combine(rootPath, "DeclaredSymbols.txt");
            var huffmanFile = Path.Combine(rootPath, "Huffman.txt");
            if (!File.Exists(masterIndexFile) || !File.Exists(huffmanFile))
            {
                return;
            }

            using (Measure.Time("Read huffman tables"))
            {
                huffman = Huffman.Read(huffmanFile);
            }

            using (Measure.Time("Read binary file"))
            {
                symbols.Clear();
                using (var fileStream = new FileStream(
                    masterIndexFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    262144,
                    FileOptions.SequentialScan))
                using (var reader = new BinaryReader(fileStream))
                {
                    int count = reader.ReadInt32();
                    int onePercent = Math.Max(count / 100, 1);
                    symbols.Capacity = count;
                    for (int i = 0; i < count; i++)
                    {
                        if (i % onePercent == 0)
                        {
                            progress = (double)i / count;
                        }

                        var symbol = Serialization.ReadDeclaredSymbol(reader);
                        symbols.Add(symbol);
                    }
                }
            }
        }
    }
}