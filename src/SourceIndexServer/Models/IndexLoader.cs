using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public static class IndexLoader
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
            IFileSystem fs;
            if (string.IsNullOrEmpty(Helpers.IndexProxyUrl))
            {
                fs = new StaticFileSystem(rootPath);
            }
            else
            {
                fs = new AzureBlobFileSystem(Helpers.IndexProxyUrl);
            }
            using (Measure.Time("Read index"))
            {
                using (Measure.Time("Read project info"))
                {
                    ReadProjectInfo(
                        fs,
                        index.assemblies,
                        index.projects,
                        index.projectToAssemblyIndexMap);
                }

                using (Measure.Time("Read declared symbols"))
                {
                    ReadDeclaredSymbols(
                        fs,
                        index.symbols,
                        index.assemblies,
                        index.projects,
                        ref index.huffman,
                        ref index.progress);
                }

                ReadGuids(fs, index.guids);
                ReadMSBuildProperties(fs, index.msbuildProperties);
                ReadMSBuildItems(fs, index.msbuildItems);
                ReadMSBuildTargets(fs, index.msbuildTargets);
                ReadMSBuildTasks(fs, index.msbuildTasks);
            }
        }

        private static void ReadMSBuildProperties(IFileSystem fs, List<string> msbuildProperties)
        {
            var msbuildPropertiesFolder = Path.Combine(Constants.MSBuildPropertiesAssembly, Constants.ReferencesFileName);
            if (!fs.DirectoryExists(msbuildPropertiesFolder))
            {
                return;
            }

            foreach (var msbuildPropertiesFile in fs.ListFiles(msbuildPropertiesFolder))
            {
                var propertyName = Path.GetFileNameWithoutExtension(msbuildPropertiesFile);
                msbuildProperties.Add(propertyName);
            }

            msbuildProperties.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildItems(IFileSystem fs, List<string> msbuildItems)
        {
            var msbuildItemsFolder = Path.Combine(Constants.MSBuildItemsAssembly, Constants.ReferencesFileName);
            if (!fs.DirectoryExists(msbuildItemsFolder))
            {
                return;
            }

            foreach (var msbuildPropertiesFile in fs.ListFiles(msbuildItemsFolder))
            {
                var propertyName = Path.GetFileNameWithoutExtension(msbuildPropertiesFile);
                msbuildItems.Add(propertyName);
            }

            msbuildItems.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildTargets(IFileSystem fs, List<string> msbuildTargets)
        {
            var msbuildTargetsFolder = Path.Combine(Constants.MSBuildTargetsAssembly, Constants.ReferencesFileName);
            if (!fs.DirectoryExists(msbuildTargetsFolder))
            {
                return;
            }

            foreach (var msbuildTargetsFile in fs.ListFiles(msbuildTargetsFolder))
            {
                var targetName = Path.GetFileNameWithoutExtension(msbuildTargetsFile);
                msbuildTargets.Add(targetName);
            }

            msbuildTargets.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadMSBuildTasks(IFileSystem fs, List<string> msbuildTasks)
        {
            var msbuildTasksFolder = Path.Combine(Constants.MSBuildTasksAssembly, Constants.ReferencesFileName);
            if (!fs.DirectoryExists(msbuildTasksFolder))
            {
                return;
            }

            foreach (var msbuildTasksFile in fs.ListFiles(msbuildTasksFolder))
            {
                var taskName = Path.GetFileNameWithoutExtension(msbuildTasksFile);
                msbuildTasks.Add(taskName);
            }

            msbuildTasks.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadGuids(IFileSystem fs, List<string> guids)
        {
            var guidsFolder = Path.Combine(Constants.GuidAssembly, Constants.ReferencesFileName);
            if (!fs.DirectoryExists(guidsFolder))
            {
                return;
            }

            foreach (var guidFile in fs.ListFiles(guidsFolder))
            {
                var guid = Path.GetFileNameWithoutExtension(guidFile);
                guids.Add(guid);
            }

            guids.Sort();
        }

        public static void ReadProjectInfo(
            IFileSystem fs,
            List<AssemblyInfo> assemblies,
            List<string> projects,
            Dictionary<string, int> projectToAssemblyMap)
        {
            projects.Clear();
            projects.AddRange(Serialization.ReadProjects(fs));

            assemblies.Clear();
            assemblies.AddRange(Serialization.ReadAssemblies(fs));

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
            IFileSystem fs,
            List<IndexEntry> symbols,
            List<AssemblyInfo> assemblies,
            List<string> projects,
            ref Huffman huffman,
            ref double progress)
        {
            var masterIndexFile = "DeclaredSymbols.txt";
            if (!fs.FileExists("DeclaredSymbols.txt"))
            {
                return;
            }

            using (Measure.Time("Read huffman tables"))
            {
                using (var stream = fs.OpenSequentialReadStream("Huffman.txt"))
                {
                    huffman = Huffman.Read(stream);
                }
            }

            using (Measure.Time("Read binary file"))
            {
                symbols.Clear();

                using (var stream = fs.OpenSequentialReadStream(masterIndexFile))
                using (var reader = new BinaryReader(stream))
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