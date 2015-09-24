using System.IO;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class XamlSupport : XmlSupport
    {
        private ProjectGenerator projectGenerator;
        private string relativePath;

        public XamlSupport(ProjectGenerator projectGenerator)
        {
            this.projectGenerator = projectGenerator;
        }

        internal void GenerateXaml(string sourceXmlFile, string destinationHtmlFile, string relativePath)
        {
            this.relativePath = relativePath;
            base.Generate(sourceXmlFile, destinationHtmlFile, projectGenerator.SolutionGenerator.SolutionDestinationFolder);
        }

        protected override string GetAssemblyName()
        {
            return projectGenerator.AssemblyName;
        }

        protected override string GetDisplayName()
        {
            return relativePath;
        }
    }
}
