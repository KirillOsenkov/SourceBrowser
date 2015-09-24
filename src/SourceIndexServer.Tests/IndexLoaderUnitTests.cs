using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.SourceBrowser.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.SourceBrowser.SourceIndexServer;
using Microsoft.SourceBrowser.SourceIndexServer.Models;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class IndexLoaderUnitTests
    {
        [TestMethod]
        public void TestHuffmanRoundtrip1()
        {
            TestHuffmanRoundtrip("abc", "abc", "aab", "bac");
        }

        public void TestHuffmanRoundtripOnIndex()
        {
            TestHuffmanRoundtrip("Suites.WFC.Common.SuiteUtil.SuiteUtil()", GetWords().ToArray());
        }

        public void TestHuffmanRoundtripOnIndexExhaustive()
        {
            var words = GetWords();
            var huffman = Huffman.Create(words);
            int i = 0;

            foreach (var word in words)
            {
                byte[] bytes = huffman.Compress(word);
                string actual = huffman.Uncompress(bytes);
                Assert.AreEqual(word, actual);
                i++;
            }
        }

        private void TestHuffmanRoundtrip(string word, params string[] words)
        {
            var huffman = Huffman.Create(words);
            byte[] bytes = huffman.Compress(word);
            string actual = huffman.Uncompress(bytes);
            Assert.AreEqual(word, actual);
        }

        private static IEnumerable<string> GetWords()
        {
            var index = ReadIndex();
            var words = index.symbols.Select(s => index.huffman.Uncompress(s.Description));
            return words;
        }

        private static IEnumerable<string> GetAsciiWords()
        {
            return GetWords().Where(Huffman.IsAscii);
        }

        //[TestMethod]
        public void VerifyNoAssemblyNameCollisions()
        {
            List<AssemblyInfo> assemblies = new List<AssemblyInfo>();
            List<string> projects = new List<string>();
            IndexLoader.ReadProjectInfo(GetRootPath(), assemblies, projects, new Dictionary<string, int>());
            var assemblyNames = assemblies.Select(p => p.AssemblyName);
            Assert.AreEqual(assemblyNames.Count(), assemblyNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        public void ConvertProjects()
        {
            var projectsTxt = Path.Combine(GetRootPath(), "OriginalProjects.txt");
            var assembliesAndProjects =
                from line in File.ReadLines(projectsTxt)
                let parts = line.Split(';')
                select Tuple.Create(parts[0], parts[1]);
            //Serialization.WriteProjectMap(
            //    GetRootPath(),
            //    assembliesAndProjects);
        }

        //[TestMethod]
        public void LoadIndex()
        {
            Index index = ReadIndex();
            var matches = index.FindSymbols("Microsoft.CodeAnalysis.CSharp.Symbols.SourceNamedTypeSymbol");
            var expected = new DeclaredSymbolInfo()
            {
                AssemblyName = "Microsoft.CodeAnalysis.CSharp",
                Description = "Microsoft.CodeAnalysis.CSharp.Symbols.SourceNamedTypeSymbol",
                Glyph = 1,
                ID = 6622120691603058343UL, // { 167, 174, 195, 250, 174, 127, 230, 91 }
                Kind = "class",
                Name = "SourceNamedTypeSymbol",
                ProjectFilePath = "Source\\Compilers\\CSharp\\Source\\CSharpCodeAnalysis.csproj"
            };
            Verify(matches, expected);

            var query = new Query("System.Core");
            index.FindAssemblies(query);
            Assert.AreEqual("System.Core", query.ResultAssemblies.First().AssemblyName);
        }

        public void CalculateMinimumHashLength()
        {
            var index = ReadIndex();
            List<string> list = new List<string>(index.symbols.Count);
            foreach (var item in index.symbols)
            {
                var symbolInfo = item.GetDeclaredSymbolInfo(index.huffman, index.assemblies, index.projects);
                var hexString = Serialization.ULongToHexString(symbolInfo.ID);
                list.Add(hexString);
            }

            list.Sort();

            List<int> dupes = new List<int>();

            for (int digit = 15; digit >= 0; digit--)
            {
                for (int i = 1; i < list.Count; i++)
                {
                    bool foundDifference = false;
                    for (int j = digit; j >= 0; j--)
                    {
                        if (list[i][j] != list[i - 1][j])
                        {
                            foundDifference = true;
                            break;
                        }
                    }

                    if (!foundDifference)
                    {
                        dupes.Add(i);
                    }
                }
            }

            foreach (var dupe in dupes)
            {
                System.Diagnostics.Debug.WriteLine(list[dupe]);
            }
        }

        public void TestAssemblies()
        {
            var index = ReadIndex();
            var query = new Query("System.Collections.Generric");
            index.FindSymbols(query);
            index.FindAssemblies(query);
        }

        private void Verify(List<DeclaredSymbolInfo> matches, params DeclaredSymbolInfo[] expected)
        {
            Assert.IsNotNull(matches);
            Assert.AreEqual(matches.Count, expected.Length);
            for (int i = 0; i < matches.Count; i++)
            {
                Assert.AreEqual(expected[i].AssemblyName, matches[i].AssemblyName);
                Assert.AreEqual(expected[i].Description, matches[i].Description);
                Assert.AreEqual(expected[i].Glyph, matches[i].Glyph);
                Assert.AreEqual(expected[i].Kind, matches[i].Kind);
                Assert.AreEqual(expected[i].Name, matches[i].Name);
                Assert.AreEqual(expected[i].ProjectFilePath, matches[i].ProjectFilePath);
                Assert.AreEqual(expected[i].ID, matches[i].ID);
            }
        }

        private static Index ReadIndex()
        {
            Index index = new Index();
            string rootPath = GetRootPath();
            IndexLoader.ReadIndex(index, rootPath);
            return index;
        }

        private static string GetRootPath()
        {
            throw new NotImplementedException();
        }
    }
}
