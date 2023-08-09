using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        private readonly Dictionary<ISymbol, int> localIds = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);

        private HtmlElementInfo HighlightDefinition(ISymbol declaredSymbol)
        {
            int localId = GetLocalId(declaredSymbol);
            return new HtmlElementInfo
            {
                Name = "span",
                Attributes =
                {
                    { "id", "r" + localId + " rd" + GetClass(declaredSymbol.Kind) },
                    { "class", "r" + localId + " r" + GetClass(declaredSymbol.Kind) }
                }
            };
        }

        private HtmlElementInfo HighlightReference(ISymbol symbol)
        {
            int localId = GetLocalId(symbol);
            return new HtmlElementInfo
            {
                Name = "span",
                Attributes =
                {
                    { "class", "r" + localId + " r" + GetClass(symbol.Kind) }
                }
            };
        }

        private string GetClass(SymbolKind kind)
        {
            if (kind == SymbolKind.TypeParameter)
            {
                return " t";
            }

            return "";
        }

        private int GetLocalId(ISymbol symbol)
        {
            if (!localIds.TryGetValue(symbol, out int localId))
            {
                localId = localIds.Count;
                localIds.Add(symbol, localId);
            }

            return localId;
        }
    }
}
