using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class PrepareRangesTests
    {
        [TestMethod]
        public void TestRanges()
        {
            T(
                "abcdefghijkl",
                new Dictionary<int, int> { { 2, 3 } },
                new Dictionary<int, int> { { 6, 2 } },
                new Dictionary<int, int> { { 0, 2 }, { 2, 3 }, { 5, 1 }, { 6, 2 }, { 8, 4 } });
        }

        [TestMethod]
        public void TestRemoveOverlappingRanges()
        {
            var text = "abcd";
            var actualRanges = TypeScriptSupport.RemoveOverlappingRanges(
                text,
                new[] {
                    new ClassifiedRange(text, 0, 2),
                    new ClassifiedRange(text, 2, 2)
                });
        }

        private void T(
            string text,
            Dictionary<int, int> syntactic,
            Dictionary<int, int> semantic,
            Dictionary<int, int> expectedOutput)
        {
            Paths.SolutionDestinationFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var expectedArray = expectedOutput.ToArray();
            var result = TypeScriptSupport.PrepareRanges(
                syntactic.Select(p => new ClassifiedRange(text, p.Key, p.Value)).ToArray(),
                semantic.Select(p => new ClassifiedRange(text, p.Key, p.Value)).ToArray(),
                text);
            Assert.AreEqual(expectedOutput.Count, result.Length, "Lengths aren't same");
            for (int i = 0; i < expectedOutput.Count; i++)
            {
                Assert.AreEqual(expectedArray[i].Key, result[i].start);
                Assert.AreEqual(expectedArray[i].Value, result[i].length);
            }
        }
    }
}
