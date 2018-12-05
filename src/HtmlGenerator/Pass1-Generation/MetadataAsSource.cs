using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class MetadataAsSource
    {
        private static Func<Document, ISymbol, CancellationToken, Task<Document>> addSourceToAsync = null;

        private static Func<Document, ISymbol, CancellationToken, Task<Document>> ReflectAddSourceToAsync(object service)
        {
            var assembly = Assembly.Load("Microsoft.CodeAnalysis.Features");
            var type = assembly.GetType("Microsoft.CodeAnalysis.MetadataAsSource.IMetadataAsSourceService");
            var method = type.GetMethod("AddSourceToAsync");
            return (Func<Document, ISymbol, CancellationToken, Task<Document>>)
                Delegate.CreateDelegate(typeof(Func<Document, ISymbol, CancellationToken, Task<Document>>), service, method);
        }

        public static MetadataReference CreateReferenceFromFilePath(string assemblyFilePath)
        {
            var documentationProvider = GetDocumentationProvider(
                assemblyFilePath,
                Path.GetFileNameWithoutExtension(assemblyFilePath));

            return MetadataReference.CreateFromFile(assemblyFilePath, documentation: documentationProvider);
        }

        public static Solution LoadMetadataAsSourceSolution(string assemblyFilePath)
        {
            try
            {
                using (Disposable.Timing("Metadata as source: " + assemblyFilePath))
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);

                    var solution = new AdhocWorkspace(MefHostServices.DefaultHost).CurrentSolution;
                    var workspace = solution.Workspace;
                    var project = solution.AddProject(assemblyName, assemblyName, LanguageNames.CSharp);
                    var metadataReference = CreateReferenceFromFilePath(assemblyFilePath);

                    var referencePaths = MetadataReading.GetReferencePaths(metadataReference);
                    foreach (var referencePath in referencePaths)
                    {
                        project = project.AddMetadataReference(CreateReferenceFromFilePath(referencePath));
                    }

                    var projectWithReference = project.AddMetadataReference(metadataReference);
                    var compilation = projectWithReference.GetCompilationAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    var assemblyOrModuleSymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference);
                    IAssemblySymbol assemblySymbol = assemblyOrModuleSymbol as IAssemblySymbol;
                    IModuleSymbol moduleSymbol = assemblyOrModuleSymbol as IModuleSymbol;
                    if (moduleSymbol != null && assemblySymbol == null)
                    {
                        assemblySymbol = moduleSymbol.ContainingAssembly;
                    }

                    var assemblyAttributes = MetadataReading.GetAssemblyAttributes(assemblySymbol);
                    var assemblyAttributesFileText = MetadataReading.GetAssemblyAttributesFileText(
                        LanguageNames.CSharp,
                        assemblyFilePath.Substring(0, 3),
                        assemblyAttributes);

                    INamespaceSymbol namespaceSymbol = null;
                    if (assemblySymbol != null)
                    {
                        namespaceSymbol = assemblySymbol.GlobalNamespace;
                    }
                    else if (moduleSymbol != null)
                    {
                        namespaceSymbol = moduleSymbol.GlobalNamespace;
                    }

                    var types = GetTypes(namespaceSymbol)
                        .OfType<INamedTypeSymbol>()
                        .Where(t => t.CanBeReferencedByName);

                    var tempDocument = projectWithReference.AddDocument("temp", SourceText.From(""), null);
                    var metadataAsSourceService = WorkspaceHacks.GetMetadataAsSourceService(tempDocument);
                    if (addSourceToAsync == null)
                    {
                        addSourceToAsync = ReflectAddSourceToAsync(metadataAsSourceService);
                    }

                    var texts = new Dictionary<INamedTypeSymbol, string>();

                    Parallel.ForEach(
                        types,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount
                        },
                        type =>
                        {
                            try
                            {
                                string text = "";

                                if (Configuration.GenerateMetadataAsSourceBodies)
                                {
                                    var document = addSourceToAsync(
                                                            tempDocument,
                                                            type,
                                                            CancellationToken.None).Result;
                                    text = document.GetTextAsync().Result.ToString();
                                }

                                lock (texts)
                                {
                                    texts.Add(type, text);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Exception(ex, "Error when adding a MAS document to texts: " + assemblyFilePath);
                            }
                        });

                    HashSet<string> existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var kvp in texts)
                    {
                        var tempProject = AddDocument(project, kvp, existingFileNames);

                        // tempProject can be null if the document was in an unutterable namespace
                        // we want to skip such documents
                        if (tempProject != null)
                        {
                            project = tempProject;
                        }
                    }

                    const string assemblyAttributesFileName = "AssemblyAttributes.cs";
                    project = project.AddDocument(
                        assemblyAttributesFileName,
                        assemblyAttributesFileText,
                        filePath: assemblyAttributesFileName).Project;

                    solution = project.Solution;
                    return solution;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Failed to run metadata as source for: " + assemblyFilePath);
                return null;
            }
        }

        private static Dictionary<string, string> assemblyNameToXmlDocFileMap = null;

        /// <summary>
        /// This has to be unique, there shouldn't be a project with this name ever
        /// </summary>
        public const string GeneratedAssemblyAttributesFileName = "GeneratedAssemblyAttributes0e71257b769ef";

        private static Dictionary<string, string> AssemblyNameToXmlDocFileMap
            => assemblyNameToXmlDocFileMap ?? (assemblyNameToXmlDocFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private static DocumentationProvider GetDocumentationProvider(string assemblyFilePath, string assemblyName)
        {
            var result = DocumentationProvider.Default;
            if (AssemblyNameToXmlDocFileMap.TryGetValue(assemblyName, out string xmlFile))
            {
                result = new XmlDocumentationProvider(xmlFile);
            }

            return result;
        }

        private static Project AddDocument(
            Project project,
            KeyValuePair<INamedTypeSymbol, string> symbolAndText,
            HashSet<string> existingFileNames)
        {
            var symbol = symbolAndText.Key;
            var text = symbolAndText.Value;
            var sanitizedTypeName = Paths.SanitizeFileName(symbol.Name);
            if (symbol.IsGenericType)
            {
                sanitizedTypeName = sanitizedTypeName + "`" + symbol.TypeParameters.Length;
            }

            var fileName = sanitizedTypeName + ".cs";
            var folders = GetFolderChain(symbol);
            if (folders == null)
            {
                // There was an unutterable namespace name - abort the entire document
                return null;
            }

            var foldersString = string.Join(".", folders ?? Enumerable.Empty<string>());
            var fileNameAndFolders = foldersString + fileName;
            int index = 1;
            while (!existingFileNames.Add(fileNameAndFolders))
            {
                fileName = sanitizedTypeName + index + ".cs";
                fileNameAndFolders = foldersString + fileName;
                index++;
            }

            project = project.AddDocument(fileName, text, folders, fileName).Project;
            return project;
        }

        private static string[] GetFolderChain(INamedTypeSymbol symbol)
        {
            var containingNamespace = symbol.ContainingNamespace;
            var folders = new List<string>();
            while (containingNamespace != null && !containingNamespace.IsGlobalNamespace)
            {
                if (!containingNamespace.CanBeReferencedByName)
                {
                    // namespace name is mangled - we don't want it
                    return null;
                }

                var sanitizedNamespaceName = Paths.SanitizeFolder(containingNamespace.Name);
                folders.Add(sanitizedNamespaceName);
                containingNamespace = containingNamespace.ContainingNamespace;
            }

            folders.Reverse();
            return folders.ToArray();
        }

        private static IEnumerable<ISymbol> GetTypes(INamespaceSymbol namespaceSymbol)
        {
            var results = new List<ISymbol>();
            EnumSymbols(namespaceSymbol, results.Add);
            return results;
        }

        private static void EnumSymbols(INamespaceSymbol namespaceSymbol, Action<ISymbol> action)
        {
            foreach (var subNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                EnumSymbols(subNamespace, action);
            }

            foreach (var topLevelType in namespaceSymbol.GetTypeMembers())
            {
                action(topLevelType);
            }
        }
    }
}
