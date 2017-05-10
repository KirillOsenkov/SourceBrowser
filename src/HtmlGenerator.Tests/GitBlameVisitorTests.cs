using System.Collections.Generic;
using System.IO;
using GitGlyph;
using LibGit2Sharp;
using Microsoft.SourceBrowser.HtmlGenerator.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HtmlGenerator.Tests
{
    [TestClass]
    public class GitBlameVistorTests
    {
        [TestMethod]
        public void TestVisit()
        {
            string pathToClonedRepo = Path.Combine(Path.GetTempPath(), "GitBlameVisitorRepo");
            string pathToTestFile = Path.Combine(pathToClonedRepo, "Test.cs");
            string pathToBareRepo = Path.Combine("Repositories", "GitBlameVisitorRepo.git");
            try
            {
                Repository.Clone(pathToBareRepo, pathToClonedRepo);
                Repository repo = new Repository(pathToClonedRepo);
                //Test.cs contains:
                //bd46cc8 public class Test
                //bd46cc8 {
                //909a6197  private int test;
                //11933d2a  private int myInt;
                //bd46cc8 }
                Dictionary<string, string> context = new Dictionary<string, string>
                {
                    {Microsoft.SourceBrowser.MEF.ContextKeys.FilePath, pathToTestFile},
                    {Microsoft.SourceBrowser.MEF.ContextKeys.LineNumber, "3"}
                };
                string expectedThirdLineSha1 = "909a6197";
                GitBlameVisitor visitor = new GitBlameVisitor(repo, new PluginLogger());

                string htmlResult = visitor.Visit(null, context);

                Assert.IsTrue(htmlResult.Contains(expectedThirdLineSha1));
            }
            finally
            {
                DeleteDirectory(pathToClonedRepo);
            }
        }

        /// <summary>
        /// Recursively unsets the readonly bit for all files and repositories in a directory and delete this directory
        /// Necessary to remove Git Repositories
        /// </summary>
        /// <param name="directoryPath">Directory to process</param>
        private static void DeleteDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }
            var files = Directory.GetFiles(directoryPath);
            var directories = Directory.GetDirectories(directoryPath);
            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            foreach (var dir in directories)
            {
                DeleteDirectory(dir);
            }
            File.SetAttributes(directoryPath, FileAttributes.Normal);
            Directory.Delete(directoryPath, false);
        }
    }
}
