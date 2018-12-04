using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class MetadataReading
    {
        public static string GetAssemblyAttributesFileText(string assemblyFilePath, string language = LanguageNames.CSharp)
        {
            var assemblyAttributes = MetadataReading.GetAssemblyAttributes(assemblyFilePath, language);
            if (!assemblyAttributes.Any())
            {
                return null;
            }

            return GetAssemblyAttributesFileText(language, assemblyFilePath.Substring(0, 3), assemblyAttributes);
        }

        public static string GetAssemblyAttributesFileText(string language, string driveRoot, IEnumerable<string> assemblyAttributes)
        {
            string template = language == LanguageNames.CSharp ? "[assembly: {0}]" : "<Assembly: {0}>";
            var sb = new StringBuilder();
            foreach (var attribute in assemblyAttributes)
            {
                var line = string.Format(template, attribute);
                var fdd = @"f:\\dd\\";
                int index = line.IndexOf(fdd, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    if (driveRoot[0] == 'Q')
                    {
                        driveRoot = @"P:\\";
                    }
                    else if (driveRoot[0] == 'O')
                    {
                        driveRoot = @"N:\\";
                    }

                    fdd = line.Substring(index, fdd.Length);
                    line = line.Replace(fdd, driveRoot);
                }

                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public static IEnumerable<string> GetAssemblyAttributes(string assemblyPath, string language = LanguageNames.CSharp)
        {
            var assemblySymbol = GetAssemblySymbol(assemblyPath, language);
            if (assemblySymbol == null)
            {
                return Enumerable.Empty<string>();
            }

            return GetAssemblyAttributes(assemblySymbol);
        }

        public static IEnumerable<string> GetAssemblyAttributes(IAssemblySymbol assemblySymbol)
        {
            var attributes = assemblySymbol.GetAttributes();
            foreach (var attribute in attributes)
            {
                yield return attribute.ToString();
            }
        }

        public static IEnumerable<string> GetReferencePaths(MetadataReference metadataReference)
        {
            var symbol = GetRawAssemblySymbol(metadataReference);
            if (symbol == null)
            {
                return Enumerable.Empty<string>();
            }

            var references = GetReferencePaths(symbol);
            return references.ToArray();
        }

        private static IEnumerable<string> resolutionPaths = new[]
        {
            "",
            "bin\\i386",
            "cdf",
            "wpf"
        };

        public static IEnumerable<string> GetReferencePaths(IAssemblySymbol assemblySymbol)
        {
            foreach (var referenceIdentity in GetReferences(assemblySymbol))
            {
                var resolved = Resolve(referenceIdentity.Name);
                if (!string.IsNullOrEmpty(resolved))
                {
                    yield return resolved;
                }
                else
                {
                    Log.Message(SymbolIdService.GetAssemblyId(assemblySymbol) + " references an assembly that cannot be resolved: " + referenceIdentity.Name);
                }
            }
        }

        private static string Resolve(string assembly)
        {
            if (GenerateFromBuildLog.AssemblyNameToFilePathMap.TryGetValue(assembly, out string assemblyFilePath))
            {
                return assemblyFilePath;
            }

            return null;
        }

        public static IAssemblySymbol GetAssemblySymbol(string assemblyPath, string language = LanguageNames.CSharp)
        {
            var metadataReference = MetadataAsSource.CreateReferenceFromFilePath(assemblyPath);
            return GetAssemblySymbol(metadataReference, language);
        }

        public static IAssemblySymbol GetAssemblySymbol(MetadataReference metadataReference, string language = LanguageNames.CSharp)
        {
            var compilation = CreateCompilation(metadataReference, language);
            var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
            if (assemblySymbol == null)
            {
                return null;
            }

            var referencePaths = GetReferencePaths(assemblySymbol);
            foreach (var referencePath in referencePaths)
            {
                var reference = MetadataAsSource.CreateReferenceFromFilePath(referencePath);
                compilation = compilation.AddReferences(reference);
            }

            assemblySymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
            return assemblySymbol;
        }

        public static IAssemblySymbol GetRawAssemblySymbol(MetadataReference metadataReference, string language = LanguageNames.CSharp)
        {
            var compilation = CreateCompilation(metadataReference, language);
            var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
            return assemblySymbol;
        }

        public static Compilation CreateCompilation(MetadataReference metadataReference, string language = LanguageNames.CSharp)
        {
            var references = new[]
            {
                metadataReference
            };
            if (language == LanguageNames.CSharp)
            {
                return Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                    "Temp", references: references);
            }
            else
            {
                return Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create(
                    "Temp", references: references);
            }
        }

        public static ImmutableArray<AssemblyIdentity> GetReferences(IAssemblySymbol assemblySymbol)
        {
            return assemblySymbol.Modules
                .SelectMany(m => m.ReferencedAssemblies)
                .Distinct()
                .ToImmutableArray();
        }
    }
}
