using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace MEF
{
    public class SymbolVisitorWrapper : SymbolVisitor<string>
    {
        private ISymbolVisitor Wrapped;
        private IReadOnlyDictionary<string, string> Context;

        public SymbolVisitorWrapper(ISymbolVisitor v, IReadOnlyDictionary<string, string> context)
        {
            Wrapped = v;
            Context = context;
        }

        public override string VisitAlias(IAliasSymbol symbol)
        {
            return Wrapped.VisitAlias(symbol, Context);
        }

        public override string VisitArrayType(IArrayTypeSymbol symbol)
        {
            return Wrapped.VisitArrayType(symbol, Context);
        }

        public override string VisitAssembly(IAssemblySymbol symbol)
        {
            return Wrapped.VisitAssembly(symbol, Context);
        }

        public override string VisitDynamicType(IDynamicTypeSymbol symbol)
        {
            return Wrapped.VisitDynamicType(symbol, Context);
        }

        public override string VisitEvent(IEventSymbol symbol)
        {
            return Wrapped.VisitEvent(symbol, Context);
        }

        public override string VisitField(IFieldSymbol symbol)
        {
            return Wrapped.VisitField(symbol, Context);
        }

        public override string VisitLabel(ILabelSymbol symbol)
        {
            return Wrapped.VisitLabel(symbol, Context);
        }

        public override string VisitLocal(ILocalSymbol symbol)
        {
            return Wrapped.VisitLocal(symbol, Context);
        }

        public override string VisitMethod(IMethodSymbol symbol)
        {
            return Wrapped.VisitMethod(symbol, Context);
        }

        public override string VisitModule(IModuleSymbol symbol)
        {
            return Wrapped.VisitModule(symbol, Context);
        }

        public override string VisitNamedType(INamedTypeSymbol symbol)
        {
            return Wrapped.VisitNamedType(symbol, Context);
        }

        public override string VisitNamespace(INamespaceSymbol symbol)
        {
            return Wrapped.VisitNamespace(symbol, Context);
        }

        public override string VisitParameter(IParameterSymbol symbol)
        {
            return Wrapped.VisitParameter(symbol, Context);
        }

        public override string VisitPointerType(IPointerTypeSymbol symbol)
        {
            return Wrapped.VisitPointerType(symbol, Context);
        }

        public override string VisitProperty(IPropertySymbol symbol)
        {
            return Wrapped.VisitProperty(symbol, Context);
        }

        public override string VisitRangeVariable(IRangeVariableSymbol symbol)
        {
            return Wrapped.VisitRangeVariable(symbol, Context);
        }

        public override string VisitTypeParameter(ITypeParameterSymbol symbol)
        {
            return Wrapped.VisitTypeParameter(symbol, Context);
        }
    }
}
