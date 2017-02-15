﻿using System;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class DeclaredSymbolInfo : IEquatable<DeclaredSymbolInfo>
    {
        public ushort AssemblyNumber;
        public string AssemblyName { get; set; }
        public string ProjectFilePath { get; set; }
        public ushort Glyph;
        public string Name;
        public ulong ID;
        public string Kind;
        public string Description;
        public ushort MatchLevel;

        public DeclaredSymbolInfo()
        {
        }

        public ushort KindRank
        {
            get
            {
                return SymbolKindText.Rank(Kind);
            }
        }

        public string GetNamespace()
        {
            var description = Description;
            if (string.IsNullOrEmpty(description))
            {
                return "";
            }

            int lastDot = description.LastIndexOf('.');
            if (lastDot == -1)
            {
                return "";
            }

            return description.Substring(0, lastDot);
        }

        public int Weight
        {
            get
            {
                return MatchLevel * 10 + KindRank;
            }
        }

        public bool Equals(DeclaredSymbolInfo other)
        {
            if (other == null)
            {
                return false;
            }

            return
                AssemblyNumber == other.AssemblyNumber &&
                ProjectFilePath == other.ProjectFilePath &&
                Glyph == other.Glyph &&
                Name == other.Name &&
                Kind == other.Kind &&
                ID == other.ID &&
                Description == other.Description;
        }

        public string GetUrl()
        {
            return "/" + AssemblyName + "/a.html#" + Serialization.ULongToHexString(ID);
        }

        public override bool Equals(object obj)
        {
            DeclaredSymbolInfo other = obj as DeclaredSymbolInfo;
            if (other == null)
            {
                return false;
            }

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return
                AssemblyNumber.GetHashCode() ^
                ProjectFilePath.GetHashCode() ^
                Glyph.GetHashCode() ^
                Name.GetHashCode() ^
                Kind.GetHashCode() ^
                Description.GetHashCode() ^
                ID.GetHashCode();
        }
    }
}
