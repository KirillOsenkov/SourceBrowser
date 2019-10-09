using System.Collections.Generic;
using System.IO;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public interface IFileSystem
    {
        bool DirectoryExists(string name);
        IEnumerable<string> ListFiles(string dirName);
        bool FileExists(string name);
        Stream OpenSequentialReadStream(string name);
        IEnumerable<string> ReadLines(string name);
    }
}