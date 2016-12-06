using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class TypeForwardReader : MarshalByRefObject
    {
        public IEnumerable<Tuple<string, string, string>> GetTypeForwards(string path)
        {
            try
            {
                return GetTypeForwardsImpl(path).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Array.Empty<Tuple<string, string, string>>();
            }
        }

        private IEnumerable<Tuple<string, string, string>> GetTypeForwardsImpl(string path)
        {
            var workspace = MSBuildWorkspace.Create();
            Solution solution;
            if (path.EndsWith(".sln"))
            {
                solution = workspace.OpenSolutionAsync(path).GetAwaiter().GetResult();
            }
            else
            {
                solution = workspace.OpenProjectAsync(path).GetAwaiter().GetResult().Solution;
            }

            var assemblies = solution.Projects.Select(p => p.OutputFilePath).Where(File.Exists).ToList();
            foreach (var assemblyFile in assemblies)
            {
                var thisAssemblyName = Path.GetFileNameWithoutExtension(assemblyFile);
                using (var peReader = new PEReader(File.ReadAllBytes(assemblyFile).ToImmutableArray()))
                {
                    var reader = peReader.GetMetadataReader();
                    foreach (var exportedTypeHandle in reader.ExportedTypes)
                    {
                        var exportedType = reader.GetExportedType(exportedTypeHandle);
                        var result = ProcessExportedType(exportedType, reader, thisAssemblyName);
                        if (result != null)
                        {
                            yield return result;
                        }
                    }
                }
            }
        }

        private static string GetFullName(MetadataReader reader, ExportedType type)
        {
            Debug.Assert(type.IsForwarder);
            if (type.Implementation.Kind == HandleKind.AssemblyReference)
            {
                var name = reader.GetString(type.Name);
                var ns = type.Namespace.IsNil ? null : reader.GetString(type.Namespace);
                var fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                return fullName;
            }
            if (type.Implementation.Kind == HandleKind.ExportedType)
            {
                var name = reader.GetString(type.Name);
                Debug.Assert(type.Namespace.IsNil);
                return GetFullName(reader, reader.GetExportedType((ExportedTypeHandle)type.Implementation)) + "." + name;
            }
            throw new NotSupportedException(type.Implementation.Kind.ToString());
        }

        private static string GetAssemblyName(MetadataReader reader, ExportedType type)
        {
            Debug.Assert(type.IsForwarder);
            if (type.Implementation.Kind == HandleKind.AssemblyReference)
            {
                return reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.Implementation).Name);
            }
            if (type.Implementation.Kind == HandleKind.ExportedType)
            {
                return GetAssemblyName(reader, reader.GetExportedType((ExportedTypeHandle)type.Implementation));
            }
            throw new NotSupportedException(type.Implementation.Kind.ToString());
        }

        private static Tuple<string, string, string> ProcessExportedType(ExportedType exportedType, MetadataReader reader, string thisAssemblyName)
        {
            if (!exportedType.IsForwarder) return null;
            return Tuple.Create(thisAssemblyName, "T:" + GetFullName(reader, exportedType), GetAssemblyName(reader, exportedType));
        }
    }
}
