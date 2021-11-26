using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static class Extensions
{
    public static string NewLine => "\r\n";
    public static void ExtensionMethod(this string s)
    {
        var s1 = new string(new char[0]);
        var s2 = new string(' ', 42);
    }
}

#if TESTDEFINE
public class ThisShouldBeEnabled { }
#endif

class ArrayOfZeroLengthAllocationDetection
{
    private const int SIZE = 0;
    static void Test()
    {
        var a = new object[0];
        Console.WriteLine(a.Length);
        a = new object[] { };
        Console.WriteLine(a.Length);
        a = new object[SIZE];
    }
}

public class ExtensionUsage
{
    public void Test()
    {
        "".ExtensionMethod();
        Extensions.ExtensionMethod("");
    }

    ~ExtensionUsage()
    {
    }
}

public interface I1 { void Foo(); }
public interface I2 : I1 { }

interface I3 : IEnumerable<I2>, I2, I1 { }
interface I4 : IEnumerable<I2> { }

public partial class Partial
{
    partial void Foo();

    internal const string Internal = "Friend";
}

namespace Acme
{
    public class Generic<T>
    {
        public static T M<T>(T t) { }
    }

    namespace Test
    {
    }

    namespace Test2 { class Baz { } }
}

[Guid(@"C09FBDCB-C126-4A4F-BF36-4B1E3AF4D376")]
class Class<U> : I1
{
    public class Nested1
    {
        public enum Nested2
        {
            A
        }
    }

    public class NestedGeneric<T>
    {
        public T GetT(U u) { }
    }

    void I1.Foo()
    {
#if _DEBUG
        whatnot
#endif
        C c = new C();
        throw new NotImplementedException();
    }
}

[Guid("C09FBDCB-C126-4A4F-BF36-4B1E3AF4D376")]
class Abc : I1
{
    public virtual void M() { }
    protected virtual string Name { get; set; }
    protected internal virtual event Action Event;

    public static readonly Guid guid = new Guid(@"AAAAAAAA-C126-4A4F-BF36-4B1E3AF4D376");
    public static readonly Guid guid2 = new Guid("{BBBBBBBB-C126-4A4F-BF36-4B1E3AF4D376}");
    public static readonly Guid guid3 = new Guid(@"{C09FBDCB-C126-4A4F-bf36-4B1E3AF4D376}");

    public void Foo()
    {
        var a = Name;
        Name = a;
        this.Name = a;
    }
}

class A
{
    public virtual void M()
    {
    }

    public abstract string Name
    {
        get;
    }

    protected virtual event Action Event;
}

class B : A, System.ICloneable
{
    System.ICloneable c;
    A a = new A();

    public B()
    {
    }

    public override void M()
    {
        base.M();
        Name = Name;
        var b = new B();
    }

    protected override string Name
    {
        get
        {
            return base.Name;
        }
    }

    protected internal override event Action Event;
}

class TargetedTypeNewTest
{
    public TargetedTypeNewTest(B b) { }

    static TargetedTypeNewTest Create()
    {
        return new(new());
    }
}