using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        public string ProjectDestinationFolder { get; private set; }

        private string projectSourcePath;
        private string referencesFolder;
        public SolutionFinalizer SolutionFinalizer;

        public string AssemblyId { get; private set; }
        public string[] ReferencedAssemblies { get; set; }
        public List<string> ReferencingAssemblies { get; private set; }
        public Dictionary<ulong, DeclaredSymbolInfo> DeclaredSymbols { get; set; }
        public Dictionary<ulong, Tuple<string, ulong>> BaseMembers { get; set; }
        public MultiDictionary<ulong, Tuple<string, ulong>> ImplementedInterfaceMembers { get; set; }

        public long DocumentCount { get; set; }
        public long LinesOfCode { get; set; }
        public long BytesOfCode { get; set; }
        public long DeclaredSymbolCount { get; set; }
        public long DeclaredTypeCount { get; set; }
        public long PublicTypeCount { get; set; }

        public ProjectFinalizer(SolutionFinalizer solutionFinalizer, string directory)
        {
            this.BaseMembers = new Dictionary<ulong, Tuple<string, ulong>>();
            this.ImplementedInterfaceMembers = new MultiDictionary<ulong, Tuple<string, ulong>>();
            this.SolutionFinalizer = solutionFinalizer;
            ReferencingAssemblies = new List<string>();
            this.ProjectDestinationFolder = directory;
            this.referencesFolder = Path.Combine(directory, Constants.ReferencesFileName);
            this.AssemblyId = string.Intern(Path.GetFileName(directory));
            ReadProjectInfo();
            ReadDeclarationLines();
            ReadBaseMembers();
            ReadImplementedInterfaceMembers();
        }

        public override string ToString()
        {
            return AssemblyId;
        }

        public void ReadDeclarationLines()
        {
            DeclaredSymbols = new Dictionary<ulong, DeclaredSymbolInfo>();
            var assemblyIndex = Path.Combine(ProjectDestinationFolder, Constants.DeclaredSymbolsFileName + ".txt");
            if (!File.Exists(assemblyIndex))
            {
                return;
            }

            var declarationLines = File.ReadAllLines(assemblyIndex);
            foreach (var declarationLine in declarationLines)
            {
                var symbolInfo = new DeclaredSymbolInfo(declarationLine)
                {
                    AssemblyName = this.AssemblyId
                };
                if (symbolInfo.IsValid)
                {
                    DeclaredSymbols[symbolInfo.ID] = symbolInfo;
                }
            }
        }

        public string ProjectInfoLine => projectSourcePath;

        private void ReadBaseMembers()
        {
            var baseMembersFile = Path.Combine(ProjectDestinationFolder, Constants.BaseMembersFileName + ".txt");
            if (!File.Exists(baseMembersFile))
            {
                return;
            }

            var lines = File.ReadAllLines(baseMembersFile);
            foreach (var line in lines)
            {
                var parts = line.Split(';');
                var derivedId = Serialization.HexStringToULong(parts[0]);
                var baseAssemblyName = string.Intern(parts[1]);
                var baseId = Serialization.HexStringToULong(parts[2]);
                BaseMembers[derivedId] = Tuple.Create(baseAssemblyName, baseId);
            }
        }

        private void ReadImplementedInterfaceMembers()
        {
            var implementedInterfaceMembersFile = Path.Combine(ProjectDestinationFolder, Constants.ImplementedInterfaceMembersFileName + ".txt");
            if (!File.Exists(implementedInterfaceMembersFile))
            {
                return;
            }

            var lines = File.ReadAllLines(implementedInterfaceMembersFile);
            foreach (var line in lines)
            {
                var parts = line.Split(';');
                var implementationId = Serialization.HexStringToULong(parts[0]);
                var interfaceAssemblyName = string.Intern(parts[1]);
                var interfaceMemberId = Serialization.HexStringToULong(parts[2]);
                ImplementedInterfaceMembers.Add(implementationId, Tuple.Create(interfaceAssemblyName, interfaceMemberId));
            }
        }

        private void ReadProjectInfo()
        {
            var projectInfoFile = Path.Combine(ProjectDestinationFolder, Constants.ProjectInfoFileName + ".txt");
            if (File.Exists(projectInfoFile))
            {
                var lines = File.ReadAllLines(projectInfoFile);
                projectSourcePath = Serialization.ReadValue(lines, "ProjectSourcePath");
                DocumentCount = Serialization.ReadLong(lines, "DocumentCount");
                LinesOfCode = Serialization.ReadLong(lines, "LinesOfCode");
                BytesOfCode = Serialization.ReadLong(lines, "BytesOfCode");
                DeclaredSymbolCount = Serialization.ReadLong(lines, "DeclaredSymbols");
                DeclaredTypeCount = Serialization.ReadLong(lines, "DeclaredTypes");
                PublicTypeCount = Serialization.ReadLong(lines, "PublicTypes");
            }

            var referenceList = Path.Combine(ProjectDestinationFolder, Constants.ReferencedAssemblyList + ".txt");
            if (File.Exists(referenceList))
            {
                ReferencedAssemblies = File.ReadAllLines(referenceList).Select(s => string.Intern(s)).ToArray();
            }
        }
    }
}
