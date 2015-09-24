using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class NormalizeTests
    {
        [TestMethod]
        public void NormalizeTest1()
        {
            new TestNormalize
            {
                AssembliesAndProjects =
                {
                    { "a", "e" },
                    { "b", "f" },
                    { "c", "d" }
                },
                Assemblies =
                {
                    { "a", 1 },
                    { "b", 2 },
                    { "c", 0 }
                },
                Projects =
                {
                    "d",
                    "e",
                    "f"
                }
            }.Verify();
        }

        [TestMethod]
        public void NormalizeTest2()
        {
            new TestNormalize
            {
                AssembliesAndProjects =
                {
                    { "a", null },
                    { "b", "f" },
                    { "c", "d" }
                },
                Assemblies =
                {
                    { "a", -1 },
                    { "b", 1 },
                    { "c", 0 }
                },
                Projects =
                {
                    "d",
                    "f"
                }
            }.Verify();
        }
    }

    public class TestNormalize
    {
        public TupleList<string, string> AssembliesAndProjects = new TupleList<string, string>();
        public TupleList<string, int> Assemblies = new TupleList<string, int>();
        public List<string> Projects = new List<string>();

        public void Verify()
        {
            IEnumerable<Tuple<string, int>> actualAssemblies;
            IEnumerable<string> actualProjects;
            Serialization.Normalize(
                AssembliesAndProjects,
                out actualAssemblies,
                out actualProjects);
            Assert.IsTrue(actualAssemblies.SequenceEqual(Assemblies));
            Assert.IsTrue(actualProjects.SequenceEqual(Projects));
        }
    }

    public class TupleList<T1, T2> : List<Tuple<T1, T2>>
    {
        public void Add(T1 t1, T2 t2)
        {
            Add(Tuple.Create(t1, t2));
        }
    }
}
