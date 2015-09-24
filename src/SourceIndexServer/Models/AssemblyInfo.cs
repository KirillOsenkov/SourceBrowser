namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public struct AssemblyInfo
    {
        public string AssemblyName;
        public short ProjectKey;
        public short ReferencingAssembliesCount;

        public AssemblyInfo(string line)
        {
            var parts = line.Split(';');
            AssemblyName = parts[0];
            ProjectKey = short.Parse(parts[1]);
            ReferencingAssembliesCount = short.Parse(parts[2]);
        }
    }
}
