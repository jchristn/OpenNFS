namespace OpenNFS.XdrGen.Emission
{
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrEmissionSymbolSupport
    {
        private readonly XdrEmissionSymbolCollector collector;
        private readonly XdrEmissionTypeReferenceSupport typeReferences;
        private readonly XdrEmissionNumericLiteralSupport literals;

        internal XdrEmissionSymbolSupport(XdrEmissionContext context)
        {
            collector = new XdrEmissionSymbolCollector(context);
            typeReferences = new XdrEmissionTypeReferenceSupport(context);
            literals = new XdrEmissionNumericLiteralSupport(context);
        }

        internal void CollectNumericSymbols()
        {
            collector.CollectNumericSymbols();
        }

        internal void CollectDeclaredTypeKinds()
        {
            collector.CollectDeclaredTypeKinds();
        }

        internal CodecValueDescriptor ResolveCodecValueDescriptor(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
        {
            return typeReferences.ResolveCodecValueDescriptor(ownerTypeName, typeSpecifier, declarator);
        }

        internal CSharpTypeReference ResolveTypeReference(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
        {
            return typeReferences.ResolveTypeReference(ownerTypeName, typeSpecifier, declarator);
        }

        internal string ResolveUnionCaseLabel(PropertyDefinition discriminantProperty, string caseLabel)
        {
            return literals.ResolveUnionCaseLabel(discriminantProperty, caseLabel);
        }

        internal string ResolveBoundArgumentLiteral(string? boundExpression)
        {
            return literals.ResolveBoundArgumentLiteral(boundExpression);
        }

        internal string BuildMaximumArgumentLiteral(string? boundExpression)
        {
            return literals.BuildMaximumArgumentLiteral(boundExpression);
        }

        internal string ResolveFixedCountLiteral(string boundExpression)
        {
            return literals.ResolveFixedCountLiteral(boundExpression);
        }

        internal ulong ResolveExpressionValue(string expression)
        {
            return literals.ResolveExpressionValue(expression);
        }

        internal string ResolveNumericLiteral(string symbolName)
        {
            return literals.ResolveNumericLiteral(symbolName);
        }

        internal string ResolveExpressionLiteral(string expression)
        {
            return literals.ResolveExpressionLiteral(expression);
        }
    }
}
