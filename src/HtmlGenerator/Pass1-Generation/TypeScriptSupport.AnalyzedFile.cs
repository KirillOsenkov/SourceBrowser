namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class AnalyzedFile
    {
        public string fileName { get; set; }
        public ClassifiedRange[] syntacticClassifications { get; set; }
        public ClassifiedRange[] semanticClassifications { get; set; }
        public string fileSymbolId { get; set; }
    }
}
