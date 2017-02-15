using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class Interpretation
    {
        public string CoreSearchTerm { get; set; }
        public bool IsVerbatim { get; set; }
        public string Namespace { get; set; }
        public List<string> FilterNames { get; private set; }
        public List<string> FilterDotSeparatedNames { get; private set; }

        public Interpretation()
        {
            FilterNames = new List<string>();
            FilterDotSeparatedNames = new List<string>();
        }

        public Interpretation Clone()
        {
            var newInterpretation = new Interpretation();
            newInterpretation.CoreSearchTerm = CoreSearchTerm;
            newInterpretation.IsVerbatim = IsVerbatim;
            newInterpretation.Namespace = Namespace;
            newInterpretation.FilterNames = new List<string>(FilterNames);
            newInterpretation.FilterDotSeparatedNames = new List<string>(FilterDotSeparatedNames);
            return newInterpretation;
        }

        public bool Filter(DeclaredSymbolInfo symbol)
        {
            return
                FilterDottedNames(symbol) &&
                FilterWords(symbol);
        }

        private bool FilterWords(DeclaredSymbolInfo symbol)
        {
            if (this.FilterNames.Count == 0)
            {
                return true;
            }

            foreach (var word in this.FilterNames)
            {
                if (symbol.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) == -1 &&
                    (symbol.AssemblyName == null || symbol.AssemblyName.IndexOf(word, StringComparison.OrdinalIgnoreCase) == -1) &&
                    (symbol.ProjectFilePath == null || symbol.ProjectFilePath.IndexOf(word, StringComparison.OrdinalIgnoreCase) == -1))
                {
                    return false;
                }
            }

            return true;
        }

        private bool FilterDottedNames(DeclaredSymbolInfo symbol)
        {
            if (this.Namespace == null)
            {
                return FilterAssemblies(symbol) || FilterDotSeparatedNames.Any(n => FilterNamespace(symbol, n));
            }
            else
            {
                return FilterAssemblies(symbol) && FilterNamespace(symbol, this.Namespace);
            }
        }

        private static bool FilterNamespace(DeclaredSymbolInfo symbol, string namespacePrefix)
        {
            var description = symbol.Description;
            int openParen = description.IndexOf('(');
            if (openParen > -1)
            {
                // if the description contains (, we should only search before it, to not accidentally
                // match any of the parameters that may follow afterwards
                description = description.Substring(0, openParen);
            }

            if (description.IndexOf(namespacePrefix, StringComparison.OrdinalIgnoreCase) != -1)
            {
                return true;
            }

            return false;
        }

        private bool FilterAssemblies(DeclaredSymbolInfo symbol)
        {
            if (!this.FilterDotSeparatedNames.Any())
            {
                return true;
            }

            foreach (var assemblyName in this.FilterDotSeparatedNames)
            {
                if (symbol.AssemblyName.IndexOf(assemblyName, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}