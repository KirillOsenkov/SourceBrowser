using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class HtmlElementInfo
    {
        public string Name { get; set; }
        public Dictionary<string, string> Attributes { get; }

        public ISymbol DeclaredSymbol { get; set; }
        public string DeclaredSymbolId { get; set; }

        public HtmlElementInfo()
        {
            Attributes = new Dictionary<string, string>();
        }
    }
}