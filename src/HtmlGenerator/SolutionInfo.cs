using System.Collections.Generic;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class SolutionInfo
    {
        public string SlnPath { get; set; }
        public string UrlRoot { get; set; }
        public Dictionary<string, string> MSBuildProperties { get; set; }
        public bool NuGetRestore { get; internal set; }
    }
}
