using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.MEF
{
    public class PluginAggregator : IReadOnlyCollection<SourceBrowserPluginWrapper>, IDisposable
    {
        private CompositionContainer container;

        [ImportMany]
#pragma warning disable CS0649
        IEnumerable<Lazy<ISourceBrowserPlugin, ISourceBrowserPluginMetadata>> plugins;
#pragma warning restore CS0649
        private List<SourceBrowserPluginWrapper> Plugins;
        private ILog Logger;

        private Dictionary<string, Dictionary<string, string>> PluginConfigurations;

        public int Count => Plugins.Count;

        public PluginAggregator(Dictionary<string, Dictionary<string, string>> pluginConfigurations, ILog logger, IEnumerable<string> blackList)
        {
            PluginConfigurations = pluginConfigurations;
            Logger = logger;

            // Create the CompositionContainer with the parts in the catalog
            container = new CompositionContainer(new DirectoryCatalog(AppDomain.CurrentDomain.BaseDirectory));

            // Fill the imports of this object
            container.ComposeParts(this);

            var blackListSet = new HashSet<string>(blackList ?? Array.Empty<string>());

            Plugins = plugins
            .Select(pair => new SourceBrowserPluginWrapper(pair.Value, pair.Metadata, Logger))
            .Where(w => !blackListSet.Contains(w.Name))
            .ToList();
        }

        public void Init()
        {
            foreach (var plugin in Plugins)
            {
                if (!PluginConfigurations.TryGetValue(plugin.Name, out Dictionary<string, string> config))
                {
                    config = new Dictionary<string, string>();
                }
                plugin.Init(config, Logger);
            }
        }

        public IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(Project project)
        {
            return Plugins.SelectMany(p => p.ManufactureSymbolVisitors(project.FilePath));
        }

        private IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(string name, ISourceBrowserPlugin plugin, Project project)
        {
            try
            {
                return plugin.ManufactureSymbolVisitors(project.FilePath);
            }
            catch (Exception ex)
            {
                Logger.Info(name + " Plugin failed to manufacture symbol visitors", ex);
                return Enumerable.Empty<ISymbolVisitor>();
            }
        }

        public IEnumerable<ITextVisitor> ManufactureTextVisitors(Project project)
        {
            return Plugins.SelectMany(p => p.ManufactureTextVisitors(project.FilePath));
        }

        public void Dispose() => container?.Dispose();

        public IEnumerator<SourceBrowserPluginWrapper> GetEnumerator() => Plugins.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Plugins.GetEnumerator();
    }
}
