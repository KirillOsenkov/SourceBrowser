using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MEF
{
    public interface ISourceBrowserPlugin
    {
        void Init(Dictionary<string, string> Configuration, ILog logger);
        IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(string projectPath);
        IEnumerable<ITextVisitor> ManufactureTextVisitors(string projectPath);
    }
}
