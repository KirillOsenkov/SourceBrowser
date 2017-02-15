namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class ClassifiedRange
    {
        // deserialization
        public ClassifiedRange()
        {
        }

        public ClassifiedRange(string text, int start, int length, ClassifiedRange enclosingRange = null)
        {
            this.text = text.Substring(start, length);

            this.start = start;
            this.length = length;

            if (enclosingRange != null)
            {
                classification = enclosingRange.classification;
                hyperlinks = enclosingRange.hyperlinks;
                definitionSymbolId = enclosingRange.definitionSymbolId;
                definitionKind = enclosingRange.definitionKind;
                searchString = enclosingRange.searchString;
                fullName = enclosingRange.fullName;
            }
        }

        public string classification { get; set; }
        public int start { get; set; }
        public int length { get; set; }
        public int end { get { return start + length; } }
        public Hyperlink[] hyperlinks { get; set; }
        public string definitionSymbolId { get; set; }
        public string definitionKind { get; set; }
        public string searchString { get; set; }
        public string fullName { get; set; }

        public bool IsSemantic { get; set; }

        public string text { get; set; }
        public int lineNumber { get; set; }
        public int column { get; set; }
        public string lineText { get; set; }

        public bool IsSymbolLocalOnly()
        {
            return
                definitionKind == "variable" ||
                definitionKind == "parameter";
        }

        public override string ToString()
        {
            return string.Format("{0} ({1};{2}) {3}", text, start, length, classification);
        }
    }
}
