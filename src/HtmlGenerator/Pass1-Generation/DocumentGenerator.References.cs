using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.SourceBrowser.Common;
using System.Diagnostics;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        private readonly HashSet<string> reportedSymbolDisplayStrings = new HashSet<string>();
        private bool reportedDiagnostics = false;

        private string ClassFromSymbol(ISymbol symbol, string currentClass = null)
        {
            string result = null;
            switch (symbol.Kind)
            {
                case SymbolKind.Namespace:
                    result = Constants.ClassificationNamespace;
                    break;
                case SymbolKind.Field:
                    result = Constants.ClassificationField;
                    break;
                case SymbolKind.Property:
                    result = Constants.ClassificationProperty;
                    break;
                case SymbolKind.Method:
                    IMethodSymbol ms = symbol as IMethodSymbol;
                    if (ms?.MethodKind == MethodKind.Constructor)
                    {
                        result = Constants.ClassificationConstructor;
                    }
                    else
                    {
                        result = Constants.ClassificationMethod;
                    }
                    break;
                case SymbolKind.Alias:
                case SymbolKind.NamedType:
                    result = Constants.ClassificationTypeName;
                    break;
                default:
                    return currentClass;
            }

            if (string.IsNullOrEmpty(currentClass))
            {
                return result;
            }

            return currentClass + " " + result;
        }

        private string GetClassAttribute(string rangeText, Classification.Range range)
        {
            string classificationType = range.ClassificationType;

            if (classificationType == null ||
                classificationType == Constants.ClassificationPunctuation)
            {
                return null;
            }

            if (range.ClassificationType == Constants.ClassificationLiteral ||
                range.ClassificationType == Constants.ClassificationUnknown)
            {
                return classificationType;
            }

            if (range.ClassificationType != Constants.ClassificationIdentifier &&
                range.ClassificationType != Constants.ClassificationTypeName &&
                rangeText != "this" &&
                rangeText != "base" &&
                rangeText != "var" &&
                rangeText != "New" &&
                rangeText != "new" &&
                rangeText != "[" &&
                rangeText != "partial" &&
                rangeText != "Partial")
            {
                return classificationType;
            }

            if (range.ClassificationType == Constants.ClassificationKeyword)
            {
                return classificationType;
            }

            var position = range.ClassifiedSpan.TextSpan.Start;
            var token = Root.FindToken(position, findInsideTrivia: true);

            var declaredSymbol = SemanticModel.GetDeclaredSymbol(token.Parent);
            if (declaredSymbol is IParameterSymbol && rangeText == "this")
            {
                return classificationType;
            }

            if (declaredSymbol != null)
            {
                return ClassFromSymbol(declaredSymbol, classificationType);
            }

            var node = GetBindableParent(token);
            if (token.ToString() == "[" &&
                token.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.BracketedArgumentListSyntax &&
                token.Parent.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax)
            {
                node = token.Parent.Parent;
            }

            if (node == null)
            {
                return classificationType;
            }

            var symbol = GetSymbol(node);
            if (symbol == null)
            {
                return classificationType;
            }

            return ClassFromSymbol(symbol, classificationType);
        }

        private HtmlElementInfo ProcessReference(Classification.Range range, SyntaxToken token, bool isLargeFile = false)
        {
            ClassifiedSpan classifiedSpan = range.ClassifiedSpan;
            var kind = ReferenceKind.Reference;
            var node = GetBindableParent(token);

            if (token.RawKind == (int)Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.NewKeyword &&
                node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ObjectCreationExpressionSyntax)
            {
                // don't count New in New Foo() as a reference to the constructor
                return null;
            }

            if (token.ToString() == "[" &&
                token.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.BracketedArgumentListSyntax &&
                token.Parent.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.ElementAccessExpressionSyntax)
            {
                node = token.Parent.Parent;
            }

            if (node == null)
            {
                return null;
            }

            var symbol = GetSymbol(node);
            if (symbol == null)
            {
                return null;
            }

            //Diagnostics(classifiedSpan, token, symbol);

            kind = DetermineReferenceKind(token, node, symbol);

            return ProcessReference(range, symbol, kind, isLargeFile);
        }

        private void Diagnostics(ClassifiedSpan classifiedSpan, SyntaxToken token, ISymbol symbol)
        {
            var tokenText = token.ToString();

            if (!(symbol is INamedTypeSymbol) ||
                classifiedSpan.ClassificationType == "t" ||
                tokenText == "this" ||
                tokenText == "base" ||
                tokenText == "var")
            {
                return;
            }

            if (tokenText == "SR" ||
                tokenText == "SR2" ||
                tokenText == "SRID" ||
                tokenText == "Strings" ||
                tokenText == "Res" ||
                tokenText == "VisualStudioVersionInfo" ||
                tokenText == "Error" ||
                tokenText == "Resource" ||
                tokenText == "Resources" ||
                tokenText == "AssemblyRef" ||
                tokenText == "ProjectResources")
            {
                return;
            }

            var symbolDisplayString = SymbolIdService.GetDisplayString(symbol);
            if (symbolDisplayString == "System.SR" ||
                symbolDisplayString == "System.Web.SR" ||
                symbolDisplayString == "ThisAssembly")
            {
                return;
            }

            if (!reportedSymbolDisplayStrings.Add(symbolDisplayString))
            {
                return;
            }

            if (reportedDiagnostics)
            {
                return;
            }

            reportedDiagnostics = true;

            string message =
                this.documentDestinationFilePath + "\r\n" +
                token.ToString() + ", " + token.Span.Start + "\r\n" +
                (classifiedSpan.ClassificationType ?? "null classification type") + "\r\n" +
                symbolDisplayString;

            var diagnostics = this.SemanticModel.GetDiagnostics().Where(d =>
            {
                var diagnostic = d.GetMessage();
                if (diagnostic.Contains("The type or namespace name 'Resources'") ||
                    diagnostic.Contains("must declare a body because it is not marked abstract"))
                {
                    return false;
                }

                return true;
            });

            if (diagnostics.Any())
            {
                var diagnosticsMessage = string.Join("\r\n", diagnostics.Select(d => d.ToString()));
                message = message + "\r\n" + diagnosticsMessage;
            }

            Log.Exception("Classification: " + message);
        }

        private ReferenceKind DetermineReferenceKind(SyntaxToken token, SyntaxNode node, ISymbol referencedSymbol)
        {
            var kind = ReferenceKind.Reference;

            var baseList =
                (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.BaseListSyntax>() ??
                (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.InheritsStatementSyntax>() ??
                node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.ImplementsStatementSyntax>();
            if (baseList != null)
            {
                var typeDeclaration = baseList.Parent;
                if (typeDeclaration != null &&
                    SemanticModel.GetDeclaredSymbol(typeDeclaration) is INamedTypeSymbol derivedType &&
                    referencedSymbol is INamedTypeSymbol baseSymbol)
                {
                    if (baseSymbol.TypeKind == TypeKind.Class && baseSymbol.Equals(derivedType.BaseType))
                    {
                        kind = ReferenceKind.DerivedType;
                    }
                    else if (baseSymbol.TypeKind == TypeKind.Interface)
                    {
                        if (derivedType.TypeKind == TypeKind.Interface && derivedType.Interfaces.Contains(baseSymbol))
                        {
                            kind = ReferenceKind.InterfaceInheritance;
                        }
                        else if (derivedType.Interfaces.Contains(baseSymbol))
                        {
                            kind = ReferenceKind.InterfaceImplementation;
                        }
                    }
                }
            }

            if ((referencedSymbol.Kind == SymbolKind.Field ||
                referencedSymbol.Kind == SymbolKind.Property) &&
                IsWrittenTo(node))
            {
                kind = ReferenceKind.Write;
            }

            return kind;
        }

        private bool IsWrittenTo(SyntaxNode node)
        {
            bool result = this.isWrittenToDelegate(SemanticModel, node, CancellationToken.None);
            return result;
        }

        private SyntaxNode GetBindableParent(SyntaxToken token)
        {
            return this.getBindableParentDelegate(token);
        }

        private HtmlElementInfo ProcessReference(Classification.Range range, ISymbol symbol, ReferenceKind kind, bool isLargeFile = false)
        {
            ClassifiedSpan classifiedSpan = range.ClassifiedSpan;
            var methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null && methodSymbol.ReducedFrom != null)
            {
                symbol = methodSymbol.ReducedFrom;
            }

            HtmlElementInfo result = null;

            if (symbol.IsImplicitlyDeclared)
            {
                if (methodSymbol?.MethodKind == MethodKind.Constructor &&
                    symbol.ContainingSymbol != null)
                {
                    return ProcessReference(range, symbol.ContainingSymbol, ReferenceKind.Instantiation);
                }
            }

            if (symbol.Kind == SymbolKind.Local ||
                symbol.Kind == SymbolKind.Parameter ||
                symbol.Kind == SymbolKind.TypeParameter)
            {
                if (isLargeFile)
                {
                    return null;
                }

                return HighlightReference(symbol);
            }

            if (methodSymbol?.MethodKind == MethodKind.Constructor &&
                methodSymbol.ContainingType != null)
            {
                ProcessReference(range, methodSymbol.ContainingType, ReferenceKind.Instantiation);
            }

            if ((symbol.Kind == SymbolKind.Event ||
                 symbol.Kind == SymbolKind.Field ||
                 symbol.Kind == SymbolKind.Method ||
                 symbol.Kind == SymbolKind.NamedType ||
                 symbol.Kind == SymbolKind.Property) &&
                 symbol.Locations.Length >= 1)
            {
                var typeSymbol = symbol as ITypeSymbol;
                string symbolId = SymbolIdService.GetId(symbol);
                var location = symbol.Locations[0];
                string destinationAssemblyName = null;
                if (location.IsInSource)
                {
                    result = GenerateHyperlink(symbol, symbolId, location.SourceTree, out destinationAssemblyName);
                }
                else if (location.IsInMetadata && location.MetadataModule != null)
                {
                    var metadataModule = location.MetadataModule;
                    result = GenerateHyperlink(symbolId, symbol, metadataModule, isLargeFile, out destinationAssemblyName);
                }

                if (result == null)
                {
                    return result;
                }

                if (result.Attributes == null ||
                    !result.Attributes.TryGetValue("href", out string target) ||
                    !target.Contains("@"))
                {
                    // only register a reference to the symbol if it's not a symbol from an external assembly.
                    // if this links to a symbol in a different index, link target contain @.
                    projectGenerator.AddReference(
                        this.documentDestinationFilePath,
                        Text,
                        destinationAssemblyName,
                        symbol,
                        symbolId,
                        classifiedSpan.TextSpan.Start,
                        classifiedSpan.TextSpan.End,
                        kind);
                }
            }

            // don't make this and var into hyperlinks in large files to save space
            if (isLargeFile && (range.Text == "this" || range.Text == "var"))
            {
                result = null;
            }

            return result;
        }

        private ISymbol GetSymbol(SyntaxNode node)
        {
            var symbolInfo = SemanticModel.GetSymbolInfo(node);
            ISymbol symbol = symbolInfo.Symbol;
            if (symbol == null)
            {
                return null;
            }

            if (IsThisParameter(symbol))
            {
                var typeInfo = SemanticModel.GetTypeInfo(node);
                if (typeInfo.Type != null)
                {
                    return typeInfo.Type;
                }
            }
            else if (IsFunctionValue(symbol))
            {
                if (symbol.ContainingSymbol is IMethodSymbol method)
                {
                    return method.AssociatedSymbol != null ? method.AssociatedSymbol : method;
                }
            }

            symbol = ResolveAccessorParameter(symbol);

            return symbol;
        }

        private ISymbol ResolveAccessorParameter(ISymbol symbol)
        {
            if (symbol == null || !symbol.IsImplicitlyDeclared)
            {
                return symbol;
            }

            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol == null)
            {
                return symbol;
            }

            var accessorMethod = parameterSymbol.ContainingSymbol as IMethodSymbol;
            if (accessorMethod == null)
            {
                return symbol;
            }

            var property = accessorMethod.AssociatedSymbol as IPropertySymbol;
            if (property == null)
            {
                return symbol;
            }

            int ordinal = parameterSymbol.Ordinal;
            if (property.Parameters.Length <= ordinal)
            {
                return symbol;
            }

            return property.Parameters[ordinal];
        }

        private static bool IsFunctionValue(ISymbol symbol)
        {
            return symbol is ILocalSymbol && ((ILocalSymbol)symbol).IsFunctionValue;
        }

        private static bool IsThisParameter(ISymbol symbol)
        {
            return symbol?.Kind == SymbolKind.Parameter && ((IParameterSymbol)symbol).IsThis;
        }

        public HtmlElementInfo GenerateHyperlink(
            ISymbol symbol,
            string symbolId,
            SyntaxTree syntaxTree,
            out string assemblyName)
        {
            string href = null;
            assemblyName = SymbolIdService.GetAssemblyId(GetAssemblyFromSymbol(symbol));

            // if it's in a different assembly, use the URL to a redirecting file for that assembly
            if (assemblyName != Document.Project.AssemblyName)
            {
                href = GetAbsoluteLink(symbolId, assemblyName);
            }
            else // it's in the same assembly, we can just use the direct path without redirects
            {
                string referencedSymbolDestinationFilePath = null;
                if (symbol.Locations.Length > 1)
                {
                    referencedSymbolDestinationFilePath = Path.Combine(
                        SolutionDestinationFolder,
                        assemblyName,
                        Constants.PartialResolvingFileName,
                        symbolId);
                }
                else
                {
                    var referenceRelativeFilePath = Paths.GetRelativePathInProject(syntaxTree, Document.Project);
                    referencedSymbolDestinationFilePath = Path.Combine(projectGenerator.ProjectDestinationFolder, referenceRelativeFilePath);
                }

                href = Paths.MakeRelativeToFile(referencedSymbolDestinationFilePath, documentDestinationFilePath) + ".html";
                if (referencedSymbolDestinationFilePath + ".html" == documentDestinationFilePath)
                {
                    href = "";
                }
                else
                {
                    href = href.Replace('\\', '/');
                }

                href = href + "#" + symbolId;
            }

            return new HtmlElementInfo
            {
                Name = "a",
                Attributes =
                {
                    { "href", href },
                }
            };
        }

        public HtmlElementInfo GenerateHyperlink(
            string symbolId,
            ISymbol symbol,
            IModuleSymbol moduleSymbol,
            bool isLargeFile,
            out string destinationAssemblyName)
        {
            destinationAssemblyName = GetAssemblyFromSymbol(symbol);
            return GenerateHyperlink(symbolId, destinationAssemblyName, isLargeFile);
        }

        internal static readonly SymbolDisplayFormat QualifiedNameOnlyFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private string GetAssemblyFromSymbol(ISymbol symbol)
        {
            ITypeSymbol type = (ITypeSymbol)GetTypeFromSymbol(symbol);
            string metadataName = type.MetadataName;
            if (type.ContainingNamespace != null)
            {
                var namespaceName = type.ContainingNamespace.ToDisplayString(QualifiedNameOnlyFormat);
                if (!string.IsNullOrEmpty(namespaceName))
                {
                    metadataName = namespaceName + "." + metadataName;
                }
            }

            var forwardedTo = symbol.ContainingAssembly.ResolveForwardedType(metadataName);
            if (forwardedTo != null)
            {
                symbol = forwardedTo;
            }

            type = (ITypeSymbol)GetTypeFromSymbol(symbol);
            var forwardKey = ValueTuple.Create(type.ContainingAssembly.Name, (type?.OriginalDefinition ?? type).GetDocumentationCommentId());
            string forwardedToAssembly;
            if (projectGenerator.SolutionGenerator.TypeForwards.TryGetValue(forwardKey, out forwardedToAssembly))
            {
                lock (projectGenerator.ForwardedReferenceAssemblies)
                {
                    projectGenerator.ForwardedReferenceAssemblies.Add(
                        type.ContainingAssembly.Name + "->" + forwardedToAssembly);
                }
                return forwardedToAssembly;
            }

            var assembly = SymbolIdService.GetAssemblyId(symbol.ContainingAssembly);
            return assembly;
        }

        private static ISymbol GetTypeFromSymbol(ISymbol symbol)
        {
            while (symbol.ContainingType != null)
            {
                symbol = symbol.ContainingType;
            }

            return symbol;
        }

        public HtmlElementInfo GenerateHyperlink(string symbolId, string assemblyName, bool isLargeFile = false)
        {
            int externalAssemblyIndex = projectGenerator.SolutionGenerator.GetExternalAssemblyIndex(assemblyName);

            if (externalAssemblyIndex == -1)
            {
                if (!projectGenerator.SolutionGenerator.IsPartOfSolution(assemblyName))
                {
                    // this is not part of the index and also not part of federated indices
                    // the link would be a 404, so don't create a link at all.
                    // TODO: we might be interested in collecting references to unindexed assemblies - this was always supported
                    return null;
                }

                string href = GetAbsoluteLink(symbolId, assemblyName);

                return new HtmlElementInfo
                {
                    Name = "a",
                    Attributes =
                    {
                        { "href", href },
                    }
                };
            }
            else
            {
                // don't generate external links for large files
                if (isLargeFile)
                {
                    return null;
                }

                string href = "@" +
                    externalAssemblyIndex.ToString() +
                    "@" +
                    assemblyName +
                    "/" +
                    Constants.IDResolvingFileName +
                    ".html" +
                    "#" +
                    symbolId;

                return new HtmlElementInfo
                {
                    Name = "a",
                    Attributes =
                    {
                        { "href", href },
                    }
                };
            }
        }

        private static string GetAbsoluteLink(string symbolId, string assemblyName)
        {
            return "/" + assemblyName + "/" + Constants.IDResolvingFileName + ".html#" + symbolId;
        }

        private string ProjectDestinationFolder
        {
            get { return projectGenerator.ProjectDestinationFolder; }
        }

        private string SolutionDestinationFolder
        {
            get { return projectGenerator.SolutionGenerator.SolutionDestinationFolder; }
        }
    }
}
