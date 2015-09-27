using System;
using System.Net;
using System.Security;

namespace Microsoft.SourceBrowser.HtmlGenerator.Utilities
{
    /// <summary>
    /// WebProxyAuthenticator will determine if proxy authentication is required.
    /// If it is, the user will be asked for the credentials.
    /// </summary>
    public class WebProxyAuthenticator
    {

        private static bool hasAuthenticated = false;

        /// <summary>
        /// Determine if there is a proxy that requires credentials.
        /// If there is, ask the user for the credentials and apply them to WebRequest.DefaultWebProxy
        /// </summary>
        /// <param name="url">URL to be used for testing if proxy auth is required</param>
        public static void Authenticate(string url)
        {
            while (!ProxyAuthSuccess(url))
            {
                InitialiseProxyAuth();
            }
        }

        private static bool ProxyAuthSuccess(string url)
        {
            if (hasAuthenticated)
            {
                return true;
            }

            try
            {
                new WebClient().DownloadString(url);
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    return false;
                }
            }

            hasAuthenticated = true;
            return true;
        }

        private static void InitialiseProxyAuth()
        {
            Console.WriteLine("Proxy authentication required:");
            
            Console.Write("Username: ");
            var username = Console.ReadLine();

            Console.Write("Password: ");
            var password = GetPassword();
            Console.WriteLine();
            Console.WriteLine();
            
            var credentials = new NetworkCredential(username, password);

            WebRequest.DefaultWebProxy.Credentials = credentials;
        }

        /// <summary>
        /// Get a password from the console
        /// </summary>
        /// <returns>Password stored in a SecureString</returns>
        private static SecureString GetPassword()
        {
            var password = new SecureString();
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.RemoveAt(password.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    password.AppendChar(keyInfo.KeyChar);
                    Console.Write("*");
                }
            }
            return password;
        }
    }
}
