using System;

partial class Partial
{
    partial void Foo()
    {
    }
}

abstract class Word<T>
{
}

class DerivedWord<T> : Word<T>
{
    void Foo()
    {
        Word<int> w = new DerivedWord<int>();
    }
}