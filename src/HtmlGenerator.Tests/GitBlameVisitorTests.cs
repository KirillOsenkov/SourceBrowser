using System;
using System.Collections.Generic;
using System.IO;
using GitGlyph;
using LibGit2Sharp;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.HtmlGenerator.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HtmlGenerator.Tests
{
    [TestClass]
    public class GitBlameVisitorTests
    {
        [TestMethod]
        public void TestVisit()
        {
            string pathToRepo = Path.Combine(Path.GetTempPath(),Path.GetRandomFileName());

            // Create the Git Repository for test
            Repository.Init(pathToRepo);
            var repo = new Repository(pathToRepo);

            // Create the committer's signature and commit
            Signature author = new Signature("User", "user@example.com", DateTime.Now);
            Signature committer = author;
            writeAndCommit("FirstLine", author, committer, repo, "First Commit");
            Commit commit2 = writeAndCommit("SecondLine", author, committer, repo, "Second Commit");

            try
            {
                Dictionary<string, string> context = new Dictionary<string, string>
                {
                    {Microsoft.SourceBrowser.MEF.ContextKeys.FilePath, Path.Combine(pathToRepo,"Test.txt")},
                    {Microsoft.SourceBrowser.MEF.ContextKeys.LineNumber, "2"}
                };
                string expectedSecondLineSha1 = commit2.Id.Sha;
                GitBlameVisitor visitor = new GitBlameVisitor(repo, new PluginLogger());

                string htmlResult = visitor.Visit(null, context);

                Assert.Contains(expectedSecondLineSha1, htmlResult);
            }
            finally
            {
                DeleteDirectory(pathToRepo);
            }
        }

        /// <summary>
        /// Write content in the file Test.txt, stage this file and finally commit this change to the repository.
        /// </summary>
        /// <param name="content">Line written in the the file</param>
        /// <param name="author">Signature of the author</param>
        /// <param name="committer">Signature of the committer</param>
        /// <param name="repo">Repository to commit to</param>
        /// <param name="commitMessage">Message of the commit</param>
        /// <returns>Commit of the change</returns>
        private Commit writeAndCommit(String content, Signature author, Signature committer, Repository repo, String commitMessage)
        {
            File.AppendAllLines(Path.Combine(repo.Info.WorkingDirectory, "Test.txt"), content.SplitBySpacesConsideringQuotes());
            Commands.Stage(repo, "Test.txt");
            return repo.Commit(commitMessage, author, committer);
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
