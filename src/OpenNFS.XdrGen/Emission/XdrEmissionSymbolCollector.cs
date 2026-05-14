namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.IO;
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrEmissionSymbolCollector
    {
        private readonly XdrEmissionContext context;

        internal XdrEmissionSymbolCollector(XdrEmissionContext context)
        {
            this.context = context;
        }

        internal void CollectNumericSymbols()
        {
            foreach (XdrDocument document in context.Documents)
            {
                foreach (XdrDefinition definition in document.Definitions)
                {
                    switch (definition)
                    {
                        case XdrConstDefinition constDefinition:
                            AddNumericSymbol(constDefinition.Name, constDefinition.ValueExpression);
                            break;

                        case XdrTypedefDefinition typedefDefinition:
                            CollectNumericSymbols(typedefDefinition.TypeSpecifier);
                            break;

                        case XdrTypeDefinition typeDefinition:
                            CollectNumericSymbols(typeDefinition.TypeSpecifier);
                            break;

                        case XdrProgramDefinition programDefinition:
                            AddNumericSymbol(programDefinition.Name, programDefinition.ValueExpression);
                            foreach (XdrVersionDefinition version in programDefinition.Versions)
                            {
                                AddNumericSymbol(version.Name, version.ValueExpression);

                                foreach (XdrProcedureDefinition procedure in version.Procedures)
                                {
                                    AddNumericSymbol(procedure.Name, procedure.ValueExpression);
                                }
                            }

                            break;
                    }
                }
            }
        }

        internal void CollectDeclaredTypeKinds()
        {
            foreach (XdrDocument document in context.Documents)
            {
                foreach (XdrDefinition definition in document.Definitions)
                {
                    switch (definition)
                    {
                        case XdrTypedefDefinition typedefDefinition:
                            AddDeclaredTypeKind(typedefDefinition.Declarator.Identifier, EmittedTypeKind.Class);
                            break;

                        case XdrTypeDefinition typeDefinition when typeDefinition.TypeSpecifier is XdrEnumTypeSpecifier enumTypeSpecifier:
                            if (!string.IsNullOrWhiteSpace(enumTypeSpecifier.Name))
                            {
                                AddDeclaredTypeKind(enumTypeSpecifier.Name, EmittedTypeKind.Enum);
                            }

                            break;

                        case XdrTypeDefinition typeDefinition when typeDefinition.TypeSpecifier is XdrStructTypeSpecifier structTypeSpecifier:
                            if (!string.IsNullOrWhiteSpace(structTypeSpecifier.Name))
                            {
                                AddDeclaredTypeKind(structTypeSpecifier.Name, EmittedTypeKind.Class);
                            }

                            break;

                        case XdrTypeDefinition typeDefinition when typeDefinition.TypeSpecifier is XdrUnionTypeSpecifier unionTypeSpecifier:
                            if (!string.IsNullOrWhiteSpace(unionTypeSpecifier.Name))
                            {
                                AddDeclaredTypeKind(unionTypeSpecifier.Name, EmittedTypeKind.Class);
                            }

                            break;
                    }
                }
            }
        }

        private void CollectNumericSymbols(XdrTypeSpecifier typeSpecifier)
        {
            switch (typeSpecifier)
            {
                case XdrEnumTypeSpecifier enumTypeSpecifier:
                    foreach (XdrEnumMember member in enumTypeSpecifier.Members)
                    {
                        AddNumericSymbol(member.Name, member.ValueExpression);
                    }

                    break;

                case XdrStructTypeSpecifier structTypeSpecifier:
                    foreach (XdrFieldDeclaration field in structTypeSpecifier.Fields)
                    {
                        CollectNumericSymbols(field.TypeSpecifier);
                    }

                    break;

                case XdrUnionTypeSpecifier unionTypeSpecifier:
                    CollectNumericSymbols(unionTypeSpecifier.SwitchType);
                    foreach (XdrUnionArm arm in unionTypeSpecifier.Arms)
                    {
                        if (arm.Declaration is not null)
                        {
                            CollectNumericSymbols(arm.Declaration.TypeSpecifier);
                        }
                    }

                    break;
            }
        }

        private void AddDeclaredTypeKind(string typeName, EmittedTypeKind typeKind)
        {
            if (context.DeclaredTypeKinds.TryGetValue(typeName, out EmittedTypeKind existingKind))
            {
                if (existingKind != typeKind)
                {
                    throw new InvalidDataException("Duplicate XDR type name '" + typeName + "' resolves to conflicting emitted kinds.");
                }

                return;
            }

            context.DeclaredTypeKinds.Add(typeName, typeKind);
        }

        private void AddNumericSymbol(string name, string expression)
        {
            if (context.NumericSymbols.TryGetValue(name, out string? existingExpression))
            {
                if (!string.Equals(existingExpression, expression, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Duplicate XDR numeric symbol '" + name + "' resolves to conflicting expressions: '" + existingExpression + "' and '" + expression + "'.");
                }

                return;
            }

            context.NumericSymbols.Add(name, expression);
        }
    }
}
