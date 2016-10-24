using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MEF
{
    public interface ITextVisitor
    {
        string Visit(string text, IReadOnlyDictionary<string,string> context);
    }
}
