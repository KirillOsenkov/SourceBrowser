using System.Collections.Generic;

namespace MEF
{
    public interface ITextVisitor
    {
        string Visit(string text, IReadOnlyDictionary<string, string> context);
    }
}
