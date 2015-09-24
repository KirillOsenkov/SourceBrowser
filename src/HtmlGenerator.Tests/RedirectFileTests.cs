using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class RedirectFileTests
    {
        //[TestMethod]
        public void TestRedirectFileGeneration()
        {
            var originalFile = @"E:\Index\mscorlib\A.html";
            var lines = File.ReadAllLines(originalFile);

            var list = new List<KeyValuePair<string, string>>();
            var map = new Dictionary<string, string>();

            foreach (var line in lines)
            {
                if (line.Length > 25 && line[0] == 'm')
                {
                    if (line[22] == '"')
                    {
                        var id = line.Substring(3, 16);
                        var file = line.Substring(23, line.Length - 25);
                        list.Add(new KeyValuePair<string, string>(id, file));
                        map[id] = file;
                    }
                    else if (line[22] == 'm')
                    {
                        var id = line.Substring(3, 16);
                        var other = line.Substring(25, 16);
                        list.Add(new KeyValuePair<string, string>(id, map[other]));
                    }
                }
            }

            Microsoft.SourceBrowser.HtmlGenerator.ProjectGenerator.GenerateRedirectFile(
                @"E:\Solution",
                @"E:\Solution\Project",
                list.ToDictionary(kvp => kvp.Key, kvp => (IEnumerable<string>)new List<string> { kvp.Value }));
        }
    }
}
