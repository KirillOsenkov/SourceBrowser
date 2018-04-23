using System;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public static class WorkspaceHacks
    {
        public static HostServices Pack { get; set; }

        static WorkspaceHacks()
        {
            var assemblyNames = new[]
            {
                "Microsoft.CodeAnalysis.Workspaces",
                "Microsoft.CodeAnalysis.Workspaces.MSBuild",
                "Microsoft.CodeAnalysis.CSharp.Workspaces",
                "Microsoft.CodeAnalysis.VisualBasic.Workspaces",
                "Microsoft.CodeAnalysis.Features",
                "Microsoft.CodeAnalysis.CSharp.Features",
                "Microsoft.CodeAnalysis.VisualBasic.Features"
            };
            var assemblies = assemblyNames
                .Select(n => Assembly.Load(n));
            Pack = MefHostServices.Create(assemblies);
        }

        public static dynamic GetSemanticFactsService(Document document)
        {
            return GetService(document, "Microsoft.CodeAnalysis.LanguageServices.ISemanticFactsService", "Microsoft.CodeAnalysis.Workspaces");
        }

        public static dynamic GetSyntaxFactsService(Document document)
        {
            return GetService(document, "Microsoft.CodeAnalysis.LanguageServices.ISyntaxFactsService", "Microsoft.CodeAnalysis.Workspaces");
        }

        public static object GetMetadataAsSourceService(Document document)
        {
            var language = document.Project.Language;
            var workspace = document.Project.Solution.Workspace;
            var serviceAssembly = Assembly.Load("Microsoft.CodeAnalysis.Features");
            var serviceInterfaceType = serviceAssembly.GetType("Microsoft.CodeAnalysis.MetadataAsSource.IMetadataAsSourceService");
            var result = GetService(workspace, language, serviceInterfaceType);
            return result;
        }

        private static object GetService(Workspace workspace, string language, Type serviceType)
        {
            var languageServices = workspace.Services.GetLanguageServices(language);
            var languageServicesType = typeof(HostLanguageServices);
            var genericMethod = languageServicesType.GetMethod("GetService", BindingFlags.Public | BindingFlags.Instance);
            var closedGenericMethod = genericMethod.MakeGenericMethod(serviceType);
            var result = closedGenericMethod.Invoke(languageServices, new object[0]);
            if (result == null)
            {
                throw new NullReferenceException("Unable to get language service: " + serviceType.FullName + " for " + language);
            }

            return result;
        }

        private static object GetService(Document document, string serviceType, string assemblyName)
        {
            var assembly = typeof(Document).Assembly;
            var documentExtensions = assembly.GetType("Microsoft.CodeAnalysis.Shared.Extensions.DocumentExtensions");
            var serviceAssembly = Assembly.Load(assemblyName);
            var serviceInterfaceType = serviceAssembly.GetType(serviceType);
            var getLanguageServiceMethod = documentExtensions.GetMethod("GetLanguageService");
            getLanguageServiceMethod = getLanguageServiceMethod.MakeGenericMethod(serviceInterfaceType);
            var service = getLanguageServiceMethod.Invoke(null, new object[] { document });
            return service;
        }
    }
}
