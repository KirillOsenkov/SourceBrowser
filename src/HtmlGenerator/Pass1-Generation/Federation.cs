using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Federation
    {
        private readonly string[] servers;
        private List<HashSet<string>> assemblies = new List<HashSet<string>>();

        public Federation(params string[] servers)
        {
            this.servers = servers;
            foreach (var server in servers)
            {
                var url = server;
                if (!url.EndsWith("/"))
                {
                    url += "/";
                }

                var assemblyList = new WebClient().DownloadString(url + "Assemblies.txt");
                var assemblyNames = new HashSet<string>(assemblyList
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(';')[0]), StringComparer.OrdinalIgnoreCase);

                assemblies.Add(assemblyNames);
            }
        }

        public int GetExternalAssemblyIndex(string assemblyName)
        {
            for (int i = 0; i < assemblies.Count; i++)
            {
                if (assemblies[i].Contains(assemblyName))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
