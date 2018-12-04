using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        private HtmlElementInfo ProcessDeclaredSymbol(ISymbol declaredSymbol, bool isLargeFile)
        {
            if (declaredSymbol.Kind == SymbolKind.Local ||
                declaredSymbol.Kind == SymbolKind.Parameter ||
                declaredSymbol.Kind == SymbolKind.TypeParameter)
            {
                if (isLargeFile)
                {
                    return null;
                }

                return HighlightDefinition(declaredSymbol);
            }

            var result = GenerateHyperlinkToReferences(declaredSymbol, isLargeFile);
            return result;
        }

        public HtmlElementInfo GenerateHyperlinkToReferences(ISymbol symbol, bool isLargeFile = false)
        {
            string symbolId = SymbolIdService.GetId(symbol);

            string referencesFilePath = Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName, symbolId + ".html");
            string href = Paths.MakeRelativeToFile(referencesFilePath, documentDestinationFilePath);
            href = href.Replace('\\', '/');

            var result = new HtmlElementInfo
            {
                Name = "a",
                Attributes =
                {
                    ["id"] = symbolId,
                    ["href"] = href,
                    ["target"] = "n",
                },
                DeclaredSymbol = symbol,
                DeclaredSymbolId = symbolId
            };

            if (!isLargeFile)
            {
                var dataGlyph = string.Format("{0},{1}",
                    SymbolIdService.GetGlyphNumber(symbol),
                    GetSymbolDepth(symbol));
                result.Attributes.Add("data-glyph", dataGlyph);
            }

            return result;
        }

        private int GetSymbolDepth(ISymbol symbol)
        {
            ISymbol current = symbol.ContainingSymbol;
            int depth = 0;
            while (current != null)
            {
                if (current is INamespaceSymbol namespaceSymbol)
                {
                    // if we've reached the global namespace, we're already at the top; bail
                    if (namespaceSymbol.IsGlobalNamespace)
                    {
                        break;
                    }
                }
                else
                {
                    // we don't want namespaces to add to our "depth" because they won't be displayed in the tree
                    depth++;
                }

                current = current.ContainingSymbol;
            }

            return depth;
        }
    }
}