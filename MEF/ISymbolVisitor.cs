using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace MEF
{
    public interface ISymbolVisitor
    {
        string Visit(ISymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitAlias(IAliasSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitArrayType(IArrayTypeSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitAssembly(IAssemblySymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitDynamicType(IDynamicTypeSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitEvent(IEventSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitField(IFieldSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitLabel(ILabelSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitLocal(ILocalSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitMethod(IMethodSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitModule(IModuleSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitNamedType(INamedTypeSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitNamespace(INamespaceSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitParameter(IParameterSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitPointerType(IPointerTypeSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitProperty(IPropertySymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitRangeVariable(IRangeVariableSymbol symbol, IReadOnlyDictionary<string, string> context);
        string VisitTypeParameter(ITypeParameterSymbol symbol, IReadOnlyDictionary<string, string> context);
    }
}
