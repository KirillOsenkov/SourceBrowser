using System.Collections.Generic;
using System.Linq;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.SourceIndexServer;
using Microsoft.SourceBrowser.SourceIndexServer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class IndexUnitTests
    {
        [TestMethod]
        public void Test()
        {
            Test(
                new[] { "a" },
                "bbb",
                new string[0]);
        }

        [TestMethod]
        public void Test2()
        {
            Test(
                new[] { "aaa" },
                "aaa",
                new[] { "aaa" });
        }

        [TestMethod]
        public void Test3()
        {
            Test(
                new[] { "aaa", "bbb" },
                "aaa",
                new[] { "aaa" });
        }

        [TestMethod]
        public void Test4()
        {
            Test(
                new[] { "aaa", "bbb" },
                "bbb",
                new[] { "bbb" });
        }

        [TestMethod]
        public void Test5()
        {
            Test(
                new[] { "aa", "aaa" },
                "aaa",
                new[] { "aaa" });
        }

        [TestMethod]
        public void Test6()
        {
            Test(
                new[] { "aaa", "aaa" },
                "aaa",
                new[] { "aaa", "aaa" });
        }

        [TestMethod]
        public void Test7()
        {
            Test(
                new[] { "aaa", "aba" },
                "aab",
                new string[0]);
        }

        [TestMethod]
        public void TestGotoSpaceStripQuotes()
        {
            Test(new[] { "a" }, "\"b", new string[0]);
            Test(new[] { "a" }, "\"b\"", new string[0]);
            Test(new[] { "a" }, "\"a \"", new string[0]);
        }

        [TestMethod]
        public void TestE2E2()
        {
            Test(
                new[] { "a", "b", "bin", "bin", "c", "z" },
                "bin",
                new[] { "bin", "bin" });
        }

        [TestMethod]
        public void TestE2E3()
        {
            Test(
                new[] { "a", "b", "bin", "binary", "c", "z" },
                "binary",
                new[] { "binary" });
        }

        public class EntryList : List<KeyValuePair<string, string>>
        {
            public void Add(string name, string description)
            {
                Add(new KeyValuePair<string, string>(name, description));
            }
        }

        [TestMethod]
        public void TestNamespaceSearch1()
        {
            Test(
                new EntryList
                {
                    { "Console", "System.Console" },
                    { "Console", "Foo.Console" }
                },
                "System.Con",
                "System.Console");
        }

        [TestMethod]
        public void TestNamespaceSearch2()
        {
            Test(
                new EntryList
                {
                    { "Console", "System.Console" },
                    { "Console", "System.Foo.Console" }
                },
                "System.Con",
                "System.Console");
        }

        [TestMethod]
        public void TestSortingOfResultsWithDottedQuery()
        {
            Test(
                new EntryList
                {
                    { "ConsoleSpecialKey", "System.ConsoleSpecialKey" },
                    { "Console", "System.Console" }
                },
                "System.Console",
                "System.Console",
                "System.ConsoleSpecialKey");
        }

        [TestMethod]
        public void TestStringFormatCurlies()
        {
            EndToEnd("{ab}",
                @"<div class=""note"">No results found</div>
<p>Try also searching on:</p>
<ul>
<li><a href=""http://stackoverflow.com/search?q=%7bab%7d"" target=""_blank"">http://stackoverflow.com/search?q=%7bab%7d</a></li>
<li><a href=""http://social.msdn.microsoft.com/Search/en-US?query=%7bab%7d"" target=""_blank"">http://social.msdn.microsoft.com/Search/en-US?query=%7bab%7d</a></li>
<li><a href=""https://www.google.com/search?q=%7bab%7d"" target=""_blank"">https://www.google.com/search?q=%7bab%7d</a></li>
<li><a href=""http://www.bing.com/search?q=%7bab%7d"" target=""_blank"">http://www.bing.com/search?q=%7bab%7d</a></li>
</ul>
");
        }

        [TestMethod]
        public void TestFilteringByOtherWords()
        {
            Test(
                new EntryList
                {
                    { "SourceNamedTypeSymbol", "Roslyn.Compilers.CSharp.SourceNamedTypeSymbol" },
                    { "SourceNamespaceSymbol", "Roslyn.Compilers.CSharp.SourceNamespaceSymbol" },
                    { "SourceFolder", "Roslyn.Compilers.SourceFolder" }
                },
                "Source Symbol",
                "Roslyn.Compilers.CSharp.SourceNamedTypeSymbol",
                "Roslyn.Compilers.CSharp.SourceNamespaceSymbol");
        }

        public void Test(IEnumerable<KeyValuePair<string, string>> input, string pattern, params string[] expectedResults)
        {
            using (var index = new Index())
            {
                var huffman = Huffman.Create(input.Select(kvp => kvp.Value));
                index.indexFinishedPopulating = true;
                index.huffman = huffman;
                index.symbols = new List<IndexEntry>(input.Select(kvp => new IndexEntry(kvp.Key, huffman.CompressToNative(kvp.Value))));
                var query = index.Get(pattern);
                var resultSymbols = query.ResultSymbols;
                Assert.IsNotNull(resultSymbols);
                var actualResults = resultSymbols.Select(i => i.Description);
                Assert.IsTrue(actualResults.SequenceEqual(expectedResults));
            }
        }

        public void EndToEnd(string queryString, string expectedHtml)
        {
            var testData = new List<DeclaredSymbolInfo>
            {
                new DeclaredSymbolInfo()
                {
                    Name = "Console",
                    Description = "System.Console",
                    // T:System.Console, f907d79481da6ba4
                    ID = 11847803494810978297UL
                }
            };

            using (var index = new Index())
            {
                var huffman = Huffman.Create(testData.Select(i => i.Description));
                index.indexFinishedPopulating = true;
                index.huffman = huffman;
                index.symbols = testData.Select(dsi => new IndexEntry(dsi)).ToList();
                var query = index.Get(queryString);
                var actualHtml = new ResultsHtmlGenerator(query).Generate(index: index);
                Assert.AreEqual(expectedHtml, actualHtml);
            }
        }

        private void Test(string[] input, string pattern, string[] expectedResults)
        {
            var index = new Index();
            index.symbols = new List<IndexEntry>(input.Select(s => new IndexEntry(s)));
            var foundSymbols = index.FindSymbols(pattern);
            if ((expectedResults == null || expectedResults.Length == 0) && foundSymbols == null)
            {
                return;
            }

            var actualResults = foundSymbols.Select(i => i.Name);
            Assert.IsTrue(actualResults.SequenceEqual(expectedResults));
        }
    }
}
