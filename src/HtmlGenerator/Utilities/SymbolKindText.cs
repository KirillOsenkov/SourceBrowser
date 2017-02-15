using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
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
        };
        private static readonly HashSet<string> hashtable = new HashSet<string>(Kinds);

        public static string GetSymbolKind(ISymbol declaredSymbol)
        {
            if (declaredSymbol.Kind == SymbolKind.NamedType)
            {
                return GetTypeKind(declaredSymbol);
            }

            return declaredSymbol.Kind.ToString().ToLowerInvariant();
        }

        public static string GetTypeKind(ISymbol declaredSymbol)
        {
            return ((INamedTypeSymbol)declaredSymbol).TypeKind.ToString().ToLowerInvariant();
        }

        public static ushort Rank(string kind)
        {
            switch (kind)
            {
                case SymbolKindText.Class:
                case SymbolKindText.Struct:
                case SymbolKindText.Interface:
                case SymbolKindText.Enum:
                case SymbolKindText.Delegate:
                    return 1;
                case SymbolKindText.Field:
                    return 3;
                case SymbolKindText.File:
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
