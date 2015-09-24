using System;
using System.Linq;
using Microsoft.SourceBrowser.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.SourceBrowser.SourceIndexServer;
using Microsoft.SourceBrowser.SourceIndexServer.Models;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class QueryUnitTests
    {
        [TestMethod]
        public void PrefilterPositive()
        {
            Match(new DeclaredSymbolInfo
            {
                AssemblyName = "mscorlib",
                ProjectFilePath = "ndp\\clr\\src\\mscorlib\\mscorlib.csproj",
                Name = "ConsoleColor",
                Description = "System.ConsoleColor",
                Kind = SymbolKindText.Class
            },
            "System.get_ConsoleC",
            "Console mscor",
            "Console clr",
            "System.Con Color",
            "Cons class",
            "Conso class struct",
            "Console System.",
            "Console System.C",
            "Console System.Co",
            "System.Con",
            "System.Console",
            "System.ConsoleColor",
            "System.set_ConsoleColor",
            "System.add_Cons",
            "get_Console",
            "set_Con",
            "add_ConsoleColor");

            Match(new DeclaredSymbolInfo
            {
                AssemblyName = "System.Core",
                ProjectFilePath = "ndp\\clr\\src\\bcl\\System.Core\\System.Core.csproj",
                Name = "Where",
                Description = "System.Linq.Enumerable.Where<T>(bla bla bla)",
                Kind = SymbolKindText.Method
            },
            "Where System",
            "Where System.",
            "Where System.Core",
            "Enumerable.Where",
            "Linq.Enumerable.Where");
        }

        [TestMethod]
        public void PrefilterNegative()
        {
            NoMatch(new DeclaredSymbolInfo
            {
                AssemblyName = "mscorlib",
                ProjectFilePath = "ndp\\clr\\src\\mscorlib\\mscorlib.csproj",
                Name = "ConsoleColor",
                Description = "System.ConsoleColor",
                Kind = SymbolKindText.Class
            },
            "Console Core",
            "System.Con Back",
            "Console env\\Editor",
            "Consol struct",
            "Console System.Cor",
            "Console System.Core");
        }

        private void Match(DeclaredSymbolInfo declaredSymbolInfo, params string[] queryStrings)
        {
            foreach (var queryString in queryStrings)
            {
                Match(declaredSymbolInfo, queryString, true);
            }
        }

        private void NoMatch(DeclaredSymbolInfo declaredSymbolInfo, params string[] queryStrings)
        {
            foreach (var queryString in queryStrings)
            {
                Match(declaredSymbolInfo, queryString, false);
            }
        }

        private static void Match(DeclaredSymbolInfo declaredSymbolInfo, string queryString, bool expected)
        {
            var query = new Query(queryString);
            bool actual =
                query.Filter(declaredSymbolInfo) &&
                query.Interpretations.Any(i =>
                    declaredSymbolInfo.Name.StartsWith(
                        i.CoreSearchTerm,
                        StringComparison.OrdinalIgnoreCase) &&
                    i.Filter(declaredSymbolInfo));
            Assert.AreEqual(expected, actual, queryString);
        }

        [TestMethod]
        public void TestSplitBySpaces1()
        {
            Split("");
            Split(" ");
            Split("  ");
            Split("\"", "\"");
            Split("\"\"", "\"\"");
            Split("\"\"\"", "\"\"", "\"");
            Split("\" \"", "\" \"");
            Split("\" \" ", "\" \"");
            Split(" \" \"", "\" \"");
            Split("a", "a");
            Split("a\"", "a", "\"");
            Split("\"a", "\"a");
            Split(" a", "a");
            Split("  a", "a");
            Split("a ", "a");
            Split("a  ", "a");
            Split("a b", "a", "b");
            Split("a b c", "a", "b", "c");
            Split("a b \"c\"", "a", "b", "\"c\"");
            Split("a b c\"", "a", "b", "c", "\"");
            Split("a b \"c", "a", "b", "\"c");
            Split("a b ", "a", "b");
            Split(" a b ", "a", "b");
            Split("\"a b\"", "\"a b\"");
            Split("\"a\" \"b\"", "\"a\"", "\"b\"");
            Split("\"a \"b\"", "\"a \"", "b", "\"");
            Split("\"a\" b", "\"a\"", "b");
            Split("a \"b\"", "a", "\"b\"");
        }

        private void Split(string query, params string[] expectedParts)
        {
            var actualParts = query.SplitBySpacesConsideringQuotes();
            Assert.IsTrue(
                Enumerable.SequenceEqual(expectedParts, actualParts),
                "Expected: " + string.Join(",", expectedParts) + "\r\n",
                "Actual: " + string.Join(",", actualParts));
        }
    }
}
