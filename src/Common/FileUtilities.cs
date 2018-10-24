using System;
using System.IO;

namespace Microsoft.SourceBrowser.Common
{
    public static class FileUtilities
    {
        public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new ArgumentException("Source directory doesn't exist:" + sourceDirectory);
            }

            sourceDirectory = sourceDirectory.TrimSlash();

            if (string.IsNullOrEmpty(destinationDirectory))
            {
                throw new ArgumentNullException(nameof(destinationDirectory));
            }

            destinationDirectory = destinationDirectory.TrimSlash();

            var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relative = file.Substring(sourceDirectory.Length + 1);
                var destination = Path.Combine(destinationDirectory, relative);
                CopyFile(file, destination);
            }
        }

        public static void CopyFile(string sourceFilePath, string destinationFilePath, bool overwrite = false)
        {
            if (!File.Exists(sourceFilePath))
            {
                return;
            }

            if (!overwrite && File.Exists(destinationFilePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(destinationFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite);
            File.SetAttributes(destinationFilePath, File.GetAttributes(destinationFilePath) & ~FileAttributes.ReadOnly);
        }
    }
}
