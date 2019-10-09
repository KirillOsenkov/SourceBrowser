using System.Collections.Generic;
using System.IO;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class StaticFileSystem : IFileSystem
    {
        private readonly string rootPath;

        public StaticFileSystem(string rootPath)
        {
            this.rootPath = rootPath;
        }
        public bool DirectoryExists(string name)
        {
            return Directory.Exists(Path.Combine(rootPath, name));
        }

        public IEnumerable<string> ListFiles(string dirName)
        {
            return Directory.GetFiles(Path.Combine(rootPath, dirName));
        }

        public bool FileExists(string name)
        {
            return File.Exists(Path.Combine(rootPath, name));
        }

        public Stream OpenSequentialReadStream(string name)
        {
            var path = Path.Combine(rootPath, name);
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None,
                262144,
                FileOptions.SequentialScan);
        }

        public IEnumerable<string> ReadLines(string name)
        {
            return File.ReadLines(Path.Combine(rootPath, name));
        }
    }
}