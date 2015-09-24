using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var analyzer = new TypeScriptAnalyzer(@"TypeScript\analyzer.js"))
            {
                var start = Stopwatch.StartNew();
                var contents = File.ReadAllText("TypeScriptAnalyzerArguments.json");
                string[] files = JsonConvert.DeserializeObject<string[]>(contents);
                var result = analyzer.Analyze(files);
                Console.WriteLine(start.Elapsed);
            }
        }
    }
}
