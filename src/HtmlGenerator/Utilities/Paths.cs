using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public static class Paths
    {
        private static string solutionDestinationFolder;
        public static string SolutionDestinationFolder
        {
            get { return solutionDestinationFolder; }
            set { solutionDestinationFolder = value.MustBeAbsolute(); }
        }

        public static string ProcessedAssemblies
        {
            get
            {
                var root = SolutionDestinationFolder;
                if (root == null)
                {
                    root = Common.Paths.BaseAppFolder;
                }

                return Path.Combine(root, @"ProcessedAssemblies.txt");
            }
        }

        public static HashSet<string> LoadProcessedAssemblies()
        {
            HashSet<string> processed = null;
            if (File.Exists(Paths.ProcessedAssemblies))
            {
                processed = new HashSet<string>(File.ReadAllLines(Paths.ProcessedAssemblies), StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return processed;
        }

        public static string AssemblyPathsFile
        {
            get
            {
                return Path.Combine(Microsoft.SourceBrowser.Common.Paths.BaseAppFolder, Constants.AssemblyPaths);
            }
        }

        public static void PrepareDestinationFolder(bool forceOverwrite = false)
        {
            if (!Configuration.CreateFoldersOnDisk &&
                !Configuration.WriteDocumentsToDisk &&
                !Configuration.WriteProjectAuxiliaryFilesToDisk)
            {
                return;
            }

            if (Directory.Exists(SolutionDestinationFolder))
            {
                if (!forceOverwrite)
                {
                    Log.Write(string.Format("Warning, {0} will be deleted! Are you sure? (y/n)", SolutionDestinationFolder), ConsoleColor.Red);
                    if (Console.ReadKey().KeyChar != 'y')
                    {
                        if (!File.Exists(Paths.ProcessedAssemblies))
                        {
                            Environment.Exit(0);
                        }

                        Log.Write("Would you like to continue previously aborted index operation where it left off?", ConsoleColor.Green);
                        if (Console.ReadKey().KeyChar != 'y')
                        {
                            Environment.Exit(0);
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }

                Log.Write("Deleting " + SolutionDestinationFolder);
                Directory.Delete(SolutionDestinationFolder, recursive: true);
            }

            Directory.CreateDirectory(SolutionDestinationFolder);
        }

        /// <summary>
        /// Returns a path to <paramref name="filePath"/> if you start in a folder where the file
        /// <paramref name="relativeToPath"/> is located.
        /// </summary>
        /// <param name="filePath">C:\A\B\1.txt</param>
        /// <param name="relativeToPath">C:\C\D\2.txt</param>
        /// <returns>..\..\A\B\1.txt</returns>
        public static string MakeRelativeToFile(string filePath, string relativeToPath)
        {
            relativeToPath = Path.GetDirectoryName(relativeToPath);
            string result = MakeRelativeToFolder(filePath, relativeToPath);
            return result;
        }

        /// <summary>
        /// Returns a path to <paramref name="filePath"/> if you start in folder <paramref name="relativeToPath"/>.
        /// </summary>
        /// <param name="filePath">C:\A\B\1.txt</param>
        /// <param name="relativeToPath">C:\C\D</param>
        /// <returns>..\..\A\B\1.txt</returns>
        public static string MakeRelativeToFolder(string filePath, string relativeToPath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (string.IsNullOrEmpty(relativeToPath))
            {
                throw new ArgumentNullException(nameof(relativeToPath));
            }

            // the file is on a different drive
            if (filePath[0] != relativeToPath[0])
            {
                // better than crashing
                return Path.GetFileName(filePath);
            }

            if (relativeToPath.EndsWith("\\"))
            {
                relativeToPath = relativeToPath.TrimEnd('\\');
            }

            StringBuilder result = new StringBuilder();
            while (!EnsureTrailingSlash(filePath).StartsWith(EnsureTrailingSlash(relativeToPath), StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"..\");
                relativeToPath = Path.GetDirectoryName(relativeToPath);
            }

            if (filePath.Length > relativeToPath.Length)
            {
                filePath = filePath.Substring(relativeToPath.Length);
                if (filePath.StartsWith("\\"))
                {
                    filePath = filePath.Substring(1);
                }

                result.Append(filePath);
            }

            return result.ToString();
        }

        public static string GetRelativeFilePathInProject(Document document)
        {
            string result = Path.Combine(document.Folders
                .Select(SanitizeFolder)
                .ToArray());

            string fileName;
            if (document.FilePath != null)
            {
                fileName = Path.GetFileName(document.FilePath);
            }
            else
            {
                fileName = document.Name;
            }

            result = Path.Combine(result, fileName);

            return result;
        }

        private static char[] invalidFileChars = Path.GetInvalidFileNameChars();
        private static char[] invalidPathChars = Path.GetInvalidPathChars();

        public static string SanitizeFileName(string fileName)
        {
            return ReplaceInvalidChars(fileName, invalidFileChars);
        }

        private static string ReplaceInvalidChars(string fileName, char[] invalidChars)
        {
            var sb = new StringBuilder(fileName.Length);
            for (int i = 0; i < fileName.Length; i++)
            {
                if (invalidChars.Contains(fileName[i]))
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(fileName[i]);
                }
            }

            return sb.ToString();
        }

        public static string SanitizeFolder(string folderName)
        {
            string result = folderName;

            if (folderName == ".")
            {
                result = "current";
            }
            else if (folderName == "..")
            {
                result = "parent";
            }
            else if (folderName.EndsWith(":"))
            {
                result = folderName.TrimEnd(':');
            }
            else
            {
                result = folderName;
            }

            result = ReplaceInvalidChars(result, invalidPathChars);
            return result;
        }

        private static bool IsValidFolder(string folderName)
        {
            return !string.IsNullOrEmpty(folderName) &&
                folderName != "." &&
                folderName != ".." &&
                !folderName.EndsWith(":");
        }

        public static string GetRelativePathInProject(SyntaxTree syntaxTree, Project project)
        {
            var document = project.GetDocument(syntaxTree);
            return GetRelativeFilePathInProject(document);
        }

        public static string EnsureTrailingSlash(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }

            return path;
        }

        public static string GetCssPathFromFile(string solutionDestinationPath, string fileName)
        {
            string result = MakeRelativeToFile(solutionDestinationPath, fileName);
            result = Path.Combine(result, "styles.css");
            result = result.Replace('\\', '/');
            return result;
        }

        public static string GetMD5Hash(string input, int digits)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(bytes);
                return Serialization.ByteArrayToHexString(hashBytes, digits);
            }
        }

        public static ulong GetMD5HashULong(string input, int digits)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(bytes);
                return BitConverter.ToUInt64(hashBytes, 0);
            }
        }

        public static string StripExtension(string fileName)
        {
            return Path.ChangeExtension(fileName, null);
        }

        public static string GetDocumentDestinationPath(Document document, string projectDestinationFolder)
        {
            var documentRelativeFilePathWithoutHtmlExtension = GetRelativeFilePathInProject(document);
            var documentDestinationFilePath = Path.Combine(projectDestinationFolder, documentRelativeFilePathWithoutHtmlExtension) + ".html";
            return documentDestinationFilePath;
        }

        public static string CalculateRelativePathToRoot(string filePath, string rootFolder)
        {
            var relativePath = filePath.Substring(rootFolder.Length + 1);
            var depth = relativePath.Count(c => c == '\\') + relativePath.Count(c => c == '/');
            var sb = new StringBuilder();
            for (int i = 0; i < depth; i++)
            {
                sb.Append("../");
            }

            return sb.ToString();
        }

        /// <summary>
        /// This makes sure that a filePath that can be outside the folder is replanted inside the folder.
        /// This is important when a project references a file outside the project cone and we want to
        /// display it as if it is inside the project.
        /// </summary>
        public static string GetFullPathInFolderCone(string folder, string filePath)
        {
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(folder, filePath);
            }

            return GetFullPathInFolderConeForRootedFilePath(folder, filePath);
        }

        private static string GetFullPathInFolderConeForRootedFilePath(string folder, string rootedFilePath)
        {
            folder = Path.GetFullPath(folder);
            rootedFilePath = Path.GetFullPath(rootedFilePath);
            if (rootedFilePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                return rootedFilePath;
            }

            var folderParts = folder.Split(Path.DirectorySeparatorChar);
            var rootedFilePathParts = rootedFilePath.Split(Path.DirectorySeparatorChar);
            int commonParts = 0;
            for (int i = 0; i < Math.Min(folderParts.Length, rootedFilePathParts.Length); i++)
            {
                if (string.Equals(folderParts[i], rootedFilePathParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    commonParts++;
                }
                else
                {
                    break;
                }
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), rootedFilePathParts.Skip(commonParts));
            relativePath = relativePath.Replace(":", "");
            rootedFilePath = Path.Combine(folder, relativePath);
            return rootedFilePath;
        }
    }
}
