using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Microsoft.SourceBrowser.MEF
{
    /// <summary>
    /// A wrapper around MEF-imported objects to prevent exceptions from bubbling up
    /// </summary>
    public class SourceBrowserPluginWrapper : ISourceBrowserPlugin, ISourceBrowserPluginMetadata
    {
        private ISourceBrowserPluginMetadata Metadata;
        private ISourceBrowserPlugin Plugin;
        private ILog Logger;

        public SourceBrowserPluginWrapper(ISourceBrowserPlugin plugin, ISourceBrowserPluginMetadata metadata, ILog logger)
        {
            Plugin = plugin;
            Metadata = metadata;
            Logger = logger;
        }

        public string Name
        {
            get
            {
                try
                {
                    return Metadata.Name;
                }
                catch (Exception ex)
                {
                    Logger.Info("Couldn't retrieve plugin name", ex);
                    return "Unknown Plugin";
                }
            }
        }

        public void Init(Dictionary<string, string> configuration, ILog logger)
        {
            try
            {
                Plugin.Init(configuration, logger);
            }
            catch (Exception ex)
            {
                Logger.Info(Name + " plugin failed to initialize", ex);
            }
        }

        public IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(string projectPath)
        {
            try
            {
                return Plugin.ManufactureSymbolVisitors(projectPath);
            }
            catch (Exception ex)
            {
                Logger.Info(Name + " plugin failed to manufacture symbol visitors", ex);
                return Enumerable.Empty<ISymbolVisitor>();
            }
        }

        public IEnumerable<ITextVisitor> ManufactureTextVisitors(string projectPath)
        {
            try
            {
                return Plugin.ManufactureTextVisitors(projectPath);
            }
            catch (Exception ex)
            {
                Logger.Info(Name + " plugin failed to manufacture text visitors", ex);
                return Enumerable.Empty<ITextVisitor>();
            }
        }

        public Module PluginModule
        {
            get
            {
                return Plugin.GetType().Module;
            }
        }
    }
}
