using System;
using System.Test;

namespace Type.Referencer
{
    public class Class1
    {
        public ClassType Foo()
        {
            var t = new ClassType();
            t.Foo(1);
            return t;
        }

        public GenericType<int, string> Stuff(GenericType<string, int> values)
        {
            return new GenericType<int, string>();
        }

        public void Test(StructureType value)
        {
        }

        public void HasPizza(EnumerationType value)
        {
        }

        public void Run(IInterfaceType iface)
        {
        }
        
        public void Run(DelegateType func)
        {
        }
    }
}
