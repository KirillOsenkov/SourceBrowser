using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project2
{
    class _
    {
        _ _;

        _(){}

        void M<SomeTypeParameter>(
            SomeClass a,
            SomeStruct b,
            SomeRecord c,
            SomeEnum d,
            SomeInterface e,
            SomeDelegate f,
            SomeTypeParameter g)
        {
        }

        class SomeClass { }
        struct SomeStruct { }
        record SomeRecord(int i);
        enum SomeEnum { }
        interface SomeInterface { }
        delegate void SomeDelegate();
    }
}
