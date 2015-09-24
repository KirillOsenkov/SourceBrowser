using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class SerializationUnitTests
    {
        [TestMethod]
        public void TestULongToHexStringRoundtrip()
        {
            for (int i = 0; i < 1000; i++)
            {
                var originalStringId = Paths.GetMD5Hash(i.ToString(), 16);
                var id = Paths.GetMD5HashULong(i.ToString(), 16);
                var stringId = Serialization.ULongToHexString(id);
                Assert.AreEqual(originalStringId, stringId);
                Assert.AreEqual(16, stringId.Length);
                var actualId = Serialization.HexStringToULong(stringId);
                Assert.AreEqual(id, actualId);
            }
        }
    }
}
