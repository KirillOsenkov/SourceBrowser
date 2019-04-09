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

//==========================================================
// https://github.com/KirillOsenkov/SourceBrowser/issues/113
//==========================================================

public interface IAnimal
{
    void Eat();
}

public abstract class AbstractAnimal
{
    public void Eat() { }
}

public class Giraffe : AbstractAnimal, IAnimal
{
}