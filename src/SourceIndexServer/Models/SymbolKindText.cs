using System.Collections.Generic;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public class SymbolKindText
    {
        public const string Assembly = "assembly";
        public const string Type = "type";
        public const string Class = "class";
        public const string Struct = "struct";
        public const string Interface = "interface";
        public const string Enum = "enum";
        public const string Delegate = "delegate";
        public const string Method = "method";
        public const string Property = "property";
        public const string Event = "event";
        public const string Field = "field";
        public const string File = "file";
        public const string MSBuildProperty = "MSBuildProperty";
        public const string MSBuildItem = "MSBuildItem";
        public const string MSBuildTarget = "MSBuildTarget";
        public const string MSBuildTask = "MSBuildTask";
        public const string Default = "symbol";

        public static readonly string[] Kinds =
        {
            Assembly,
            Type,
            Class,
            Struct,
            Interface,
            Enum,
            Delegate,
            Method,
            Property,
            Event,
            Field,
            File,
            MSBuildProperty,
            MSBuildItem,
            MSBuildTarget,
            MSBuildTask,
        };
        private static readonly HashSet<string> hashtable = new HashSet<string>(Kinds);

        public static ushort Rank(string kind)
        {
            switch (kind)
            {
                case Class:
                case Struct:
                case Interface:
                case Enum:
                case Delegate:
                    return 1;
                case Field:
                    return 3;
                case File:
                    return 4;
                default:
                    return 2;
            }
        }

        public static bool IsKnown(string term)
        {
            return hashtable.Contains(term);
        }

        public static bool IsType(string kind)
        {
            return
                kind == Class ||
                kind == Struct ||
                kind == Interface ||
                kind == Enum ||
                kind == Delegate;
        }
    }
}
