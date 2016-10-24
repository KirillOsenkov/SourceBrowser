using System;
using System.Collections.Generic;
using MEF;
using Microsoft.CodeAnalysis;
using System.ComponentModel.Composition;
using System.Linq;
using LibGit2Sharp;

namespace GitGlyph
{
    [Export(typeof(ISourceBrowserPlugin))]
    [ExportMetadata("Name", "Git")]
    public class GitSourceBrowserPlugin : ISourceBrowserPlugin, IDisposable
    {
        private ILog Logger;
        private List<Repository> RepositoriesToDispose;

        public GitSourceBrowserPlugin()
        {
            RepositoriesToDispose = new List<Repository>();
        }

        public void Dispose()
        {
            foreach ( var r in RepositoriesToDispose )
            {
                r.Dispose();
            }
        }

        public void Init(Dictionary<string, string> Configuration, ILog logger)
        {
            Logger = logger;
        }

        public IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(string projectPath)
        {
            yield break;
        }

        public IEnumerable<ITextVisitor> ManufactureTextVisitors(string projectPath)
        {
            var path = Repository.Discover(projectPath);
            if ( path == null )
            {
                Logger.Warning("Cannot find git repo");
            } else {
                Repository r = new Repository(path);
                RepositoriesToDispose.Add(r);
                yield return new GitBlameVisitor(r, Logger);
            }
        }
    }
}
