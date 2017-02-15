using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Federation
    {
        public static IEnumerable<string> FederatedIndexUrls = new[]
        {
            @"http://referencesource.microsoft.com",
            @"http://source.roslyn.io"
        };

        private class Info
        {
            public Info(string server, HashSet<string> assemblies)
            {
                if (server == null)
                {
                    throw new ArgumentNullException(nameof(server));
                }

                if (assemblies == null)
                {
                    throw new ArgumentNullException(nameof(assemblies));
                }

                if (!server.StartsWith("http://"))
                {
                    server = "http://" + server;
                }

                if (!server.EndsWith("/"))
                {
                    server += "/";
                }

                Server = server;
                Assemblies = assemblies;
            }

            public string Server { get; }
            public HashSet<string> Assemblies { get; }
        }

        private readonly List<Info> federations = new List<Info>();

        public Federation() : this(FederatedIndexUrls)
        {
        }

        public Federation(IEnumerable<string> servers) : this(servers.ToArray())
        {
        }

        public Federation(params string[] servers)
        {
            if (servers == null || servers.Length == 0)
            {
                return;
            }

            foreach (var server in servers)
            {
                AddFederation(server);
            }
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
