using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class MetadataAsSourceUnitTests
    {
        [TestMethod]
        public void TestMAS1()
        {
            var filePath = Assembly.GetExecutingAssembly().Location;
            var solution = MetadataAsSource.LoadMetadataAsSourceSolution(filePath);
            var project = solution.Projects.First();
            var documents = project.Documents.ToArray();
            var texts = documents.Select(d => d.GetTextAsync().Result.ToString()).ToArray();
        }

        [TestMethod]
        public void TestAssemblyAttributes1()
        {
            var filePath = Assembly.GetExecutingAssembly().Location;
            var attributes = MetadataReading.GetAssemblyAttributes(filePath);
        }
    }
}
