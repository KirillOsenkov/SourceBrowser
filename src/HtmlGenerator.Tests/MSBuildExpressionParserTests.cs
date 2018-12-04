using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class MSBuildExpressionParserTests
    {
        [TestMethod]
        public void T1()
        {
            T("");
            T("a", "a");
            T("$(a)", "$(a)");
            T("$(a", "$(a");
            T("$a)", "$a)");
            T("(a)", "(a)");
            T("$(a())", "$(a())");
            T("$(a()) ", "$(a())", " ");
            T(" $(a())", " ", "$(a())");
            T(" $(a()) ", " ", "$(a())", " ");
            T("a$(b)c", "a", "$(b)", "c");
            T("a$()c", "a", "$()", "c");
            T("a$(b)c$(d)e", "a", "$(b)", "c", "$(d)", "e");
            T("a$(b$(c))", "a", "$(b", "$(c)", ")");
            T("$(a)$(b)c", "$(a)", "$(b)", "c");
            T("@(a)", "@(a)");
            T("@(a", "@(a");
            T("@a)", "@a)");
            T("(a)", "(a)");
            T("@(a())", "@(a())");
            T("@(a()) ", "@(a())", " ");
            T(" @(a())", " ", "@(a())");
            T(" @(a()) ", " ", "@(a())", " ");
            T("a@(b)c", "a", "@(b)", "c");
            T("a@()c", "a", "@()", "c");
            T("a@(b)c@(d)e", "a", "@(b)", "c", "@(d)", "e");
            T("a@(b)c$(d)e", "a", "@(b)", "c", "$(d)", "e");
            T("a$(b)c@(d)e", "a", "$(b)", "c", "@(d)", "e");
            T("a@(b@(c))", "a", "@(b", "@(c)", ")");
            T("@(a)@(b)c", "@(a)", "@(b)", "c");
            T("$(a)@(b)c", "$(a)", "@(b)", "c");
            T("@(a)$(b)c", "@(a)", "$(b)", "c");
            T("@(a->b)", "@(a-", ">b)");
            T("@(a-&gt;b)", "@(a-", "&gt;b)");
            T("@(a,b)", "@(a,", "b)");
            T("$(a.b)", "$(a.b)");
            T(" @(a->b) ", " ", "@(a-", ">b) ");
        }

        private void T(string text, params string[] expectedParts)
        {
            var actual = MSBuildExpressionParser.SplitStringByPropertiesAndItems(text);
            var equal = expectedParts.SequenceEqual(actual);
            Assert.IsTrue(equal);
        }
    }
}
