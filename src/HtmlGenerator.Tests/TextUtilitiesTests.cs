using System.IO;
using System.Linq;
using Microsoft.SourceBrowser.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class TextUtilitiesTests
    {
        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset1()
        {
            int start = 1;
            int end = 3;
            var actual = Markup.HtmlEscape("asbe", ref start, ref end);
            Assert.AreEqual(1, start);
            Assert.AreEqual(3, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset2()
        {
            int start = 1;
            int end = 3;
            var actual = Markup.HtmlEscape("a'be", ref start, ref end);
            Assert.AreEqual("a&#39;be", actual);
            Assert.AreEqual(1, start);
            Assert.AreEqual(7, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset3()
        {
            int start = 0;
            int end = 2;
            var actual = Markup.HtmlEscape("'be", ref start, ref end);
            Assert.AreEqual("&#39;be", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(6, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset4()
        {
            int start = 0;
            int end = 3;
            var actual = Markup.HtmlEscape("'b'", ref start, ref end);
            Assert.AreEqual("&#39;b&#39;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(11, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset5()
        {
            int start = 1;
            int end = 4;
            var actual = Markup.HtmlEscape("a'b'c", ref start, ref end);
            Assert.AreEqual("a&#39;b&#39;c", actual);
            Assert.AreEqual(1, start);
            Assert.AreEqual(12, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeSingleQuoteOffset6()
        {
            int start = 0;
            int end = 4;
            var actual = Markup.HtmlEscape("a'b'c", ref start, ref end);
            Assert.AreEqual("a&#39;b&#39;c", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(12, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeLessThanBeforeSpan()
        {
            int start = 3;
            int end = 4;
            var actual = Markup.HtmlEscape("<b>a", ref start, ref end);
            Assert.AreEqual("&lt;b&gt;a", actual);
            Assert.AreEqual(9, start);
            Assert.AreEqual(10, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeLessThanBeforeSpan2()
        {
            int start = 1;
            int end = 4;
            var actual = Markup.HtmlEscape("a<b>", ref start, ref end);
            Assert.AreEqual("a&lt;b&gt;", actual);
            Assert.AreEqual(1, start);
            Assert.AreEqual(10, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeLessThanAfterSpan()
        {
            int start = 0;
            int end = 1;
            var actual = Markup.HtmlEscape("a<b>", ref start, ref end);
            Assert.AreEqual("a&lt;b&gt;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(1, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeLessThanAfterSpan2()
        {
            int start = 0;
            int end = 3;
            var actual = Markup.HtmlEscape("<b>a", ref start, ref end);
            Assert.AreEqual("&lt;b&gt;a", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(9, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeBothInclusive()
        {
            int start = 0;
            int end = 3;
            var actual = Markup.HtmlEscape("<a>", ref start, ref end);
            Assert.AreEqual("&lt;a&gt;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(9, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeBothExclusive()
        {
            int start = 1;
            int end = 2;
            var actual = Markup.HtmlEscape("<a>", ref start, ref end);
            Assert.AreEqual("&lt;a&gt;", actual);
            Assert.AreEqual(4, start);
            Assert.AreEqual(5, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeInclusiveExclusive()
        {
            int start = 0;
            int end = 2;
            var actual = Markup.HtmlEscape("<a>", ref start, ref end);
            Assert.AreEqual("&lt;a&gt;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(5, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeExclusiveInclusive()
        {
            int start = 1;
            int end = 3;
            var actual = Markup.HtmlEscape("<a>", ref start, ref end);
            Assert.AreEqual("&lt;a&gt;", actual);
            Assert.AreEqual(4, start);
            Assert.AreEqual(9, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed04()
        {
            int start = 0;
            int end = 4;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(17, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed14()
        {
            int start = 1;
            int end = 4;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(4, start);
            Assert.AreEqual(17, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed24()
        {
            int start = 2;
            int end = 4;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(9, start);
            Assert.AreEqual(17, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed34()
        {
            int start = 3;
            int end = 4;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(13, start);
            Assert.AreEqual(17, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed03()
        {
            int start = 0;
            int end = 3;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(0, start);
            Assert.AreEqual(13, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed13()
        {
            int start = 1;
            int end = 3;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(4, start);
            Assert.AreEqual(13, end);
        }

        [TestMethod]
        public void TestMarkupHtmlEscapeMixed23()
        {
            int start = 2;
            int end = 3;
            var actual = Markup.HtmlEscape("<'>>", ref start, ref end);
            Assert.AreEqual("&lt;&#39;&gt;&gt;", actual);
            Assert.AreEqual(9, start);
            Assert.AreEqual(13, end);
        }

        [TestMethod]
        public void TestGetLineLengths()
        {
            T("");
            T("a", 1);
            T("\r", 1, 0);
            T("\r\r", 1, 1, 0);
            T("\r\r\r", 1, 1, 1, 0);
            T("\n", 1, 0);
            T("\n\n", 1, 1, 0);
            T("\n\r", 1, 1, 0);
            T("\r\n", 2, 0);
            T("\r\n\r", 2, 1, 0);
            T("a\r\n\a\r\n\a", 3, 3, 1);
        }

        [TestMethod]
        public void TestSplitSemicolonSeparatedList()
        {
            S("");
            S("a", "a");
            S(" a", " ", "a");
            S("a ", "a", " ");
            S("ab", "ab");
            S("a;", "a", ";");
            S(";a", ";", "a");
            S(";;", ";", ";");
            S("a;b", "a", ";", "b");
            S("a; b", "a", ";", " ", "b");
            S("a;\r\nb;\r\nc;", "a", ";", "\r\n", "b", ";", "\r\n", "c", ";");
        }

        private void S(string text, params string[] expected)
        {
            var actual = TextUtilities.SplitSemicolonSeparatedList(text);
            var equal = actual.SequenceEqual(expected);
            Assert.IsTrue(equal);
        }

        private void T(string text, params int[] lineLengths)
        {
            var actual = TextUtilities.GetLineLengths(text);
            var equal = actual.SequenceEqual(lineLengths);
            Assert.IsTrue(equal);

            File.WriteAllText("test.txt", text);
            actual = File.ReadAllLines("test.txt").Select(l => l.Length).ToArray();
            equal = actual.SequenceEqual(lineLengths);
        }

        [TestMethod]
        public void TestMinimalUniquenessPreservingPrefixLength()
        {
            MUPPL(1, "a", "b");
            MUPPL(2, "abc", "ace");
            MUPPL(3, "abcd", "abef");
            MUPPL(4, "abcde", "abcee");
            MUPPL(5, "abcde", "abcdg");
            MUPPL(6, "abcdefg", "abcdegh");
            MUPPL(7, "abcdefgh", "abcdefii");
            MUPPL(7, "abcdefgh1234zxcv", "abcdefiipokliuhj", "aosdfijficicufuf");
        }

        private void MUPPL(int expected, params string[] strings)
        {
            var actual = TextUtilities.MinimalUniquenessPreservingPrefixLength(strings);
            Assert.AreEqual(expected, actual);
        }
    }
}
