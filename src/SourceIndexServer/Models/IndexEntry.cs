using System;
using System.Collections.Generic;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public struct IndexEntry
    {
        public ushort AssemblyNumber;
        public ushort Glyph;
        public string Name;
        public ulong ID;
        public IntPtr Description;

        public IndexEntry(DeclaredSymbolInfo symbolInfo)
        {
            AssemblyNumber = symbolInfo.AssemblyNumber;
            Glyph = symbolInfo.Glyph;
            Name = symbolInfo.Name;
            ID = symbolInfo.ID;
            Description = IntPtr.Zero;
        }

        public IndexEntry(string name)
        {
            this = new IndexEntry();
            Name = name;
        }

        public IndexEntry(string name, IntPtr description)
            : this(name)
        {
            Description = description;
        }

        public DeclaredSymbolInfo GetDeclaredSymbolInfo(
            Huffman huffman,
            IList<AssemblyInfo> assemblies,
            IList<string> projects)
        {
            var result = new DeclaredSymbolInfo
            {
                AssemblyNumber = AssemblyNumber,
                Glyph = Glyph,
                Name = Name
            };

            if (assemblies != null && AssemblyNumber < assemblies.Count)
            {
                var assembly = assemblies[AssemblyNumber];
                result.AssemblyName = assembly.AssemblyName;
                if (projects != null && assembly.ProjectKey >= 0 && assembly.ProjectKey < projects.Count)
                {
                    result.ProjectFilePath = projects[assembly.ProjectKey];
                }
            }

            result.ID = ID;

            result.Kind = GetKind(Glyph);

            if (huffman != null && Description != IntPtr.Zero)
            {
                result.Description = huffman.Uncompress(Description);
            }

            return result;
        }

        private static string GetKind(ushort Glyph)
        {
            switch (Glyph)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                    return SymbolKindText.Class;
                case 12:
                case 13:
                case 14:
                case 15:
                case 16:
                case 17:
                    return SymbolKindText.Delegate;
                case 18:
                case 19:
                case 20:
                case 21:
                case 22:
                case 23:
                    return SymbolKindText.Enum;
                case 30:
                case 31:
                case 32:
                case 33:
                case 34:
                case 35:
                    return SymbolKindText.Event;
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 24:
                case 42:
                case 43:
                case 44:
                case 45:
                case 46:
                case 47:
                    return SymbolKindText.Field;
                case 48:
                case 49:
                case 50:
                case 51:
                case 52:
                case 53:
                    return SymbolKindText.Interface;
                case 72:
                case 73:
                case 74:
                case 75:
                case 76:
                case 77:
                case 78:
                case 79:
                case 80:
                case 81:
                case 82:
                case 83:
                case 220:
                case 221:
                case 224:
                    return SymbolKindText.Method;
                case 102:
                case 103:
                case 104:
                case 105:
                case 106:
                case 107:
                    return SymbolKindText.Property;
                case 108:
                case 109:
                case 110:
                case 111:
                case 112:
                case 113:
                    return SymbolKindText.Struct;
                case 195:
                case 196:
                case 227:
                case 228:
                    return SymbolKindText.File;
            }

            return SymbolKindText.Default;
        }
    }
}