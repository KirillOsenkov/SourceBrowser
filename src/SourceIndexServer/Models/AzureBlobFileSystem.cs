using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Microsoft.SourceBrowser.SourceIndexServer.Models
{
    public class AzureBlobFileSystem : IFileSystem
    {
        private class AnonContainerClient : BlobContainerClient
        {
            public override Uri Uri { get; }
            protected override HttpPipeline Pipeline { get; }

            public AnonContainerClient(string uri)
            {
                Uri = new Uri(uri);
                Pipeline = HttpPipelineBuilder.Build(new BlobClientOptions());
            }

            public override BlobClient GetBlobClient(string blobName)
            {
                return (BlobClient)typeof(BlobClient).GetTypeInfo().DeclaredConstructors.Single(ctor => ctor.ToString() == "Void .ctor(System.Uri, Azure.Core.Pipeline.HttpPipeline)").Invoke(new object[] { AppendToPath(this.Uri, blobName), this.Pipeline });
            }

            private static Uri AppendToPath(Uri uri, string segment)
            {
                UriBuilder uriBuilder = new UriBuilder(uri);
                string path = uriBuilder.Path;
                uriBuilder.Path = uriBuilder.Path + (path.Length == 0 || path[path.Length - 1] != '/' ? "/" : "") + segment;
                return uriBuilder.Uri;
            }
        }

        private readonly BlobContainerClient container;
        public AzureBlobFileSystem(string uri)
        {
            container = new AnonContainerClient(uri);
        }

        public bool DirectoryExists(string name)
        {
            return true;
        }

        public IEnumerable<string> ListFiles(string dirName)
        {
            dirName = dirName.ToLowerInvariant();
            dirName = dirName.Replace("\\", "/");
            if (!dirName.EndsWith("/"))
            {
                dirName += "/";
            }

            return container.GetBlobsByHierarchy("/", new GetBlobsOptions
            {
                Prefix = dirName,
            }).Select(res => res.Value).Where(item => item.IsBlob).Select(item => item.Blob.Name).ToList();
        }

        public bool FileExists(string name)
        {
            name = name.ToLowerInvariant();
            var blob = container.GetBlobClient(name);
            try
            {
                blob.GetProperties();
                return true;
            }
            catch (StorageRequestFailedException ex) when (string.Equals(ex.ErrorCode, "BlobNotFound"))
            {
                return false;
            }
        }

        public Stream OpenSequentialReadStream(string name)
        {
            name = name.ToLowerInvariant();
            var blob = container.GetBlobClient(name);
            return blob.Download().Value.Content;
        }

        public IEnumerable<string> ReadLines(string name)
        {
            name = name.ToLowerInvariant();
            var blob = container.GetBlobClient(name);
            using (var stream = blob.Download().Value.Content)
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    yield return reader.ReadLine();
                }
            }
        }
    }
}