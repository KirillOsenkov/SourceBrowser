using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        private HtmlElementInfo GenerateLinks(Classification.Range range, bool isLargeFile = false)
        {
            var text = range.Text;

            if (range.ClassificationType == Constants.ClassificationLiteral)
            {
                return TryProcessGuid(range);
            }

            if (range.ClassificationType != Constants.ClassificationIdentifier &&
                range.ClassificationType != Constants.ClassificationTypeName &&
                text != "this" &&
                text != "base" &&
                text != "var" &&
                text != "New" &&
                text != "new" &&
                text != "[" &&
                text != "partial" &&
                text != "Partial")
            {
                return null;
            }

            var position = range.ClassifiedSpan.TextSpan.Start;
            var token = Root.FindToken(position, findInsideTrivia: true);
            if (IsZeroLengthArrayAllocation(token))
            {
                projectGenerator.AddReference(
                    this.documentDestinationFilePath,
                    Text,
                    "mscorlib",
                    null,
                    "EmptyArrayAllocation",
                    range.ClassifiedSpan.TextSpan.Start,
                    range.ClassifiedSpan.TextSpan.End,
                    ReferenceKind.EmptyArrayAllocation);
                return null;
            }

            // now that we've passed the empty array allocation check, disable all further new keywords
            if (range.ClassificationType == Constants.ClassificationKeyword && text == "new")
            {
                return null;
            }

            var declaredSymbol = SemanticModel.GetDeclaredSymbol(token.Parent);
            if (declaredSymbol is IParameterSymbol && text == "this")
            {
                // it's a 'this' in the first parameter of an extension method - we don't want it to
                // hyperlink to anything
                return null;
            }

            if (declaredSymbol != null)
            {
                if (token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword) ||
                    token.IsKind(Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.PartialKeyword))
                {
                    if (declaredSymbol is INamedTypeSymbol)
                    {
                        return TryProcessPartialKeyword((INamedTypeSymbol)declaredSymbol);
                    }

                    return null;
                }

                var explicitlyImplementedMember = GetExplicitlyImplementedMember(declaredSymbol);
                if (explicitlyImplementedMember == null)
                {
                    if (token.Span.Contains(position) &&
                        (declaredSymbol.Kind == SymbolKind.Event ||
                         declaredSymbol.Kind == SymbolKind.Field ||
                         declaredSymbol.Kind == SymbolKind.Local ||
                         declaredSymbol.Kind == SymbolKind.Method ||
                         declaredSymbol.Kind == SymbolKind.NamedType ||
                         declaredSymbol.Kind == SymbolKind.Parameter ||
                         declaredSymbol.Kind == SymbolKind.Property ||
                         declaredSymbol.Kind == SymbolKind.TypeParameter
                         ) &&
                        DeclaredSymbols.Add(declaredSymbol))
                    {
                        if ((declaredSymbol.Kind == SymbolKind.Method ||
                            declaredSymbol.Kind == SymbolKind.Property ||
                            declaredSymbol.Kind == SymbolKind.Event) &&
                            !declaredSymbol.IsStatic)
                        {
                            // declarations of overridden members are also "references" to their
                            // base members. This is needed for "Find Overridding Members" and
                            // "Find Implementations"
                            AddReferencesToOverriddenMembers(range, token, declaredSymbol);
                            AddReferencesToImplementedMembers(range, token, declaredSymbol);
                        }

                        return ProcessDeclaredSymbol(declaredSymbol, isLargeFile);
                    }
                }
                else
                {
                    projectGenerator.AddImplementedInterfaceMember(
                        declaredSymbol,
                        explicitlyImplementedMember);
                    return ProcessReference(
                        range,
                        explicitlyImplementedMember,
                        ReferenceKind.InterfaceMemberImplementation);
                }
            }
            else
            {
                return ProcessReference(range, token, isLargeFile);
            }

            return null;
        }

        private bool IsZeroLengthArrayAllocation(SyntaxToken token)
        {
            try
            {
                if (token.Language == LanguageNames.CSharp && token.IsKind(CS.SyntaxKind.NewKeyword))
                {
                    return IsZeroLengthArrayAllocationCSharp(token);
                }
                else if (token.Language == LanguageNames.VisualBasic && token.IsKind(VB.SyntaxKind.NewKeyword))
                {
                    return IsZeroLengthArrayAllocationVB(token);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private bool IsZeroLengthArrayAllocationCSharp(SyntaxToken token)
        {
            var arrayCreationExpression = token.Parent as CS.Syntax.ArrayCreationExpressionSyntax;
            if (arrayCreationExpression == null)
            {
                return false;
            }

            var arrayType = arrayCreationExpression.Type;
            if (arrayType == null || arrayType.IsMissing)
            {
                return false;
            }

            if (arrayType.RankSpecifiers.Count != 1)
            {
                return false;
            }

            var specifier = arrayType.RankSpecifiers[0] as CS.Syntax.ArrayRankSpecifierSyntax;
            if (specifier == null)
            {
                return false;
            }

            if (specifier.Rank != 1 || specifier.Sizes.Count != 1)
            {
                return false;
            }

            var sizeExpression = specifier.Sizes[0];
            if (sizeExpression == null)
            {
                return false;
            }

            if (sizeExpression is CS.Syntax.OmittedArraySizeExpressionSyntax &&
                arrayCreationExpression.Initializer != null &&
                !arrayCreationExpression.Initializer.IsMissing &&
                arrayCreationExpression.Initializer.Expressions.Count == 0)
            {
                return true;
            }

            var value = SemanticModel.GetConstantValue(sizeExpression);
            if (!value.HasValue || !(value.Value is int))
            {
                return false;
            }

            return (int)value.Value == 0;
        }

        private bool IsZeroLengthArrayAllocationVB(SyntaxToken token)
        {
            var arrayCreationExpression = token.Parent as VB.Syntax.ArrayCreationExpressionSyntax;
            if (arrayCreationExpression == null)
            {
                return false;
            }

            var arrayType = arrayCreationExpression.Type;
            if (arrayType == null || arrayType.IsMissing)
            {
                return false;
            }

            var initializer = arrayCreationExpression.Initializer;
            if (initializer == null || initializer.IsMissing || initializer.Initializers.Count > 0)
            {
                return false;
            }

            var arrayBounds = arrayCreationExpression.ArrayBounds;
            if (arrayBounds != null && arrayBounds.Arguments.Count == 1)
            {
                var argument = arrayBounds.Arguments[0] as VB.Syntax.SimpleArgumentSyntax;
                if (argument != null && !argument.IsMissing)
                {
                    var expression = argument.Expression;
                    if (expression != null && !expression.IsMissing)
                    {
                        var optional = SemanticModel.GetConstantValue(expression);
                        if (optional.HasValue && optional.Value is int && (int)optional.Value == -1)
                        {
                            return true;
                        }
                    }
                }
            }

            if (arrayCreationExpression.RankSpecifiers.Count != 1)
            {
                return false;
            }

            var specifier = arrayCreationExpression.RankSpecifiers[0] as VB.Syntax.ArrayRankSpecifierSyntax;
            if (specifier == null)
            {
                return false;
            }

            if (specifier.Rank != 1)
            {
                return false;
            }

            return true;
        }

        private HtmlElementInfo TryProcessPartialKeyword(INamedTypeSymbol symbol)
        {
            if (symbol.Locations.Length > 1)
            {
                string symbolId = SymbolIdService.GetId(symbol);

                string partialFilePath = Path.Combine(ProjectDestinationFolder, Constants.PartialResolvingFileName, symbolId + ".html");
                string href = Paths.MakeRelativeToFile(partialFilePath, documentDestinationFilePath);
                href = href.Replace('\\', '/');

                var result = new HtmlElementInfo()
                {
                    Name = "a",
                    Attributes =
                    {
                        { "href", href },
                        { "target", "s" },
                    }
                };
                return result;
            }

            return null;
        }

        private HtmlElementInfo TryProcessGuid(Classification.Range range)
        {
            var text = range.Text;
            var spanStart = range.ClassifiedSpan.TextSpan.Start;
            var spanEnd = range.ClassifiedSpan.TextSpan.End;

            if (text.StartsWith("@"))
            {
                text = text.Substring(1);
                spanStart++;
            }

            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
            {
                spanStart++;
                spanEnd--;
                text = text.Substring(1, text.Length - 2);
            }

            // quick check to reject non-Guids even before trying to parse
            if (text.Length != 32 && text.Length != 36 && text.Length != 38)
            {
                return null;
            }

            Guid guid;
            if (!Guid.TryParse(text, out guid))
            {
                return null;
            }

            var symbolId = guid.ToString();

            var referencesFilePath = Path.Combine(
                SolutionDestinationFolder,
                Constants.GuidAssembly,
                Constants.ReferencesFileName,
                symbolId + ".html");
            string href = Paths.MakeRelativeToFile(referencesFilePath, documentDestinationFilePath);
            href = href.Replace('\\', '/');

            var link = new HtmlElementInfo
            {
                Name = "a",
                Attributes =
                {
                    { "href", href },
                    { "target", "n" },
                },
                DeclaredSymbolId = symbolId
            };

            projectGenerator.AddReference(
                this.documentDestinationFilePath,
                Text,
                Constants.GuidAssembly,
                null,
                symbolId,
                spanStart,
                spanEnd,
                ReferenceKind.GuidUsage);

            return link;
        }

        private void AddReferencesToImplementedMembers(
            Classification.Range range,
            SyntaxToken token,
            ISymbol declaredSymbol)
        {
            var declaringType = declaredSymbol.ContainingType;
            var interfaces = declaringType.AllInterfaces;
            foreach (var implementedInterface in interfaces)
            {
                foreach (var member in implementedInterface.GetMembers())
                {
                    if (declaringType.FindImplementationForInterfaceMember(member) == declaredSymbol)
                    {
                        ProcessReference(
                            range,
                            member,
                            ReferenceKind.InterfaceMemberImplementation);
                        projectGenerator.AddImplementedInterfaceMember(declaredSymbol, member);
                    }
                }
            }
        }

        private void AddReferencesToOverriddenMembers(
            Classification.Range range,
            SyntaxToken token,
            ISymbol declaredSymbol)
        {
            if (!declaredSymbol.IsOverride)
            {
                return;
            }

            IMethodSymbol method = declaredSymbol as IMethodSymbol;
            if (method != null)
            {
                var overriddenMethod = method.OverriddenMethod;
                if (overriddenMethod != null)
                {
                    ProcessReference(
                        range,
                        overriddenMethod,
                        ReferenceKind.Override);
                    projectGenerator.AddBaseMember(method, overriddenMethod);
                }
            }

            IPropertySymbol property = declaredSymbol as IPropertySymbol;
            if (property != null)
            {
                var overriddenProperty = property.OverriddenProperty;
                if (overriddenProperty != null)
                {
                    ProcessReference(
                        range,
                        overriddenProperty,
                        ReferenceKind.Override);
                    projectGenerator.AddBaseMember(property, overriddenProperty);
                }
            }

            IEventSymbol eventSymbol = declaredSymbol as IEventSymbol;
            if (eventSymbol != null)
            {
                var overriddenEvent = eventSymbol.OverriddenEvent;
                if (overriddenEvent != null)
                {
                    ProcessReference(
                        range,
                        overriddenEvent,
                        ReferenceKind.Override);
                    projectGenerator.AddBaseMember(eventSymbol, overriddenEvent);
                }
            }
        }

        private ISymbol GetExplicitlyImplementedMember(ISymbol symbol)
        {
            IMethodSymbol methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                return methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IPropertySymbol propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IEventSymbol eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            return null;
        }
    }
}
