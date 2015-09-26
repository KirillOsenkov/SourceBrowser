using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.SourceBrowser.HtmlGenerator.Utilities;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Federation
    {
        public static IEnumerable<string> FederatedIndexUrls = new[] { @"http://referencesource.microsoft.com", @"http://source.roslyn.io" };

        private List<HashSet<string>> assemblies = new List<HashSet<string>>();

        public Federation(IEnumerable<string> servers) : this(servers.ToArray())
        {
        }

        public Federation(params string[] servers)
        {
            WebProxyAuthenticator.Authenticate(this.GetAssemblyUrl(servers[0]));

            foreach (var server in servers)
            {
                var url = this.GetAssemblyUrl(server);

                var assemblyList = new WebClient().DownloadString(url);
                var assemblyNames = new HashSet<string>(assemblyList
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(';')[0]), StringComparer.OrdinalIgnoreCase);

                assemblies.Add(assemblyNames);
            }
        }

        private string GetAssemblyUrl(string server)
        {
            var url = server;
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            url += "Assemblies.txt";

            return url;
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
