using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Federation
    {
        public static IEnumerable<string> DefaultFederatedIndexUrls = new[]
        {
            "https://referencesource.microsoft.com",
            "http://sourceroslyn.io"
        };

        private class Info
        {
            public Info(string server, HashSet<string> assemblies)
            {
                if (server == null)
                {
                    throw new ArgumentNullException(nameof(server));
                }

                if (!server.EndsWith("/"))
                {
                    server += "/";
                }

                Server = server;
                Assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
            }

            public string Server { get; }
            public HashSet<string> Assemblies { get; }
        }

        private readonly List<Info> federations = new List<Info>();

        static Federation()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        public Federation()
        {
        }

        public Federation(IEnumerable<string> servers) : this(servers.ToArray())
        {
        }

        public Federation(params string[] servers)
        {
            AddFederations(servers);
        }

        public void AddFederations(IEnumerable<string> servers)
        {
            if (servers == null)
            {
                return;
            }

            foreach (var server in servers)
            {
                AddFederation(server);
            }
        }

        public void AddFederations(params string[] servers)
        {
            AddFederations((IEnumerable<string>)servers);
        }

        public void AddFederation(string server)
        {
            var url = GetAssemblyUrl(server);

            var assemblyList = new WebClient().DownloadString(url);
            var assemblyNames = GetAssemblyNames(assemblyList);

            federations.Add(new Info(server, assemblyNames));
        }

        public void AddFederation(string server, string assemblyListFile)
        {
            var fileText = File.ReadAllText(assemblyListFile);
            var assemblyNames = GetAssemblyNames(fileText);
            var info = new Info(server, assemblyNames);
            federations.Add(info);
        }

        private HashSet<string> GetAssemblyNames(string assemblyList)
        {
            var assemblyNames = new HashSet<string>(assemblyList
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(';')[0]), StringComparer.OrdinalIgnoreCase);
            return assemblyNames;
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
            // Order must match order in GetServers().
            for (int i = 0; i < federations.Count; i++)
            {
                if (federations[i].Assemblies.Contains(assemblyName))
                {
                    return i;
                }
            }

            return -1;
        }

        public IEnumerable<string> GetServers()
        {
            // Order must match order in GetExternalAssemblyIndex().
            for (int i = 0; i < federations.Count; i++)
            {
                yield return federations[i].Server;
            }
        }
    }
}
