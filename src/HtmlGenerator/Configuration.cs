namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Configuration
    {
        // useful knobs to suppress stuff
        public static readonly bool GenerateMetadataAsSourceBodies = true;
        public static readonly bool CalculateRoslynSemantics = true;
        public static readonly bool WriteDocumentsToDisk = true;
        public static readonly bool WriteProjectAuxiliaryFilesToDisk = true;
        public static readonly bool CreateFoldersOnDisk = true;
    }
}
