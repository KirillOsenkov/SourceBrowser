namespace System.Test
{
    public class ClassType
    {
        protected int Field;
        public int GetOnly { get; }
        public int SetOnly { set { } }
        public int Prop { get; set; }
        public void Foo(int bar) { }
        protected virtual void DoWork(int work) { }
    }

    public class GenericType<T1, T2>
    {
    }

    public struct StructureType
    {
    }

    public enum EnumerationType
    {
        A = 1,
        B = 2
    }

    public interface IInterfaceType
    {
    }

    public delegate void DelegateType(int alpha, bool beta);
}
