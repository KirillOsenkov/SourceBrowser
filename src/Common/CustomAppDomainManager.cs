#if NET472

using System;
using System.Linq;
using System.Reflection;
using System.Security.Policy;

namespace Microsoft.SourceBrowser.Common
{
    /// <summary>
    /// We want a callback when an appdomain is created, to install the MSBuildLocator
    /// into the XAML markup compilation appdomain and other appdomains, such as ones
    /// created by the ResolveCOMReference task.
    /// See https://github.com/KirillOsenkov/AppDomainManagerTest
    /// </summary>
    /// <remarks>
    /// This class can't be in the entrypoint assembly because of a CLR bug.
    /// We need a way to callback the entrypoint assembly when a new appdomain 
    /// is created. Let's just instantiate a well-known type from the 
    /// entrypoint assembly.
    /// </remarks>
    public class CustomAppDomainManager : AppDomainManager
    {
        /// <summary>
        /// This runs in the old appdomain when a new appdomain is created
        /// </summary>
        public override AppDomain CreateDomain(string friendlyName, Evidence securityInfo, AppDomainSetup appDomainInfo)
        {
            var result = base.CreateDomain(friendlyName, securityInfo, appDomainInfo);
            return result;
        }

        /// <summary>
        /// This runs in the new appdomain very early when it is being initialized
        /// </summary>
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            NotifyEntrypointAssembly();
        }

        private void NotifyEntrypointAssembly()
        {
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                return;
            }

            var assembly = Assembly.Load("HtmlGenerator");
            if (assembly == null)
            {
                return;
            }

            var type = assembly.GetType("Microsoft.SourceBrowser.HtmlGenerator.AppDomainInitializer");
            if (type == null)
            {
                return;
            }

            Activator.CreateInstance(type);
        }
    }
}

#endif