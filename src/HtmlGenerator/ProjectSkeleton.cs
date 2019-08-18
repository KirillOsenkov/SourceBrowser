namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class ProjectSkeleton
    {
        public string AssemblyName { get; }
        public string Name { get; }

        public ProjectSkeleton(string assemblyName, string name)
        {
            AssemblyName = assemblyName;
            Name = name;
        }
    }
}