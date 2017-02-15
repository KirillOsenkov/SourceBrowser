using System;
using System.IO;

namespace Microsoft.SourceBrowser.Common
{
    public enum FileError
    {
        NotFound,
        AccessDenied,
        OK
    }
    public class FileUtilities
    {
        public static FileError GetFileStatus(string filePath)
        {
            FileStream stream = null;
            FileInfo file = new FileInfo(filePath);

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (Exception e) when ((e is DirectoryNotFoundException) || (e is FileNotFoundException))
            {
                //the file does not exist
                return FileError.NotFound;
            }
            catch (Exception e) when (e is UnauthorizedAccessException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                return FileError.AccessDenied;
            }
            catch (Exception e)
            {

            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return FileError.OK;
        }
        public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new ArgumentException("Source directory doesn't exist:" + sourceDirectory);
            }

            sourceDirectory = sourceDirectory.TrimSlash();

            if (string.IsNullOrEmpty(destinationDirectory))
            {
                throw new ArgumentNullException("destinationDirectory");
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
