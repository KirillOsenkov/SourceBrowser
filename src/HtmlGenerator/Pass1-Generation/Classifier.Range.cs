using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class Classification
    {
        public class Range
        {
            public ClassifiedSpan ClassifiedSpan;
            public string Text;

            public Range()
            {
            }

            public Range(string classification, TextSpan textSpan, string text)
            {
                ClassifiedSpan = new ClassifiedSpan(classification, textSpan);
                Text = text;
            }

            public string ClassificationType => ClassifiedSpan.ClassificationType;

            public TextSpan TextSpan => ClassifiedSpan.TextSpan;
        }
    }
}
