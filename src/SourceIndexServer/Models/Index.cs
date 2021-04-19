using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class Index : IDisposable
    {
        public const int MaxRawResults = 100;

        public static string RootPath { get; private set; }

        // For testing
        public static void SetRootPath(string rootPath)
        {
            RootPath = rootPath;
        }

        public Index()
        {
        }

        public Index(string rootPath)
        {
            RootPath = rootPath;
            Task.Run(() => IndexLoader.ReadIndex(this, rootPath));
        }

        internal void ClearAll()
        {
            assemblies.Clear();
            projects.Clear();
            symbols.Clear();
            guids.Clear();
            projectToAssemblyIndexMap.Clear();
            msbuildProperties.Clear();
            msbuildItems.Clear();
            msbuildTargets.Clear();
            indexFinishedPopulating = false;
            progress = 0.0;
            huffman = null;
        }

        public List<AssemblyInfo> assemblies = new List<AssemblyInfo>();
        public List<string> projects = new List<string>();
        public List<IndexEntry> symbols = new List<IndexEntry>();
        public List<string> guids = new List<string>();
        public Dictionary<string, int> projectToAssemblyIndexMap = new Dictionary<string, int>();
        public List<string> msbuildProperties = new List<string>();
        public List<string> msbuildItems = new List<string>();
        public List<string> msbuildTargets = new List<string>();
        public List<string> msbuildTasks = new List<string>();

        public Huffman huffman;
        public bool indexFinishedPopulating = false;
        public double progress = 0.0;
        public string loadErrorMessage = null;

        public Query Get(string queryString)
        {
            if (!indexFinishedPopulating)
            {
                string message = "Index is being rebuilt... " + string.Format("{0:0%}", progress);
                if (loadErrorMessage != null)
                {
                    message = message + "<br />" + loadErrorMessage;
                }

                return Query.Empty(message);
            }

            if (queryString.Length < 3)
            {
                return Query.Empty("Enter at least three characters for type or member name");
            }

            var query = new Query(queryString);
            if (query.IsAssemblySearch())
            {
                FindAssemblies(query, defaultToAll: true);
            }
            else if (query.SymbolKinds.Contains(SymbolKindText.MSBuildProperty))
            {
                FindMSBuildProperties(query, defaultToAll: true);
            }
            else if (query.SymbolKinds.Contains(SymbolKindText.MSBuildItem))
            {
                FindMSBuildItems(query, defaultToAll: true);
            }
            else if (query.SymbolKinds.Contains(SymbolKindText.MSBuildTarget))
            {
                FindMSBuildTargets(query, defaultToAll: true);
            }
            else if (query.SymbolKinds.Contains(SymbolKindText.MSBuildTask))
            {
                FindMSBuildTasks(query, defaultToAll: true);
            }
            else
            {
                FindSymbols(query);
                FindAssemblies(query);
                FindProjects(query);
                FindGuids(query);
                FindMSBuildProperties(query);
                FindMSBuildItems(query);
                FindMSBuildTargets(query);
                FindMSBuildTasks(query);
            }

            return query;
        }

        public AssemblyInfo FindAssembly(string assemblyName)
        {
            int i = SortedSearch.FindItem(assemblies, assemblyName, a => a.AssemblyName);
            if (i == -1)
            {
                return default(AssemblyInfo);
            }

            return assemblies[i];
        }

        public int GetReferencingAssembliesCount(string assemblyName)
        {
            var assemblyInfo = FindAssembly(assemblyName);
            return assemblyInfo.ReferencingAssembliesCount;
        }

        public void FindAssemblies(Query query, bool defaultToAll = false)
        {
            string assemblyName = query.GetSearchTermForAssemblySearch();
            if (assemblyName == null)
            {
                if (defaultToAll)
                {
                    query.AddResultAssemblies(GetAllListedAssemblies());
                }

                return;
            }

            bool isQuoted = false;
            assemblyName = Query.StripQuotes(assemblyName, out isQuoted);

            var search = new SortedSearch(i => this.assemblies[i].AssemblyName, this.assemblies.Count);
            int low, high;
            search.FindBounds(assemblyName, out low, out high);
            if (high >= low)
            {
                var result = Enumerable
                    .Range(low, high - low + 1)
                    .Where(i => !isQuoted || assemblies[i].AssemblyName.Length == assemblyName.Length)
                    .Select(i => assemblies[i])
                    .Where(a => a.ProjectKey != -1)
                    .Take(MaxRawResults)
                    .ToList();
                query.AddResultAssemblies(result);
            }
        }

        private IEnumerable<AssemblyInfo> GetAllListedAssemblies()
        {
            return this.assemblies.Where(a => a.ProjectKey != -1);
        }

        public void FindSymbols(Query query)
        {
            foreach (var interpretation in query.Interpretations)
            {
                FindSymbols(query, interpretation);
            }

            if (query.ResultSymbols.Any())
            {
                query.ResultSymbols.Sort((l, r) => SymbolSorter(l, r, query));
            }
        }

        private void FindSymbols(Query query, Interpretation interpretation)
        {
            string searchTerm = interpretation.CoreSearchTerm;

            var search = new SortedSearch(i => symbols[i].Name, symbols.Count);

            int low, high;
            search.FindBounds(searchTerm, out low, out high);

            if (high < low)
            {
                return;
            }

            query.PotentialRawResults = high - low + 1;

            var result = Enumerable
                .Range(low, high - low + 1)
                .Where(i => !interpretation.IsVerbatim || symbols[i].Name.Length == searchTerm.Length)
                .Select(i => symbols[i].GetDeclaredSymbolInfo(huffman, assemblies, projects))
                .Where(query.Filter)
                .Where(interpretation.Filter)
                .Take(MaxRawResults)
                .ToList();

            foreach (var entry in result)
            {
                entry.MatchLevel = MatchLevel(entry.Name, searchTerm);
            }

            query.AddResultSymbols(result);
        }

        private void FindProjects(Query query)
        {
            string searchTerm = query.GetSearchTermForProjectSearch();
            if (searchTerm == null)
            {
                return;
            }

            var search = new SortedSearch(i => projects[i], projects.Count);

            int low, high;
            search.FindBounds(searchTerm, out low, out high);
            if (high >= low)
            {
                var result = Enumerable
                    .Range(low, high - low + 1)
                    .Select(i => assemblies[projectToAssemblyIndexMap[projects[i]]])
                    .Take(MaxRawResults)
                    .ToList();
                query.AddResultProjects(result);
            }
        }

        private void FindGuids(Query query)
        {
            string searchTerm = query.OriginalString;
            searchTerm = searchTerm.TrimStart('{', '(');
            searchTerm = searchTerm.TrimEnd('}', ')');

            var result = FindInList(searchTerm, guids, defaultToAll: false);
            if (result != null && result.Any())
            {
                query.AddResultGuids(result.ToList());
            }
        }

        private void FindMSBuildProperties(Query query, bool defaultToAll = false)
        {
            var result = FindInList(query.GetSearchTermForMSBuildSearch(), msbuildProperties, defaultToAll);
            if (result != null && result.Any())
            {
                query.AddResultMSBuildProperties(result.ToList());
            }
        }

        private void FindMSBuildItems(Query query, bool defaultToAll = false)
        {
            var result = FindInList(query.GetSearchTermForMSBuildSearch(), msbuildItems, defaultToAll);
            if (result != null && result.Any())
            {
                query.AddResultMSBuildItems(result.ToList());
            }
        }

        private void FindMSBuildTargets(Query query, bool defaultToAll = false)
        {
            var result = FindInList(query.GetSearchTermForMSBuildSearch(), msbuildTargets, defaultToAll);
            if (result != null && result.Any())
            {
                query.AddResultMSBuildTargets(result.ToList());
            }
        }

        private void FindMSBuildTasks(Query query, bool defaultToAll = false)
        {
            var result = FindInList(query.GetSearchTermForMSBuildSearch(), msbuildTasks, defaultToAll);
            if (result != null && result.Any())
            {
                query.AddResultMSBuildTasks(result.ToList());
            }
        }

        private IEnumerable<string> FindInList(string searchTerm, List<string> list, bool defaultToAll)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                if (defaultToAll)
                {
                    return list;
                }
                else
                {
                    return null;
                }
            }

            var search = new SortedSearch(i => list[i], list.Count);

            int low, high;
            search.FindBounds(searchTerm, out low, out high);
            if (high >= low)
            {
                var result = Enumerable
                    .Range(low, high - low + 1)
                    .Select(i => list[i])
                    .Take(MaxRawResults);
                return result;
            }

            return null;
        }

        public List<DeclaredSymbolInfo> FindSymbols(string queryString)
        {
            var query = new Query(queryString);
            FindSymbols(query);
            return query.ResultSymbols;
        }

        /// <summary>
        /// This defines the ordering of results based on the kind of symbol and other heuristics
        /// </summary>
        private int SymbolSorter(DeclaredSymbolInfo left, DeclaredSymbolInfo right, Query query)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null || right == null)
            {
                return 1;
            }

            var comparison = left.MatchLevel.CompareTo(right.MatchLevel);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.KindRank.CompareTo(right.KindRank);
            if (comparison != 0)
            {
                return comparison;
            }

            if (left.Name != null && right.Name != null)
            {
                comparison = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            comparison = left.AssemblyNumber.CompareTo(right.AssemblyNumber);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.Description, right.Description);
            return comparison;
        }

        /// <summary>
        /// This defines the ordering of the results, assigning weight to different types of matches
        /// </summary>
        private ushort MatchLevel(string candidate, string query)
        {
            int indexOf = candidate.IndexOf(query, StringComparison.Ordinal);
            int indexOfIgnoreCase = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);

            if (indexOf == 0)
            {
                if (candidate.Length == query.Length)
                {
                    // candidate == query
                    return 1;
                }
                else
                {
                    // candidate.StartsWith(query)
                    return 3;
                }
            }
            else if (indexOf > 0)
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        return 2;
                    }
                    else
                    {
                        return 4;
                    }
                }
                else
                {
                    return 5;
                }
            }
            else // indexOf < 0
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        // query.Equals(candidate, StringComparison.OrdinalIgnoreCase)
                        return 2;
                    }
                    else
                    {
                        // candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                        return 4;
                    }
                }
                else
                {
                    return 7;
                }
            }
        }

        public void Dispose()
        {
            if (huffman == null || symbols == null || assemblies == null || projects == null)
            {
                return;
            }

            for (int i = 0; i < this.symbols.Count; i++)
            {
                if (this.symbols[i].Description != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(this.symbols[i].Description);
                }
            }

            this.huffman = null;
            this.symbols = null;
            this.assemblies = null;
            this.projects = null;
        }

        ~Index()
        {
            Dispose();
        }
    }
}