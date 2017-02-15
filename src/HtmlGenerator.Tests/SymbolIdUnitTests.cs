using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class SymbolIdUnitTests
    {
        [TestMethod]
        public void TestHash()
        {
            var symbolId = "T:Microsoft.CodeAnalysis.CSharp.Symbols.SourceNamedTypeSymbol";
            var bytes = Paths.GetMD5Hash(symbolId, 16);
            Assert.AreEqual("a7aec3faae7fe65b", bytes);
        }
    }
}
