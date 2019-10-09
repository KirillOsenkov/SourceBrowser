using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.SourceBrowser.SourceIndexServer
{
    public static class Helpers
    {
        public static async Task ProxyRequestAsync(this HttpContext context, HttpClient client, string targetUrl, Action<HttpRequestMessage> configureRequest = null)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, targetUrl))
            {
                foreach (var (key, values) in context.Request.Headers)
                {
                    switch (key.ToLower())
                    {
                        // We shouldn't copy any of these request headers
                        case "host":
                        case "authorization":
                        case "cookie":
                        case "content-length":
                        case "content-type":
                            continue;
                        default:
                            req.Headers.TryAddWithoutValidation(key, values.ToArray());
                            break;
                    }
                }

                configureRequest?.Invoke(req);

                HttpResponseMessage res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                context.Response.RegisterForDispose(res);

                foreach (var (key, values) in res.Headers)
                {
                    switch (key.ToLower())
                    {
                        // Remove headers that the response doesn't need
                        case "set-cookie":
                        case "x-powered-by":
                        case "x-aspnet-version":
                        case "server":
                        case "transfer-encoding":
                        case "access-control-expose-headers":
                        case "access-control-allow-origin":
                            continue;
                        default:
                            if (!context.Response.Headers.ContainsKey(key))
                            {
                                context.Response.Headers.Add(key, values.ToArray());
                            }

                            break;
                    }
                }

                context.Response.StatusCode = (int)res.StatusCode;
                if (res.Content != null)
                {
                    foreach (var (key, values) in res.Content.Headers)
                    {
                        if (!context.Response.Headers.ContainsKey(key))
                        {
                            context.Response.Headers.Add(key, values.ToArray());
                        }
                    }

                    using (var data = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        await data.CopyToAsync(context.Response.Body).ConfigureAwait(false);
                    }
                }
            }
        }

        private static readonly HttpClient s_client = new HttpClient();

        private static async Task<bool> UrlExistsAsync(string proxyRequestUrl)
        {
            using (var res = new HttpRequestMessage(HttpMethod.Head, proxyRequestUrl))
            using (var req = await s_client.SendAsync(res).ConfigureAwait(false))
            {
                if (req.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task ServeProxiedIndex(HttpContext context, Func<Task> next)
        {
            var path = context.Request.Path.ToUriComponent();

            if (!path.EndsWith(".html") && !path.EndsWith(".txt"))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var proxyUri = IndexProxyUrl;
            if (string.IsNullOrEmpty(proxyUri))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var proxyRequestUrl = proxyUri + (path.StartsWith("/") ? path : "/" + path).ToLowerInvariant();

            if (!await UrlExistsAsync(proxyRequestUrl).ConfigureAwait(false))
            {
                await next().ConfigureAwait(false);
                return;
            }

            await context.ProxyRequestAsync(s_client, proxyRequestUrl).ConfigureAwait(false);
        }

        public static string IndexProxyUrl => Environment.GetEnvironmentVariable("SOURCE_BROWSER_INDEX_PROXY_URL");
    }
}