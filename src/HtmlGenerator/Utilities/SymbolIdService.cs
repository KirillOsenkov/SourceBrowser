using System;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class SymbolIdService
    {
        public static string GetId(ISymbol symbol)
        {
            string result = GetSymbolString(symbol);
            return GetId(result);
        }

        public static ulong GetIdULong(ISymbol symbol)
        {
            string result = GetSymbolString(symbol);
            return GetIdULong(result);
        }

        private static string GetSymbolString(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Parameter ||
                symbol.Kind == SymbolKind.Local)
            {
                string parent = GetDocumentationCommentId(symbol.ContainingSymbol);
                return parent + ":" + symbol.MetadataName;
            }
            else
            {
                return GetDocumentationCommentId(symbol);
            }
        }

        public static readonly SymbolDisplayFormat CSharpFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                // Not showing the name is important because we visit parameters to display their
                // types.  If we visited their types directly, we wouldn't get ref/out/params.
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName);

        public static readonly SymbolDisplayFormat VisualBasicFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions:
                    SymbolDisplayGenericsOptions.IncludeTypeParameters |
                    SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                    SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                kindOptions:
                    SymbolDisplayKindOptions.None,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName);

        public static readonly SymbolDisplayFormat ShortNameFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions: SymbolDisplayMemberOptions.None,
                parameterOptions: SymbolDisplayParameterOptions.None,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.None);

        public static string GetDisplayString(ISymbol symbol)
        {
            var format = symbol.Language == LanguageNames.CSharp ? CSharpFormat : VisualBasicFormat;
            var result = symbol.ToDisplayString(format);
            return result;
        }

        public static string GetId(string result) => Paths.GetMD5Hash(result, 16);

        public static ulong GetIdULong(string content) => Paths.GetMD5HashULong(content, 16);

        public static string GetId(Document document)
        {
            var documentRelativePath = Paths.GetRelativeFilePathInProject(document);
            return GetId(documentRelativePath);
        }

        public static string GetAssemblyId(string assemblyName) => assemblyName;

        public static string GetAssemblyId(IAssemblySymbol assemblySymbol) => assemblySymbol.Name;

        public static string GetName(ISymbol symbol) => symbol.ToDisplayString(ShortNameFormat);

        private static readonly MethodInfo getGlyph =
            Assembly.Load("Microsoft.CodeAnalysis.Features")
                .GetType("Microsoft.CodeAnalysis.Shared.Extensions.ISymbolExtensions2")
                .GetMethod("GetGlyph");

        // "../../0000000000"
        public static readonly byte[] ZeroId = new byte[] { 46, 46, 47, 46, 46, 47, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48 };

        private static string GetDocumentationCommentId(ISymbol symbol)
        {
            string result = null;
            if (!symbol.IsDefinition)
            {
                symbol = symbol.OriginalDefinition;
            }

            result = symbol.GetDocumentationCommentId();

            result = result.Replace("#ctor", "ctor");

            return result;
        }

        public static ushort GetGlyphNumber(ISymbol declaredSymbol)
        {
            var glyph = (Glyph)getGlyph.Invoke(null, new object[] { declaredSymbol });
            ushort result = (ushort)((ushort)GetStandardGlyphGroup(glyph) + (ushort)GetStandardGlyphItem(glyph));
            return result;
        }

        private static StandardGlyphGroup GetStandardGlyphGroup(Glyph glyph)
        {
            switch (glyph)
            {
                case Glyph.Assembly:
                    return StandardGlyphGroup.GlyphAssembly;

                case Glyph.BasicFile:
                case Glyph.BasicProject:
                    return StandardGlyphGroup.GlyphVBProject;

                case Glyph.ClassPublic:
                case Glyph.ClassProtected:
                case Glyph.ClassPrivate:
                case Glyph.ClassInternal:
                    return StandardGlyphGroup.GlyphGroupClass;

                case Glyph.ConstantPublic:
                case Glyph.ConstantProtected:
                case Glyph.ConstantPrivate:
                case Glyph.ConstantInternal:
                    return StandardGlyphGroup.GlyphGroupConstant;

                case Glyph.CSharpFile:
                    return StandardGlyphGroup.GlyphCSharpFile;

                case Glyph.CSharpProject:
                    return StandardGlyphGroup.GlyphCoolProject;

                case Glyph.DelegatePublic:
                case Glyph.DelegateProtected:
                case Glyph.DelegatePrivate:
                case Glyph.DelegateInternal:
                    return StandardGlyphGroup.GlyphGroupDelegate;

                case Glyph.EnumPublic:
                case Glyph.EnumProtected:
                case Glyph.EnumPrivate:
                case Glyph.EnumInternal:
                    return StandardGlyphGroup.GlyphGroupEnum;

                case Glyph.EnumMemberPublic:
                case Glyph.EnumMemberProtected:
                case Glyph.EnumMemberPrivate:
                case Glyph.EnumMemberInternal:
                    return StandardGlyphGroup.GlyphGroupEnumMember;

                case Glyph.Error:
                    return StandardGlyphGroup.GlyphGroupError;

                case Glyph.ExtensionMethodPublic:
                    return StandardGlyphGroup.GlyphExtensionMethod;

                case Glyph.ExtensionMethodProtected:
                    return StandardGlyphGroup.GlyphExtensionMethodProtected;

                case Glyph.ExtensionMethodPrivate:
                    return StandardGlyphGroup.GlyphExtensionMethodPrivate;

                case Glyph.ExtensionMethodInternal:
                    return StandardGlyphGroup.GlyphExtensionMethodInternal;

                case Glyph.EventPublic:
                case Glyph.EventProtected:
                case Glyph.EventPrivate:
                case Glyph.EventInternal:
                    return StandardGlyphGroup.GlyphGroupEvent;

                case Glyph.FieldPublic:
                case Glyph.FieldProtected:
                case Glyph.FieldPrivate:
                case Glyph.FieldInternal:
                    return StandardGlyphGroup.GlyphGroupField;

                case Glyph.InterfacePublic:
                case Glyph.InterfaceProtected:
                case Glyph.InterfacePrivate:
                case Glyph.InterfaceInternal:
                    return StandardGlyphGroup.GlyphGroupInterface;

                case Glyph.Intrinsic:
                    return StandardGlyphGroup.GlyphGroupIntrinsic;

                case Glyph.Keyword:
                    return StandardGlyphGroup.GlyphKeyword;

                case Glyph.Label:
                    return StandardGlyphGroup.GlyphGroupIntrinsic;

                case Glyph.Local:
                    return StandardGlyphGroup.GlyphGroupVariable;

                case Glyph.Namespace:
                    return StandardGlyphGroup.GlyphGroupNamespace;

                case Glyph.MethodPublic:
                case Glyph.MethodProtected:
                case Glyph.MethodPrivate:
                case Glyph.MethodInternal:
                    return StandardGlyphGroup.GlyphGroupMethod;

                case Glyph.ModulePublic:
                case Glyph.ModuleProtected:
                case Glyph.ModulePrivate:
                case Glyph.ModuleInternal:
                    return StandardGlyphGroup.GlyphGroupModule;

                case Glyph.OpenFolder:
                    return StandardGlyphGroup.GlyphOpenFolder;

                case Glyph.Operator:
                    return StandardGlyphGroup.GlyphGroupOperator;

                case Glyph.Parameter:
                    return StandardGlyphGroup.GlyphGroupVariable;

                case Glyph.PropertyPublic:
                case Glyph.PropertyProtected:
                case Glyph.PropertyPrivate:
                case Glyph.PropertyInternal:
                    return StandardGlyphGroup.GlyphGroupProperty;

                case Glyph.RangeVariable:
                    return StandardGlyphGroup.GlyphGroupVariable;

                case Glyph.Reference:
                    return StandardGlyphGroup.GlyphReference;

                case Glyph.StructurePublic:
                case Glyph.StructureProtected:
                case Glyph.StructurePrivate:
                case Glyph.StructureInternal:
                    return StandardGlyphGroup.GlyphGroupStruct;

                case Glyph.TypeParameter:
                    return StandardGlyphGroup.GlyphGroupType;

                case Glyph.Snippet:
                    return StandardGlyphGroup.GlyphCSharpExpansion;

                case Glyph.CompletionWarning:
                    return StandardGlyphGroup.GlyphCompletionWarning;

                default:
                    throw new ArgumentException("glyph");
            }
        }

        private static StandardGlyphItem GetStandardGlyphItem(Glyph icon)
        {
            switch (icon)
            {
                case Glyph.ClassProtected:
                case Glyph.ConstantProtected:
                case Glyph.DelegateProtected:
                case Glyph.EnumProtected:
                case Glyph.EventProtected:
                case Glyph.FieldProtected:
                case Glyph.InterfaceProtected:
                case Glyph.MethodProtected:
                case Glyph.ModuleProtected:
                case Glyph.PropertyProtected:
                case Glyph.StructureProtected:
                    return StandardGlyphItem.GlyphItemProtected;

                case Glyph.ClassPrivate:
                case Glyph.ConstantPrivate:
                case Glyph.DelegatePrivate:
                case Glyph.EnumPrivate:
                case Glyph.EventPrivate:
                case Glyph.FieldPrivate:
                case Glyph.InterfacePrivate:
                case Glyph.MethodPrivate:
                case Glyph.ModulePrivate:
                case Glyph.PropertyPrivate:
                case Glyph.StructurePrivate:
                    return StandardGlyphItem.GlyphItemPrivate;

                case Glyph.ClassInternal:
                case Glyph.ConstantInternal:
                case Glyph.DelegateInternal:
                case Glyph.EnumInternal:
                case Glyph.EventInternal:
                case Glyph.FieldInternal:
                case Glyph.InterfaceInternal:
                case Glyph.MethodInternal:
                case Glyph.ModuleInternal:
                case Glyph.PropertyInternal:
                case Glyph.StructureInternal:
                    return StandardGlyphItem.GlyphItemFriend;

                default:
                    // We don't want any overlays
                    return StandardGlyphItem.GlyphItemPublic;
            }
        }

        private enum StandardGlyphGroup
        {
            GlyphGroupClass = 0,
            GlyphGroupConstant = 6,
            GlyphGroupDelegate = 12,
            GlyphGroupEnum = 18,
            GlyphGroupEnumMember = 24,
            GlyphGroupEvent = 30,
            GlyphGroupException = 36,
            GlyphGroupField = 42,
            GlyphGroupInterface = 48,
            GlyphGroupMacro = 54,
            GlyphGroupMap = 60,
            GlyphGroupMapItem = 66,
            GlyphGroupMethod = 72,
            GlyphGroupOverload = 78,
            GlyphGroupModule = 84,
            GlyphGroupNamespace = 90,
            GlyphGroupOperator = 96,
            GlyphGroupProperty = 102,
            GlyphGroupStruct = 108,
            GlyphGroupTemplate = 114,
            GlyphGroupTypedef = 120,
            GlyphGroupType = 126,
            GlyphGroupUnion = 132,
            GlyphGroupVariable = 138,
            GlyphGroupValueType = 144,
            GlyphGroupIntrinsic = 150,
            GlyphGroupJSharpMethod = 156,
            GlyphGroupJSharpField = 162,
            GlyphGroupJSharpClass = 168,
            GlyphGroupJSharpNamespace = 174,
            GlyphGroupJSharpInterface = 180,
            GlyphGroupError = 186,
            GlyphBscFile = 191,
            GlyphAssembly = 192,
            GlyphLibrary = 193,
            GlyphVBProject = 194,
            GlyphCoolProject = 196,
            GlyphCppProject = 199,
            GlyphDialogId = 200,
            GlyphOpenFolder = 201,
            GlyphClosedFolder = 202,
            GlyphArrow = 203,
            GlyphCSharpFile = 204,
            GlyphCSharpExpansion = 205,
            GlyphKeyword = 206,
            GlyphInformation = 207,
            GlyphReference = 208,
            GlyphRecursion = 209,
            GlyphXmlItem = 210,
            GlyphJSharpProject = 211,
            GlyphJSharpDocument = 212,
            GlyphForwardType = 213,
            GlyphCallersGraph = 214,
            GlyphCallGraph = 215,
            GlyphWarning = 216,
            GlyphMaybeReference = 217,
            GlyphMaybeCaller = 218,
            GlyphMaybeCall = 219,
            GlyphExtensionMethod = 220,
            GlyphExtensionMethodInternal = 221,
            GlyphExtensionMethodFriend = 222,
            GlyphExtensionMethodProtected = 223,
            GlyphExtensionMethodPrivate = 224,
            GlyphExtensionMethodShortcut = 225,
            GlyphXmlAttribute = 226,
            GlyphXmlChild = 227,
            GlyphXmlDescendant = 228,
            GlyphXmlNamespace = 229,
            GlyphXmlAttributeQuestion = 230,
            GlyphXmlAttributeCheck = 231,
            GlyphXmlChildQuestion = 232,
            GlyphXmlChildCheck = 233,
            GlyphXmlDescendantQuestion = 234,
            GlyphXmlDescendantCheck = 235,
            GlyphCompletionWarning = 236,
            GlyphGroupUnknown = 237
        }
    
        private enum StandardGlyphItem
        {
            GlyphItemPublic,
            GlyphItemInternal,
            GlyphItemFriend,
            GlyphItemProtected,
            GlyphItemPrivate,
            GlyphItemShortcut,
            TotalGlyphItems
        }
    }
}
