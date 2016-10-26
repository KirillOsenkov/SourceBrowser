using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.MEF
{
    public class PluginAggregator : IReadOnlyDictionary<string, ISourceBrowserPlugin>, IDisposable
    {
        private CompositionContainer container;

        //private static Lazy<PluginAggregator> _instance = new Lazy<PluginAggregator>(() => new PluginAggregator());
        //public static PluginAggregator Instance { get { return _instance.Value; } }

        [ImportMany]
#pragma warning disable CS0649
        IEnumerable<Lazy<ISourceBrowserPlugin, ISourceBrowserPluginMetadata>> plugins;
#pragma warning restore CS0649
        private Dictionary<string, ISourceBrowserPlugin> Plugins;

        public PluginAggregator(Dictionary<string, Dictionary<string, string>> pluginConfigurations, ILog logger)
        {
            //Create the CompositionContainer with the parts in the catalog
            container = new CompositionContainer(new DirectoryCatalog(AppDomain.CurrentDomain.BaseDirectory));

            //Fill the imports of this object
            container.ComposeParts(this);

            Plugins = plugins.ToDictionary(l => l.Metadata.Name, l => l.Value);

            foreach (var pair in Plugins)
            {
                Dictionary<string, string> config;
                if (!pluginConfigurations.TryGetValue(pair.Key, out config))
                {
                    config = new Dictionary<string, string>();
                }
                pair.Value.Init(config, logger);
            }
        }

        public IEnumerable<ISymbolVisitor> ManufactureSymbolVisitors(Project project)
        {
            return Values.SelectMany(p => p.ManufactureSymbolVisitors(project.FilePath));
        }

        public IEnumerable<ITextVisitor> ManufactureTextVisitors(Project project)
        {
            return Values.SelectMany(p => p.ManufactureTextVisitors(project.FilePath));
        }

        public ISourceBrowserPlugin this[string key]
        {
            get
            {
                return Plugins[key];
            }
        }

        public int Count
        {
            get
            {
                return Plugins.Count;
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                return Plugins.Keys;
            }
        }

        public IEnumerable<ISourceBrowserPlugin> Values
        {
            get
            {
                return Plugins.Values;
            }
        }

        public bool ContainsKey(string key)
        {
            return Plugins.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, ISourceBrowserPlugin>> GetEnumerator()
        {
            return Plugins.GetEnumerator();
        }

        public bool TryGetValue(string key, out ISourceBrowserPlugin value)
        {
            return Plugins.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Plugins.GetEnumerator();
        }

        public void Dispose()
        {
            if (container != null)
                container.Dispose();
        }
    }
}
