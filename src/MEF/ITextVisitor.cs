using System.Collections.Generic;

namespace Microsoft.SourceBrowser.MEF
{
    public interface ITextVisitor
    {
        string Visit(string text, IReadOnlyDictionary<string, string> context);
    }
}
