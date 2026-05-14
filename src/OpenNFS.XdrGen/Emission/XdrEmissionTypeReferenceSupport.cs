namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.IO;
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrEmissionTypeReferenceSupport
    {
        private readonly XdrEmissionContext context;

        internal XdrEmissionTypeReferenceSupport(XdrEmissionContext context)
        {
            this.context = context;
        }

        internal CodecValueDescriptor ResolveCodecValueDescriptor(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
        {
            switch (typeSpecifier)
            {
                case XdrBuiltinTypeSpecifier builtinTypeSpecifier:
                    return ResolveBuiltinCodecValueDescriptor(builtinTypeSpecifier);

                case XdrIdentifierTypeSpecifier identifierTypeSpecifier:
                    return ResolveNamedCodecValueDescriptor(identifierTypeSpecifier.Name);

                case XdrQualifiedTypeSpecifier qualifiedTypeSpecifier:
                    return ResolveNamedCodecValueDescriptor(qualifiedTypeSpecifier.Name);

                case XdrEnumTypeSpecifier enumTypeSpecifier:
                {
                    string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                    context.Types.EmitTypeFromSpecifier(anonymousTypeName, enumTypeSpecifier);
                    return new CodecValueDescriptor(CodecValueKind.Enum, anonymousTypeName, isValueType: true);
                }

                case XdrStructTypeSpecifier structTypeSpecifier:
                {
                    string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                    context.Types.EmitTypeFromSpecifier(anonymousTypeName, structTypeSpecifier);
                    return new CodecValueDescriptor(CodecValueKind.Class, anonymousTypeName, isValueType: false);
                }

                case XdrUnionTypeSpecifier unionTypeSpecifier:
                {
                    string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                    context.Types.EmitTypeFromSpecifier(anonymousTypeName, unionTypeSpecifier);
                    return new CodecValueDescriptor(CodecValueKind.Class, anonymousTypeName, isValueType: false);
                }

                default:
                    throw new InvalidDataException("Unsupported XDR type specifier for codec emission: " + typeSpecifier.GetType().FullName);
            }
        }

        internal CSharpTypeReference ResolveTypeReference(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
        {
            if (typeSpecifier is XdrBuiltinTypeSpecifier builtinTypeSpecifier)
            {
                return ResolveBuiltinTypeReference(builtinTypeSpecifier, declarator);
            }

            if (typeSpecifier is XdrIdentifierTypeSpecifier identifierTypeSpecifier)
            {
                if (TryResolveSyntheticBuiltinAlias(identifierTypeSpecifier.Name, out CSharpTypeReference aliasType))
                {
                    return ApplyDeclarator(aliasType, declarator);
                }

                if (TryResolveExternalTypeReference(identifierTypeSpecifier.Name, out CSharpTypeReference externalType))
                {
                    return ApplyDeclarator(externalType, declarator);
                }

                return ApplyDeclarator(
                    new CSharpTypeReference(identifierTypeSpecifier.Name, isValueType: false),
                    declarator);
            }

            if (typeSpecifier is XdrQualifiedTypeSpecifier qualifiedTypeSpecifier)
            {
                return ApplyDeclarator(
                    new CSharpTypeReference(qualifiedTypeSpecifier.Name, isValueType: false),
                    declarator);
            }

            if (typeSpecifier is XdrEnumTypeSpecifier enumTypeSpecifier)
            {
                string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                context.Types.EmitTypeFromSpecifier(anonymousTypeName, enumTypeSpecifier);
                return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
            }

            if (typeSpecifier is XdrStructTypeSpecifier structTypeSpecifier)
            {
                string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                context.Types.EmitTypeFromSpecifier(anonymousTypeName, structTypeSpecifier);
                return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
            }

            if (typeSpecifier is XdrUnionTypeSpecifier unionTypeSpecifier)
            {
                string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                context.Types.EmitTypeFromSpecifier(anonymousTypeName, unionTypeSpecifier);
                return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
            }

            throw new InvalidDataException("Unsupported XDR field type specifier for emission: " + typeSpecifier.GetType().FullName);
        }

        private CodecValueDescriptor ResolveBuiltinCodecValueDescriptor(XdrBuiltinTypeSpecifier builtinTypeSpecifier)
        {
            if (string.Equals(builtinTypeSpecifier.Name, "opaque", StringComparison.Ordinal))
            {
                return new CodecValueDescriptor(CodecValueKind.Opaque, "byte[]", isValueType: false);
            }

            if (string.Equals(builtinTypeSpecifier.Name, "string", StringComparison.Ordinal))
            {
                return new CodecValueDescriptor(CodecValueKind.String, "string", isValueType: false);
            }

            return builtinTypeSpecifier.Name switch
            {
                "bool" => new CodecValueDescriptor(CodecValueKind.Bool, "bool", isValueType: true),
                "int" => new CodecValueDescriptor(CodecValueKind.Int32, "int", isValueType: true),
                "unsigned" => new CodecValueDescriptor(CodecValueKind.UInt32, "uint", isValueType: true),
                "unsigned int" => new CodecValueDescriptor(CodecValueKind.UInt32, "uint", isValueType: true),
                "unsigned long" => new CodecValueDescriptor(CodecValueKind.UInt32, "uint", isValueType: true),
                "hyper" => new CodecValueDescriptor(CodecValueKind.Int64, "long", isValueType: true),
                "unsigned hyper" => new CodecValueDescriptor(CodecValueKind.UInt64, "ulong", isValueType: true),
                "float" => new CodecValueDescriptor(CodecValueKind.Float, "float", isValueType: true),
                "double" => new CodecValueDescriptor(CodecValueKind.Double, "double", isValueType: true),
                _ => throw new InvalidDataException("Unsupported XDR built-in type for codec emission: " + builtinTypeSpecifier.Name),
            };
        }

        private CodecValueDescriptor ResolveNamedCodecValueDescriptor(string identifier)
        {
            switch (identifier)
            {
                case "long":
                    return new CodecValueDescriptor(CodecValueKind.Int32, "long", isValueType: true);

                case "int32_t":
                    return new CodecValueDescriptor(CodecValueKind.Int32, "int", isValueType: true);

                case "uint32_t":
                    return new CodecValueDescriptor(CodecValueKind.UInt32, "uint", isValueType: true);

                case "int64_t":
                    return new CodecValueDescriptor(CodecValueKind.Int64, "long", isValueType: true);

                case "uint64_t":
                    return new CodecValueDescriptor(CodecValueKind.UInt64, "ulong", isValueType: true);

                case "authsys_parms" when !string.Equals(context.Mapping.OutputNamespace, "OpenNFS.Rpc.Generated", StringComparison.Ordinal):
                    return new CodecValueDescriptor(CodecValueKind.Class, "OpenNFS.Rpc.Generated.authsys_parms", isValueType: false);
            }

            if (!context.DeclaredTypeKinds.TryGetValue(identifier, out EmittedTypeKind emittedTypeKind))
            {
                throw new InvalidDataException("Unsupported XDR named type for codec emission: " + identifier);
            }

            return emittedTypeKind switch
            {
                EmittedTypeKind.Enum => new CodecValueDescriptor(CodecValueKind.Enum, identifier, isValueType: true),
                EmittedTypeKind.Class => new CodecValueDescriptor(CodecValueKind.Class, identifier, isValueType: false),
                _ => throw new InvalidDataException("Unsupported emitted XDR type kind for codec emission: " + emittedTypeKind),
            };
        }

        private static CSharpTypeReference ResolveBuiltinTypeReference(XdrBuiltinTypeSpecifier builtinTypeSpecifier, XdrDeclarator declarator)
        {
            if (string.Equals(builtinTypeSpecifier.Name, "opaque", StringComparison.Ordinal))
            {
                return new CSharpTypeReference("byte[]", isValueType: false, isNullable: true);
            }

            if (string.Equals(builtinTypeSpecifier.Name, "string", StringComparison.Ordinal))
            {
                return new CSharpTypeReference("string", isValueType: false, isNullable: true);
            }

            CSharpTypeReference builtinType = builtinTypeSpecifier.Name switch
            {
                "bool" => new CSharpTypeReference("bool", isValueType: true),
                "int" => new CSharpTypeReference("int", isValueType: true),
                "unsigned" => new CSharpTypeReference("uint", isValueType: true),
                "unsigned int" => new CSharpTypeReference("uint", isValueType: true),
                "unsigned long" => new CSharpTypeReference("uint", isValueType: true),
                "hyper" => new CSharpTypeReference("long", isValueType: true),
                "unsigned hyper" => new CSharpTypeReference("ulong", isValueType: true),
                "float" => new CSharpTypeReference("float", isValueType: true),
                "double" => new CSharpTypeReference("double", isValueType: true),
                "quadruple" => new CSharpTypeReference("decimal", isValueType: true),
                "void" => new CSharpTypeReference("object", isValueType: false, isNullable: true),
                _ => throw new InvalidDataException("Unsupported XDR built-in type for emission: " + builtinTypeSpecifier.Name),
            };

            return ApplyDeclarator(builtinType, declarator);
        }

        private static CSharpTypeReference ApplyDeclarator(CSharpTypeReference baseType, XdrDeclarator declarator)
        {
            if (declarator.Kind == XdrDeclaratorKind.FixedArray || declarator.Kind == XdrDeclaratorKind.VariableArray)
            {
                return new CSharpTypeReference(baseType.BaseTypeName + "[]", isValueType: false, isNullable: true);
            }

            if (declarator.Kind == XdrDeclaratorKind.Pointer)
            {
                return baseType.MakeNullable();
            }

            return baseType.DefaultForProperty();
        }

        private static bool TryResolveSyntheticBuiltinAlias(string identifier, out CSharpTypeReference aliasType)
        {
            if (string.Equals(identifier, "int32_t", StringComparison.Ordinal))
            {
                aliasType = new CSharpTypeReference("int", isValueType: true);
                return true;
            }

            if (string.Equals(identifier, "uint32_t", StringComparison.Ordinal))
            {
                aliasType = new CSharpTypeReference("uint", isValueType: true);
                return true;
            }

            if (string.Equals(identifier, "int64_t", StringComparison.Ordinal))
            {
                aliasType = new CSharpTypeReference("long", isValueType: true);
                return true;
            }

            if (string.Equals(identifier, "uint64_t", StringComparison.Ordinal))
            {
                aliasType = new CSharpTypeReference("ulong", isValueType: true);
                return true;
            }

            aliasType = null!;
            return false;
        }

        private bool TryResolveExternalTypeReference(string identifier, out CSharpTypeReference externalType)
        {
            if (!string.Equals(context.Mapping.OutputNamespace, "OpenNFS.Rpc.Generated", StringComparison.Ordinal)
                && string.Equals(identifier, "authsys_parms", StringComparison.Ordinal))
            {
                externalType = new CSharpTypeReference("OpenNFS.Rpc.Generated.authsys_parms", isValueType: false);
                return true;
            }

            externalType = null!;
            return false;
        }
    }
}
