namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using OpenNFS.XdrGen.Model;

    /// <summary>
    /// Emits deterministic C# model files from parsed XDR documents.
    /// </summary>
    public sealed class XdrCSharpEmitter
    {
        /// <summary>
        /// Emits generated C# files for one configured XDR project mapping.
        /// </summary>
        /// <param name="mapping">Project mapping to emit.</param>
        /// <param name="parsedDocuments">Parsed documents keyed by absolute source path.</param>
        /// <returns>The generated files in deterministic path order.</returns>
        public IReadOnlyList<GeneratedCSharpFile> EmitProject(
            XdrProjectMapping mapping,
            IReadOnlyDictionary<string, XdrDocument> parsedDocuments)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            ArgumentNullException.ThrowIfNull(parsedDocuments);

            List<XdrDocument> entryPointDocuments = new List<XdrDocument>();
            foreach (string entryPointFile in mapping.EntryPointFiles)
            {
                string fullPath = Path.GetFullPath(entryPointFile);
                if (!parsedDocuments.TryGetValue(fullPath, out XdrDocument? document))
                {
                    throw new InvalidDataException("The parsed XDR document was not found for generation entry point: " + fullPath);
                }

                entryPointDocuments.Add(document);
            }

            EmissionContext context = new EmissionContext(mapping, entryPointDocuments);
            context.Emit();
            return context.GeneratedFiles
                .OrderBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class EmissionContext
        {
            private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
            {
                "abstract",
                "as",
                "base",
                "bool",
                "break",
                "byte",
                "case",
                "catch",
                "char",
                "checked",
                "class",
                "const",
                "continue",
                "decimal",
                "default",
                "delegate",
                "do",
                "double",
                "else",
                "enum",
                "event",
                "explicit",
                "extern",
                "false",
                "finally",
                "fixed",
                "float",
                "for",
                "foreach",
                "goto",
                "if",
                "implicit",
                "in",
                "int",
                "interface",
                "internal",
                "is",
                "lock",
                "long",
                "namespace",
                "new",
                "null",
                "object",
                "operator",
                "out",
                "override",
                "params",
                "private",
                "protected",
                "public",
                "readonly",
                "record",
                "ref",
                "return",
                "sbyte",
                "sealed",
                "short",
                "sizeof",
                "stackalloc",
                "static",
                "string",
                "struct",
                "switch",
                "this",
                "throw",
                "true",
                "try",
                "typeof",
                "uint",
                "ulong",
                "unchecked",
                "unsafe",
                "ushort",
                "using",
                "virtual",
                "void",
                "volatile",
                "while",
            };

            private readonly XdrProjectMapping mapping;
            private readonly IReadOnlyList<XdrDocument> documents;
            private readonly Dictionary<string, string> numericSymbols;
            private readonly Dictionary<string, EmittedTypeKind> declaredTypeKinds;
            private readonly Dictionary<string, string> resolvedNumericLiterals;
            private readonly Dictionary<string, GeneratedCSharpFile> generatedFilesByTypeName;
            private readonly List<GeneratedCSharpFile> generatedFiles;
            private readonly string constantsTypeName;

            public EmissionContext(XdrProjectMapping mapping, IReadOnlyList<XdrDocument> documents)
            {
                this.mapping = mapping;
                this.documents = documents;
                numericSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
                declaredTypeKinds = new Dictionary<string, EmittedTypeKind>(StringComparer.Ordinal);
                resolvedNumericLiterals = new Dictionary<string, string>(StringComparer.Ordinal);
                generatedFilesByTypeName = new Dictionary<string, GeneratedCSharpFile>(StringComparer.Ordinal);
                generatedFiles = new List<GeneratedCSharpFile>();
                constantsTypeName = CreatePascalCase(mapping.Id) + "Constants";
            }

            public IReadOnlyList<GeneratedCSharpFile> GeneratedFiles => generatedFiles;

            public void Emit()
            {
                CollectDeclaredTypeKinds();
                CollectNumericSymbols();
                EmitConstantsClass();

                foreach (XdrDocument document in documents)
                {
                    foreach (XdrDefinition definition in document.Definitions)
                    {
                        switch (definition)
                        {
                            case XdrConstDefinition:
                                break;

                            case XdrTypedefDefinition typedefDefinition:
                                EmitTypedefDefinition(typedefDefinition);
                                break;

                            case XdrTypeDefinition typeDefinition:
                                EmitTopLevelTypeDefinition(typeDefinition);
                                break;

                            case XdrProgramDefinition programDefinition:
                                EmitProgramDefinition(programDefinition);
                                break;

                            default:
                                throw new InvalidDataException("Unsupported XDR definition type for emission: " + definition.GetType().FullName);
                        }
                    }
                }
            }

            private void CollectNumericSymbols()
            {
                foreach (XdrDocument document in documents)
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

            private void CollectDeclaredTypeKinds()
            {
                foreach (XdrDocument document in documents)
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
                if (declaredTypeKinds.TryGetValue(typeName, out EmittedTypeKind existingKind))
                {
                    if (existingKind != typeKind)
                    {
                        throw new InvalidDataException("Duplicate XDR type name '" + typeName + "' resolves to conflicting emitted kinds.");
                    }

                    return;
                }

                declaredTypeKinds.Add(typeName, typeKind);
            }

            private void AddNumericSymbol(string name, string expression)
            {
                if (numericSymbols.TryGetValue(name, out string? existingExpression))
                {
                    if (!string.Equals(existingExpression, expression, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Duplicate XDR numeric symbol '" + name + "' resolves to conflicting expressions: '" + existingExpression + "' and '" + expression + "'.");
                    }

                    return;
                }

                numericSymbols.Add(name, expression);
            }

            private void EmitConstantsClass()
            {
                List<XdrConstDefinition> constants = documents
                    .SelectMany(document => document.Definitions)
                    .OfType<XdrConstDefinition>()
                    .ToList();

                if (constants.Count < 1)
                {
                    return;
                }

                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    /// <summary>");
                writer.AppendLine("    /// Defines emitted XDR numeric constants for the " + EscapeXml(mapping.Id) + " corpus.");
                writer.AppendLine("    /// </summary>");
                writer.AppendLine("    public static class " + EscapeIdentifier(constantsTypeName));
                writer.AppendLine("    {");

                foreach (XdrConstDefinition constant in constants)
                {
                    writer.AppendLine("        /// <summary>");
                    writer.AppendLine("        /// Represents the XDR constant <c>" + EscapeXml(constant.Name) + "</c>.");
                    writer.AppendLine("        /// </summary>");
                    writer.AppendLine("        public const ulong " + EscapeIdentifier(constant.Name) + " = " + ResolveNumericLiteral(constant.Name) + ";");
                    writer.AppendLine();
                }

                writer.RemoveTrailingBlankLine();
                writer.AppendLine("    }");
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");

                AddGeneratedFile(constantsTypeName, writer.ToString());
            }

            private void EmitTypedefDefinition(XdrTypedefDefinition typedefDefinition)
            {
                string aliasName = typedefDefinition.Declarator.Identifier;

                if (IsAnonymousTypeSpecifier(typedefDefinition.TypeSpecifier))
                {
                    EmitTypeFromSpecifier(aliasName, typedefDefinition.TypeSpecifier);
                    return;
                }

                if (typedefDefinition.TypeSpecifier is XdrQualifiedTypeSpecifier qualifiedTypeSpecifier
                    && string.Equals(qualifiedTypeSpecifier.Name, aliasName, StringComparison.Ordinal)
                    && typedefDefinition.Declarator.Kind == XdrDeclaratorKind.Pointer)
                {
                    return;
                }

                EmitAliasType(aliasName, typedefDefinition.TypeSpecifier, typedefDefinition.Declarator);
            }

            private void EmitTopLevelTypeDefinition(XdrTypeDefinition typeDefinition)
            {
                switch (typeDefinition.TypeSpecifier)
                {
                    case XdrEnumTypeSpecifier enumTypeSpecifier:
                        EmitNamedTopLevelType(enumTypeSpecifier.Name, typeDefinition.TypeSpecifier);
                        break;

                    case XdrStructTypeSpecifier structTypeSpecifier:
                        EmitNamedTopLevelType(structTypeSpecifier.Name, typeDefinition.TypeSpecifier);
                        break;

                    case XdrUnionTypeSpecifier unionTypeSpecifier:
                        EmitNamedTopLevelType(unionTypeSpecifier.Name, typeDefinition.TypeSpecifier);
                        break;

                    default:
                        throw new InvalidDataException("Unsupported top-level XDR type definition for emission: " + typeDefinition.TypeSpecifier.GetType().FullName);
                }
            }

            private void EmitNamedTopLevelType(string? typeName, XdrTypeSpecifier typeSpecifier)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    throw new InvalidDataException("Top-level XDR type definitions must have explicit names for emission.");
                }

                EmitTypeFromSpecifier(typeName, typeSpecifier);
            }

            private void EmitProgramDefinition(XdrProgramDefinition programDefinition)
            {
                string typeName = programDefinition.Name + "_Program";
                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    /// <summary>");
                writer.AppendLine("    /// Defines emitted numeric identifiers for the XDR program <c>" + EscapeXml(programDefinition.Name) + "</c>.");
                writer.AppendLine("    /// </summary>");
                writer.AppendLine("    public static class " + EscapeIdentifier(typeName));
                writer.AppendLine("    {");
                writer.AppendLine("        /// <summary>");
                writer.AppendLine("        /// Gets the numeric program identifier.");
                writer.AppendLine("        /// </summary>");
                writer.AppendLine("        public const ulong Program = " + ResolveExpressionLiteral(programDefinition.ValueExpression) + ";");
                writer.AppendLine();

                foreach (XdrVersionDefinition version in programDefinition.Versions)
                {
                    string versionFieldName = "Version_" + version.Name;
                    writer.AppendLine("        /// <summary>");
                    writer.AppendLine("        /// Gets the numeric version identifier for <c>" + EscapeXml(version.Name) + "</c>.");
                    writer.AppendLine("        /// </summary>");
                    writer.AppendLine("        public const ulong " + EscapeIdentifier(versionFieldName) + " = " + ResolveExpressionLiteral(version.ValueExpression) + ";");
                    writer.AppendLine();

                    foreach (XdrProcedureDefinition procedure in version.Procedures)
                    {
                        string procedureFieldName = "Procedure_" + version.Name + "_" + procedure.Name;
                        writer.AppendLine("        /// <summary>");
                        writer.AppendLine("        /// Gets the numeric procedure identifier for <c>" + EscapeXml(procedure.Name) + "</c> in version <c>" + EscapeXml(version.Name) + "</c>.");
                        writer.AppendLine("        /// </summary>");
                        writer.AppendLine("        public const ulong " + EscapeIdentifier(procedureFieldName) + " = " + ResolveExpressionLiteral(procedure.ValueExpression) + ";");
                        writer.AppendLine();
                    }
                }

                writer.RemoveTrailingBlankLine();
                writer.AppendLine("    }");
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");

                AddGeneratedFile(typeName, writer.ToString());
            }

            private void EmitTypeFromSpecifier(string typeName, XdrTypeSpecifier typeSpecifier)
            {
                if (generatedFilesByTypeName.ContainsKey(typeName))
                {
                    return;
                }

                switch (typeSpecifier)
                {
                    case XdrEnumTypeSpecifier enumTypeSpecifier:
                        AddDeclaredTypeKind(typeName, EmittedTypeKind.Enum);
                        EmitEnumType(typeName, enumTypeSpecifier);
                        break;

                    case XdrStructTypeSpecifier structTypeSpecifier:
                        AddDeclaredTypeKind(typeName, EmittedTypeKind.Class);
                        EmitStructType(typeName, structTypeSpecifier);
                        break;

                    case XdrUnionTypeSpecifier unionTypeSpecifier:
                        AddDeclaredTypeKind(typeName, EmittedTypeKind.Class);
                        EmitUnionType(typeName, unionTypeSpecifier);
                        break;

                    default:
                        throw new InvalidDataException("Unsupported XDR type specifier for emission: " + typeSpecifier.GetType().FullName);
                }
            }

            private void EmitEnumType(string typeName, XdrEnumTypeSpecifier enumTypeSpecifier)
            {
                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    /// <summary>");
                writer.AppendLine("    /// Represents the XDR enum <c>" + EscapeXml(typeName) + "</c>.");
                writer.AppendLine("    /// </summary>");
                writer.AppendLine("    public enum " + EscapeIdentifier(typeName) + " : int");
                writer.AppendLine("    {");

                foreach (XdrEnumMember member in enumTypeSpecifier.Members)
                {
                    writer.AppendLine("        /// <summary>");
                    writer.AppendLine("        /// Represents the XDR enum member <c>" + EscapeXml(member.Name) + "</c>.");
                    writer.AppendLine("        /// </summary>");
                    writer.AppendLine("        " + EscapeIdentifier(member.Name) + " = " + ResolveExpressionLiteral(member.ValueExpression) + ",");
                    writer.AppendLine();
                }

                writer.RemoveTrailingBlankLine();
                writer.AppendLine("    }");
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");

                AddGeneratedFile(typeName, writer.ToString());
                EmitEnumCodecType(typeName);
            }

            private void EmitStructType(string typeName, XdrStructTypeSpecifier structTypeSpecifier)
            {
                List<PropertyDefinition> properties = new List<PropertyDefinition>();
                HashSet<string> usedPropertyNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (XdrFieldDeclaration field in structTypeSpecifier.Fields)
                {
                    properties.Add(CreatePropertyDefinition(
                        ownerTypeName: typeName,
                        memberTypeSpecifier: field.TypeSpecifier,
                        declarator: field.Declarator,
                        usedPropertyNames: usedPropertyNames,
                        summary: "Gets or sets the XDR field <c>" + EscapeXml(field.Declarator.Identifier) + "</c>."));
                }

                EmitClassType(
                    typeName,
                    "Represents the XDR struct <c>" + EscapeXml(typeName) + "</c>.",
                    properties);
                EmitStructCodecPartial(typeName, properties);
            }

            private void EmitUnionType(string typeName, XdrUnionTypeSpecifier unionTypeSpecifier)
            {
                List<PropertyDefinition> properties = new List<PropertyDefinition>();
                List<UnionArmDefinition> arms = new List<UnionArmDefinition>();
                HashSet<string> usedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
                XdrDeclarator discriminantDeclarator = new XdrDeclarator(
                    unionTypeSpecifier.SwitchName,
                    XdrDeclaratorKind.Identifier,
                    null);
                PropertyDefinition discriminantProperty = CreatePropertyDefinition(
                    ownerTypeName: typeName,
                    memberTypeSpecifier: unionTypeSpecifier.SwitchType,
                    declarator: discriminantDeclarator,
                    usedPropertyNames: usedPropertyNames,
                    summary: "Gets or sets the union discriminant <c>" + EscapeXml(unionTypeSpecifier.SwitchName) + "</c>.");
                properties.Add(discriminantProperty);

                foreach (XdrUnionArm arm in unionTypeSpecifier.Arms)
                {
                    if (arm.Declaration is null)
                    {
                        continue;
                    }

                    string uniquePropertyName = GetUniquePropertyName(typeName, arm.Declaration.Declarator.Identifier, usedPropertyNames);
                    CSharpTypeReference propertyType = ResolveTypeReference(typeName, arm.Declaration.TypeSpecifier, arm.Declaration.Declarator);
                    string caseSummary = arm.IsDefault
                        ? "default"
                        : string.Join(", ", arm.CaseLabels.Select(label => "<c>" + EscapeXml(label) + "</c>"));

                    PropertyDefinition armProperty = new PropertyDefinition(
                        name: uniquePropertyName,
                        wireName: arm.Declaration.Declarator.Identifier,
                        typeReference: propertyType,
                        valueDescriptor: ResolveCodecValueDescriptor(typeName, arm.Declaration.TypeSpecifier, arm.Declaration.Declarator),
                        declarator: arm.Declaration.Declarator,
                        summary: "Gets or sets the union arm <c>" + EscapeXml(arm.Declaration.Declarator.Identifier) + "</c> selected by " + caseSummary + ".");
                    properties.Add(armProperty);
                    arms.Add(new UnionArmDefinition(armProperty, arm.CaseLabels, arm.IsDefault));
                }

                EmitClassType(
                    typeName,
                    "Represents the XDR union <c>" + EscapeXml(typeName) + "</c>.",
                    properties);
                EmitUnionCodecPartial(typeName, discriminantProperty, arms);
            }

            private void EmitAliasType(string typeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
            {
                CSharpTypeReference valueType = ResolveTypeReference(typeName, typeSpecifier, declarator);
                PropertyDefinition valueProperty = new PropertyDefinition(
                    name: GetUniquePropertyName(typeName, "Value", new HashSet<string>(StringComparer.Ordinal)),
                    wireName: declarator.Identifier,
                    typeReference: valueType,
                    valueDescriptor: ResolveCodecValueDescriptor(typeName, typeSpecifier, declarator),
                    declarator: declarator,
                    summary: "Gets or sets the underlying XDR value.");
                List<PropertyDefinition> properties = new List<PropertyDefinition> { valueProperty };

                EmitClassType(
                    typeName,
                    "Represents the emitted XDR typedef <c>" + EscapeXml(typeName) + "</c>.",
                    properties);
                EmitAliasCodecPartial(typeName, valueProperty);
            }

            private void EmitClassType(string typeName, string summary, IReadOnlyList<PropertyDefinition> properties)
            {
                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    /// <summary>");
                writer.AppendLine("    /// " + summary);
                writer.AppendLine("    /// </summary>");
                writer.AppendLine("    public sealed partial class " + EscapeIdentifier(typeName));
                writer.AppendLine("    {");

                foreach (PropertyDefinition property in properties)
                {
                    writer.AppendLine("        /// <summary>");
                    writer.AppendLine("        /// " + property.Summary);
                    writer.AppendLine("        /// </summary>");
                    writer.AppendLine("        public " + property.TypeName + " " + EscapeIdentifier(property.Name) + " { get; set; }");
                    writer.AppendLine();
                }

                writer.RemoveTrailingBlankLine();
                writer.AppendLine("    }");
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");

                AddGeneratedFile(typeName, writer.ToString());
            }

            private PropertyDefinition CreatePropertyDefinition(
                string ownerTypeName,
                XdrTypeSpecifier memberTypeSpecifier,
                XdrDeclarator declarator,
                ISet<string> usedPropertyNames,
                string summary)
            {
                string uniquePropertyName = GetUniquePropertyName(ownerTypeName, declarator.Identifier, usedPropertyNames);
                CSharpTypeReference propertyType = ResolveTypeReference(ownerTypeName, memberTypeSpecifier, declarator);
                CodecValueDescriptor valueDescriptor = ResolveCodecValueDescriptor(ownerTypeName, memberTypeSpecifier, declarator);
                return new PropertyDefinition(
                    name: uniquePropertyName,
                    wireName: declarator.Identifier,
                    typeReference: propertyType,
                    valueDescriptor: valueDescriptor,
                    declarator: declarator,
                    summary: summary);
            }

            private CodecValueDescriptor ResolveCodecValueDescriptor(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
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
                        EmitTypeFromSpecifier(anonymousTypeName, enumTypeSpecifier);
                        return new CodecValueDescriptor(CodecValueKind.Enum, anonymousTypeName, isValueType: true);
                    }

                    case XdrStructTypeSpecifier structTypeSpecifier:
                    {
                        string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                        EmitTypeFromSpecifier(anonymousTypeName, structTypeSpecifier);
                        return new CodecValueDescriptor(CodecValueKind.Class, anonymousTypeName, isValueType: false);
                    }

                    case XdrUnionTypeSpecifier unionTypeSpecifier:
                    {
                        string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                        EmitTypeFromSpecifier(anonymousTypeName, unionTypeSpecifier);
                        return new CodecValueDescriptor(CodecValueKind.Class, anonymousTypeName, isValueType: false);
                    }

                    default:
                        throw new InvalidDataException("Unsupported XDR type specifier for codec emission: " + typeSpecifier.GetType().FullName);
                }
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

                    case "authsys_parms" when !string.Equals(mapping.OutputNamespace, "OpenNFS.Rpc.Generated", StringComparison.Ordinal):
                        return new CodecValueDescriptor(CodecValueKind.Class, "OpenNFS.Rpc.Generated.authsys_parms", isValueType: false);
                }

                if (!declaredTypeKinds.TryGetValue(identifier, out EmittedTypeKind emittedTypeKind))
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

            private void EmitEnumCodecType(string typeName)
            {
                string codecTypeName = typeName + "_XdrCodec";
                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    using System;");
                writer.AppendLine("    using OpenNFS.Rpc.Xdr;");
                writer.AppendLine();
                writer.AppendLine("    internal static class " + EscapeIdentifier(codecTypeName));
                writer.AppendLine("    {");
                writer.AppendLine("        public static void Write(XdrWriter writer, " + EscapeIdentifier(typeName) + " value)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(writer);");
                writer.AppendLine("            writer.WriteInt32((int)value);");
                writer.AppendLine("        }");
                writer.AppendLine();
                writer.AppendLine("        public static " + EscapeIdentifier(typeName) + " Read(XdrReader reader)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(reader);");
                writer.AppendLine("            return (" + EscapeIdentifier(typeName) + ")reader.ReadInt32();");
                writer.AppendLine("        }");
                writer.AppendLine("    }");
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");

                AddGeneratedFile(codecTypeName, typeName + ".XdrCodec.g.cs", writer.ToString());
            }

            private void EmitAliasCodecPartial(string typeName, PropertyDefinition valueProperty)
            {
                CodeWriter writer = CreateCodecWriter();
                writer.AppendLine("    public sealed partial class " + EscapeIdentifier(typeName));
                writer.AppendLine("    {");
                AppendWriteMethodDocumentation(writer);
                writer.AppendLine("        public void WriteTo(XdrWriter writer)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(writer);");
                EmitWriteMember(writer, "this." + EscapeIdentifier(valueProperty.Name), typeName + "." + valueProperty.Name, valueProperty, "writer", 3);
                writer.AppendLine("        }");
                writer.AppendLine();
                AppendReadMethodDocumentation(writer, typeName);
                writer.AppendLine("        public static " + EscapeIdentifier(typeName) + " ReadFrom(XdrReader reader)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(reader);");
                writer.AppendLine("            " + EscapeIdentifier(typeName) + " value = new " + EscapeIdentifier(typeName) + "();");
                EmitReadMember(writer, "value." + EscapeIdentifier(valueProperty.Name), valueProperty, "reader", 3);
                writer.AppendLine("            return value;");
                writer.AppendLine("        }");
                writer.AppendLine("    }");
                AppendCodecWriterFooter(writer);

                AddGeneratedFile(typeName + "_XdrMethods", typeName + ".Xdr.g.cs", writer.ToString());
            }

            private void EmitStructCodecPartial(string typeName, IReadOnlyList<PropertyDefinition> properties)
            {
                CodeWriter writer = CreateCodecWriter();
                writer.AppendLine("    public sealed partial class " + EscapeIdentifier(typeName));
                writer.AppendLine("    {");
                AppendWriteMethodDocumentation(writer);
                writer.AppendLine("        public void WriteTo(XdrWriter writer)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(writer);");

                foreach (PropertyDefinition property in properties)
                {
                    EmitWriteMember(writer, "this." + EscapeIdentifier(property.Name), typeName + "." + property.Name, property, "writer", 3);
                }

                writer.AppendLine("        }");
                writer.AppendLine();
                AppendReadMethodDocumentation(writer, typeName);
                writer.AppendLine("        public static " + EscapeIdentifier(typeName) + " ReadFrom(XdrReader reader)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(reader);");
                writer.AppendLine("            " + EscapeIdentifier(typeName) + " value = new " + EscapeIdentifier(typeName) + "();");

                foreach (PropertyDefinition property in properties)
                {
                    EmitReadMember(writer, "value." + EscapeIdentifier(property.Name), property, "reader", 3);
                }

                writer.AppendLine("            return value;");
                writer.AppendLine("        }");
                writer.AppendLine("    }");
                AppendCodecWriterFooter(writer);

                AddGeneratedFile(typeName + "_XdrMethods", typeName + ".Xdr.g.cs", writer.ToString());
            }

            private void EmitUnionCodecPartial(string typeName, PropertyDefinition discriminantProperty, IReadOnlyList<UnionArmDefinition> arms)
            {
                CodeWriter writer = CreateCodecWriter();
                writer.AppendLine("    public sealed partial class " + EscapeIdentifier(typeName));
                writer.AppendLine("    {");
                AppendWriteMethodDocumentation(writer);
                writer.AppendLine("        public void WriteTo(XdrWriter writer)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(writer);");

                string discriminantTypeName = discriminantProperty.TypeReference.NonNullableDisplayName;
                string discriminantAccess = "this." + EscapeIdentifier(discriminantProperty.Name);
                string discriminantContext = typeName + "." + discriminantProperty.Name;
                string discriminantValueExpression = discriminantProperty.ValueDescriptor.IsValueType && discriminantProperty.TypeReference.IsNullable
                    ? "XdrGeneratedCodec.RequireValue(" + discriminantAccess + ", \"" + discriminantContext + "\")"
                    : discriminantAccess;

                writer.AppendLine("            " + discriminantTypeName + " discriminant = " + discriminantValueExpression + ";");
                EmitWriteScalarCore(writer, "discriminant", discriminantContext, discriminantProperty, "writer", 3);

                if (arms.Count > 0)
                {
                    writer.AppendLine("            switch (discriminant)");
                    writer.AppendLine("            {");

                    foreach (UnionArmDefinition arm in arms)
                    {
                        if (arm.IsDefault)
                        {
                            writer.AppendLine("                default:");
                        }
                        else
                        {
                            foreach (string caseLabel in arm.CaseLabels)
                            {
                                writer.AppendLine("                case " + ResolveUnionCaseLabel(discriminantProperty, caseLabel) + ":");
                            }
                        }

                        EmitWriteMember(writer, "this." + EscapeIdentifier(arm.Property.Name), typeName + "." + arm.Property.Name, arm.Property, "writer", 5);
                        writer.AppendLine("                    break;");
                    }

                    writer.AppendLine("            }");
                }

                writer.AppendLine("        }");
                writer.AppendLine();
                AppendReadMethodDocumentation(writer, typeName);
                writer.AppendLine("        public static " + EscapeIdentifier(typeName) + " ReadFrom(XdrReader reader)");
                writer.AppendLine("        {");
                writer.AppendLine("            ArgumentNullException.ThrowIfNull(reader);");
                writer.AppendLine("            " + EscapeIdentifier(typeName) + " value = new " + EscapeIdentifier(typeName) + "();");
                writer.AppendLine("            " + discriminantTypeName + " discriminant = " + BuildReadScalarExpression(discriminantProperty, "reader") + ";");
                writer.AppendLine("            value." + EscapeIdentifier(discriminantProperty.Name) + " = discriminant;");

                if (arms.Count > 0)
                {
                    writer.AppendLine("            switch (discriminant)");
                    writer.AppendLine("            {");

                    foreach (UnionArmDefinition arm in arms)
                    {
                        if (arm.IsDefault)
                        {
                            writer.AppendLine("                default:");
                        }
                        else
                        {
                            foreach (string caseLabel in arm.CaseLabels)
                            {
                                writer.AppendLine("                case " + ResolveUnionCaseLabel(discriminantProperty, caseLabel) + ":");
                            }
                        }

                        EmitReadMember(writer, "value." + EscapeIdentifier(arm.Property.Name), arm.Property, "reader", 5);
                        writer.AppendLine("                    break;");
                    }

                    writer.AppendLine("            }");
                }

                writer.AppendLine("            return value;");
                writer.AppendLine("        }");
                writer.AppendLine("    }");
                AppendCodecWriterFooter(writer);

                AddGeneratedFile(typeName + "_XdrMethods", typeName + ".Xdr.g.cs", writer.ToString());
            }

            private CodeWriter CreateCodecWriter()
            {
                CodeWriter writer = new CodeWriter();
                writer.AppendLine("// <auto-generated/>");
                writer.AppendLine("#nullable enable");
                writer.AppendLine("#pragma warning disable CS8981");
                writer.AppendLine("namespace " + mapping.OutputNamespace);
                writer.AppendLine("{");
                writer.AppendLine("    using System;");
                writer.AppendLine("    using OpenNFS.Rpc.Xdr;");
                writer.AppendLine();
                return writer;
            }

            private static void AppendWriteMethodDocumentation(CodeWriter writer)
            {
                writer.AppendLine("        /// <summary>");
                writer.AppendLine("        /// Writes the current instance to an XDR writer.");
                writer.AppendLine("        /// </summary>");
                writer.AppendLine("        /// <param name=\"writer\">The XDR writer to receive the encoded payload.</param>");
            }

            private static void AppendReadMethodDocumentation(CodeWriter writer, string typeName)
            {
                writer.AppendLine("        /// <summary>");
                writer.AppendLine("        /// Reads an instance of <see cref=\"" + EscapeIdentifier(typeName) + "\"/> from an XDR reader.");
                writer.AppendLine("        /// </summary>");
                writer.AppendLine("        /// <param name=\"reader\">The XDR reader supplying the encoded payload.</param>");
                writer.AppendLine("        /// <returns>The decoded instance.</returns>");
            }

            private static void AppendCodecWriterFooter(CodeWriter writer)
            {
                writer.AppendLine("}");
                writer.AppendLine("#pragma warning restore CS8981");
            }

            private void EmitWriteMember(
                CodeWriter writer,
                string memberAccess,
                string memberContext,
                PropertyDefinition property,
                string writerIdentifier,
                int indentLevel)
            {
                if (property.Declarator.Kind == XdrDeclaratorKind.Pointer)
                {
                    if (property.ValueDescriptor.IsValueType)
                    {
                        writer.AppendLine(indentLevel, "if (" + memberAccess + ".HasValue)");
                        writer.AppendLine(indentLevel, "{");
                        writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(true);");
                        EmitWriteScalarCore(
                            writer,
                            "XdrGeneratedCodec.RequireValue(" + memberAccess + ", \"" + memberContext + "\")",
                            memberContext,
                            property,
                            writerIdentifier,
                            indentLevel + 1);
                        writer.AppendLine(indentLevel, "}");
                        writer.AppendLine(indentLevel, "else");
                        writer.AppendLine(indentLevel, "{");
                        writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(false);");
                        writer.AppendLine(indentLevel, "}");
                    }
                    else
                    {
                        writer.AppendLine(indentLevel, "if (" + memberAccess + " is null)");
                        writer.AppendLine(indentLevel, "{");
                        writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(false);");
                        writer.AppendLine(indentLevel, "}");
                        writer.AppendLine(indentLevel, "else");
                        writer.AppendLine(indentLevel, "{");
                        writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(true);");
                        EmitWriteScalarCore(
                            writer,
                            "XdrGeneratedCodec.RequireReference(" + memberAccess + ", \"" + memberContext + "\")",
                            memberContext,
                            property,
                            writerIdentifier,
                            indentLevel + 1);
                        writer.AppendLine(indentLevel, "}");
                    }

                    return;
                }

                if ((property.Declarator.Kind == XdrDeclaratorKind.FixedArray || property.Declarator.Kind == XdrDeclaratorKind.VariableArray)
                    && property.ValueDescriptor.Kind != CodecValueKind.Opaque
                    && property.ValueDescriptor.Kind != CodecValueKind.String)
                {
                    string arrayValueVariable = EscapeIdentifier(property.Name + "Value");
                    string arrayTypeName = property.TypeReference.BaseTypeName;
                    string requireArrayExpression = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                        ? "XdrGeneratedCodec.RequireFixedLength(" + memberAccess + ", " + ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ", \"" + memberContext + "\")"
                        : "XdrGeneratedCodec.RequireReference(" + memberAccess + ", \"" + memberContext + "\")";
                    writer.AppendLine(indentLevel, arrayTypeName + " " + arrayValueVariable + " = " + requireArrayExpression + ";");

                    string arrayMethodName = property.Declarator.Kind == XdrDeclaratorKind.FixedArray ? "WriteFixedArray" : "WriteVariableArray";
                    string arrayCountArgument = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                        ? "expectedCount: " + ResolveFixedCountLiteral(property.Declarator.BoundExpression!)
                        : "maximumCount: " + ResolveBoundArgumentLiteral(property.Declarator.BoundExpression);

                    writer.AppendLine(indentLevel, writerIdentifier + "." + arrayMethodName + "<" + property.ValueDescriptor.TypeName + ">(");
                    writer.AppendLine(indentLevel + 1, arrayValueVariable + ",");
                    writer.AppendLine(indentLevel + 1, arrayCountArgument + ",");
                    writer.AppendLine(indentLevel + 1, "writeElement: static (xdrWriter, element) =>");
                    writer.AppendLine(indentLevel + 1, "{");
                    EmitWriteArrayElement(writer, property, memberContext + "[]", "element", "xdrWriter", indentLevel + 2);
                    writer.AppendLine(indentLevel + 1, "});");
                    return;
                }

                string scalarValueExpression = memberAccess;
                if (property.ValueDescriptor.IsValueType && property.TypeReference.IsNullable)
                {
                    scalarValueExpression = "XdrGeneratedCodec.RequireValue(" + memberAccess + ", \"" + memberContext + "\")";
                }

                EmitWriteScalarCore(writer, scalarValueExpression, memberContext, property, writerIdentifier, indentLevel);
            }

            private void EmitWriteArrayElement(
                CodeWriter writer,
                PropertyDefinition property,
                string memberContext,
                string elementExpression,
                string writerIdentifier,
                int indentLevel)
            {
                switch (property.ValueDescriptor.Kind)
                {
                    case CodecValueKind.Bool:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteBoolean(" + elementExpression + ");");
                        break;

                    case CodecValueKind.Int32:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt32(" + BuildInt32WriteExpression(property, elementExpression) + ");");
                        break;

                    case CodecValueKind.UInt32:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt32(" + elementExpression + ");");
                        break;

                    case CodecValueKind.Int64:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt64(" + elementExpression + ");");
                        break;

                    case CodecValueKind.UInt64:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt64(" + elementExpression + ");");
                        break;

                    case CodecValueKind.Float:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteSingle(" + elementExpression + ");");
                        break;

                    case CodecValueKind.Double:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteDouble(" + elementExpression + ");");
                        break;

                    case CodecValueKind.Enum:
                        writer.AppendLine(indentLevel, EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Write(" + writerIdentifier + ", " + elementExpression + ");");
                        break;

                    case CodecValueKind.Class:
                        writer.AppendLine(indentLevel, "XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\").WriteTo(" + writerIdentifier + ");");
                        break;

                    case CodecValueKind.String:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteString(XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\"));");
                        break;

                    case CodecValueKind.Opaque:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteVariableOpaque(XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\"));");
                        break;

                    default:
                        throw new InvalidDataException("Unsupported array element codec kind: " + property.ValueDescriptor.Kind);
                }
            }

            private void EmitWriteScalarCore(
                CodeWriter writer,
                string valueExpression,
                string memberContext,
                PropertyDefinition property,
                string writerIdentifier,
                int indentLevel)
            {
                switch (property.ValueDescriptor.Kind)
                {
                    case CodecValueKind.Bool:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteBoolean(" + valueExpression + ");");
                        break;

                    case CodecValueKind.Int32:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt32(" + BuildInt32WriteExpression(property, valueExpression) + ");");
                        break;

                    case CodecValueKind.UInt32:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt32(" + valueExpression + ");");
                        break;

                    case CodecValueKind.Int64:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt64(" + valueExpression + ");");
                        break;

                    case CodecValueKind.UInt64:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt64(" + valueExpression + ");");
                        break;

                    case CodecValueKind.Float:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteSingle(" + valueExpression + ");");
                        break;

                    case CodecValueKind.Double:
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteDouble(" + valueExpression + ");");
                        break;

                    case CodecValueKind.Opaque:
                        if (property.Declarator.Kind == XdrDeclaratorKind.FixedArray)
                        {
                            writer.AppendLine(
                                indentLevel,
                                "byte[] fixedOpaqueValue = XdrGeneratedCodec.RequireFixedLength(" + valueExpression + ", " + ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ", \"" + memberContext + "\");");
                            writer.AppendLine(indentLevel, writerIdentifier + ".WriteFixedOpaque(fixedOpaqueValue);");
                        }
                        else
                        {
                            string opaqueValueExpression = "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\")";
                            string maximumLengthArgument = property.Declarator.Kind == XdrDeclaratorKind.VariableArray
                                ? ", maximumLength: " + ResolveBoundArgumentLiteral(property.Declarator.BoundExpression)
                                : string.Empty;
                            writer.AppendLine(indentLevel, writerIdentifier + ".WriteVariableOpaque(" + opaqueValueExpression + maximumLengthArgument + ");");
                        }

                        break;

                    case CodecValueKind.String:
                    {
                        string stringValueExpression = "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\")";
                        string maximumStringArgument = property.Declarator.Kind == XdrDeclaratorKind.VariableArray
                            ? ", maximumUtf8ByteLength: " + ResolveBoundArgumentLiteral(property.Declarator.BoundExpression)
                            : string.Empty;
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteString(" + stringValueExpression + maximumStringArgument + ");");
                        break;
                    }

                    case CodecValueKind.Enum:
                        writer.AppendLine(indentLevel, EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Write(" + writerIdentifier + ", " + valueExpression + ");");
                        break;

                    case CodecValueKind.Class:
                        writer.AppendLine(
                            indentLevel,
                            "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\").WriteTo(" + writerIdentifier + ");");
                        break;

                    default:
                        throw new InvalidDataException("Unsupported XDR codec kind for scalar write: " + property.ValueDescriptor.Kind);
                }
            }

            private void EmitReadMember(
                CodeWriter writer,
                string targetAccess,
                PropertyDefinition property,
                string readerIdentifier,
                int indentLevel)
            {
                if (property.Declarator.Kind == XdrDeclaratorKind.Pointer)
                {
                    writer.AppendLine(indentLevel, "if (" + readerIdentifier + ".ReadBoolean())");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, targetAccess + " = " + BuildReadScalarExpression(property, readerIdentifier) + ";");
                    writer.AppendLine(indentLevel, "}");
                    writer.AppendLine(indentLevel, "else");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, targetAccess + " = null;");
                    writer.AppendLine(indentLevel, "}");
                    return;
                }

                if ((property.Declarator.Kind == XdrDeclaratorKind.FixedArray || property.Declarator.Kind == XdrDeclaratorKind.VariableArray)
                    && property.ValueDescriptor.Kind != CodecValueKind.Opaque
                    && property.ValueDescriptor.Kind != CodecValueKind.String)
                {
                    string arrayMethodName = property.Declarator.Kind == XdrDeclaratorKind.FixedArray ? "ReadFixedArray" : "ReadVariableArray";
                    string arrayCountArgument = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                        ? "expectedCount: " + ResolveFixedCountLiteral(property.Declarator.BoundExpression!)
                        : "maximumCount: " + ResolveBoundArgumentLiteral(property.Declarator.BoundExpression);

                    writer.AppendLine(indentLevel, targetAccess + " = XdrGeneratedCodec.ToArray(");
                    writer.AppendLine(indentLevel + 1, readerIdentifier + "." + arrayMethodName + "<" + property.ValueDescriptor.TypeName + ">(");
                    writer.AppendLine(indentLevel + 2, arrayCountArgument + ",");
                    writer.AppendLine(indentLevel + 2, "readElement: static xdrReader =>");
                    writer.AppendLine(indentLevel + 2, "{");
                    writer.AppendLine(indentLevel + 3, "return " + BuildReadArrayElementExpression(property, "xdrReader") + ";");
                    writer.AppendLine(indentLevel + 2, "}));");
                    return;
                }

                writer.AppendLine(indentLevel, targetAccess + " = " + BuildReadScalarExpression(property, readerIdentifier) + ";");
            }

            private string BuildReadArrayElementExpression(PropertyDefinition property, string readerIdentifier)
            {
                return property.ValueDescriptor.Kind switch
                {
                    CodecValueKind.Bool => readerIdentifier + ".ReadBoolean()",
                    CodecValueKind.Int32 => readerIdentifier + ".ReadInt32()",
                    CodecValueKind.UInt32 => readerIdentifier + ".ReadUInt32()",
                    CodecValueKind.Int64 => readerIdentifier + ".ReadInt64()",
                    CodecValueKind.UInt64 => readerIdentifier + ".ReadUInt64()",
                    CodecValueKind.Float => readerIdentifier + ".ReadSingle()",
                    CodecValueKind.Double => readerIdentifier + ".ReadDouble()",
                    CodecValueKind.Enum => EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Read(" + readerIdentifier + ")",
                    CodecValueKind.Class => property.ValueDescriptor.TypeName + ".ReadFrom(" + readerIdentifier + ")",
                    CodecValueKind.String => readerIdentifier + ".ReadString()",
                    CodecValueKind.Opaque => readerIdentifier + ".ReadVariableOpaque()",
                    _ => throw new InvalidDataException("Unsupported array element codec kind for read: " + property.ValueDescriptor.Kind),
                };
            }

            private string BuildInt32WriteExpression(PropertyDefinition property, string valueExpression)
            {
                if (string.Equals(property.ValueDescriptor.TypeName, "long", StringComparison.Ordinal))
                {
                    return "checked((int)(" + valueExpression + "))";
                }

                return valueExpression;
            }

            private string BuildReadScalarExpression(PropertyDefinition property, string readerIdentifier)
            {
                return property.ValueDescriptor.Kind switch
                {
                    CodecValueKind.Bool => readerIdentifier + ".ReadBoolean()",
                    CodecValueKind.Int32 => readerIdentifier + ".ReadInt32()",
                    CodecValueKind.UInt32 => readerIdentifier + ".ReadUInt32()",
                    CodecValueKind.Int64 => readerIdentifier + ".ReadInt64()",
                    CodecValueKind.UInt64 => readerIdentifier + ".ReadUInt64()",
                    CodecValueKind.Float => readerIdentifier + ".ReadSingle()",
                    CodecValueKind.Double => readerIdentifier + ".ReadDouble()",
                    CodecValueKind.Opaque when property.Declarator.Kind == XdrDeclaratorKind.FixedArray => readerIdentifier + ".ReadFixedOpaque(" + ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ")",
                    CodecValueKind.Opaque => readerIdentifier + ".ReadVariableOpaque(" + BuildMaximumArgumentLiteral(property.Declarator.BoundExpression) + ")",
                    CodecValueKind.String => readerIdentifier + ".ReadString(" + BuildMaximumArgumentLiteral(property.Declarator.BoundExpression) + ")",
                    CodecValueKind.Enum => EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Read(" + readerIdentifier + ")",
                    CodecValueKind.Class => property.ValueDescriptor.TypeName + ".ReadFrom(" + readerIdentifier + ")",
                    _ => throw new InvalidDataException("Unsupported XDR codec kind for scalar read: " + property.ValueDescriptor.Kind),
                };
            }

            private string ResolveUnionCaseLabel(PropertyDefinition discriminantProperty, string caseLabel)
            {
                if (discriminantProperty.ValueDescriptor.Kind == CodecValueKind.Bool)
                {
                    if (string.Equals(caseLabel, "TRUE", StringComparison.OrdinalIgnoreCase))
                    {
                        return "true";
                    }

                    if (string.Equals(caseLabel, "FALSE", StringComparison.OrdinalIgnoreCase))
                    {
                        return "false";
                    }

                    ulong caseValue = ResolveExpressionValue(caseLabel);
                    return caseValue == 0 ? "false" : "true";
                }

                if (discriminantProperty.ValueDescriptor.Kind == CodecValueKind.Enum)
                {
                    string trimmedCaseLabel = caseLabel.Trim();
                    if (TryParseUnsignedIntegerLiteral(trimmedCaseLabel, out _)
                        || numericSymbols.ContainsKey(trimmedCaseLabel))
                    {
                        return "(" + discriminantProperty.ValueDescriptor.TypeName + ")" + ResolveExpressionLiteral(trimmedCaseLabel);
                    }

                    return discriminantProperty.ValueDescriptor.TypeName + "." + EscapeIdentifier(trimmedCaseLabel);
                }

                return ResolveExpressionLiteral(caseLabel);
            }

            private string ResolveBoundArgumentLiteral(string? boundExpression)
            {
                return boundExpression is null ? "null" : ResolveExpressionLiteral(boundExpression);
            }

            private string BuildMaximumArgumentLiteral(string? boundExpression)
            {
                return boundExpression is null ? string.Empty : ResolveExpressionLiteral(boundExpression);
            }

            private string ResolveFixedCountLiteral(string boundExpression)
            {
                ulong value = ResolveExpressionValue(boundExpression);
                if (value > int.MaxValue)
                {
                    throw new InvalidDataException("The fixed-length XDR bound '" + boundExpression + "' exceeds the supported CLR array length limit.");
                }

                return value.ToString(CultureInfo.InvariantCulture);
            }

            private ulong ResolveExpressionValue(string expression)
            {
                return ResolveExpressionValue(expression, new HashSet<string>(StringComparer.Ordinal));
            }

            private ulong ResolveExpressionValue(string expression, ISet<string> stack)
            {
                string trimmedExpression = expression.Trim();
                if (numericSymbols.TryGetValue(trimmedExpression, out string? symbolExpression))
                {
                    if (!stack.Add(trimmedExpression))
                    {
                        throw new InvalidDataException("The XDR numeric symbol '" + trimmedExpression + "' contains a circular reference.");
                    }

                    ulong value = ResolveExpressionValue(symbolExpression, stack);
                    stack.Remove(trimmedExpression);
                    return value;
                }

                if (TryParseUnsignedIntegerLiteral(trimmedExpression, out ulong literalValue))
                {
                    return literalValue;
                }

                if (TryResolveExternalNumericSymbol(trimmedExpression, out ulong externalValue))
                {
                    return externalValue;
                }

                throw new InvalidDataException("Unsupported XDR numeric expression for codec emission: '" + trimmedExpression + "'.");
            }

            private CSharpTypeReference ResolveTypeReference(string ownerTypeName, XdrTypeSpecifier typeSpecifier, XdrDeclarator declarator)
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
                    EmitTypeFromSpecifier(anonymousTypeName, enumTypeSpecifier);
                    return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
                }

                if (typeSpecifier is XdrStructTypeSpecifier structTypeSpecifier)
                {
                    string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                    EmitTypeFromSpecifier(anonymousTypeName, structTypeSpecifier);
                    return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
                }

                if (typeSpecifier is XdrUnionTypeSpecifier unionTypeSpecifier)
                {
                    string anonymousTypeName = ownerTypeName + "_" + declarator.Identifier;
                    EmitTypeFromSpecifier(anonymousTypeName, unionTypeSpecifier);
                    return ApplyDeclarator(new CSharpTypeReference(anonymousTypeName, isValueType: false), declarator);
                }

                throw new InvalidDataException("Unsupported XDR field type specifier for emission: " + typeSpecifier.GetType().FullName);
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

            private string ResolveNumericLiteral(string symbolName)
            {
                if (resolvedNumericLiterals.TryGetValue(symbolName, out string? existingLiteral))
                {
                    return existingLiteral;
                }

                HashSet<string> stack = new HashSet<string>(StringComparer.Ordinal);
                string resolvedLiteral = ResolveNumericLiteral(symbolName, stack);
                resolvedNumericLiterals.Add(symbolName, resolvedLiteral);
                return resolvedLiteral;
            }

            private string ResolveNumericLiteral(string symbolName, ISet<string> stack)
            {
                if (!numericSymbols.TryGetValue(symbolName, out string? expression))
                {
                    throw new InvalidDataException("The XDR numeric symbol '" + symbolName + "' was not collected for emission.");
                }

                if (!stack.Add(symbolName))
                {
                    throw new InvalidDataException("The XDR numeric symbol '" + symbolName + "' contains a circular reference.");
                }

                string literal = ResolveExpressionLiteral(expression, stack);
                stack.Remove(symbolName);
                return literal;
            }

            private string ResolveExpressionLiteral(string expression)
            {
                return ResolveExpressionLiteral(expression, new HashSet<string>(StringComparer.Ordinal));
            }

            private string ResolveExpressionLiteral(string expression, ISet<string> stack)
            {
                string trimmedExpression = expression.Trim();

                if (numericSymbols.ContainsKey(trimmedExpression))
                {
                    if (resolvedNumericLiterals.TryGetValue(trimmedExpression, out string? existingLiteral))
                    {
                        return existingLiteral;
                    }

                    string resolvedLiteral = ResolveNumericLiteral(trimmedExpression, stack);
                    resolvedNumericLiterals[trimmedExpression] = resolvedLiteral;
                    return resolvedLiteral;
                }

                if (TryParseUnsignedIntegerLiteral(trimmedExpression, out ulong value))
                {
                    return FormatUnsignedIntegerLiteral(value);
                }

                if (TryResolveExternalNumericSymbol(trimmedExpression, out ulong externalValue))
                {
                    return FormatUnsignedIntegerLiteral(externalValue);
                }

                throw new InvalidDataException("Unsupported XDR numeric expression for C# emission: '" + trimmedExpression + "'.");
            }

            private static bool TryParseUnsignedIntegerLiteral(string expression, out ulong value)
            {
                if (expression.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    return ulong.TryParse(
                        expression.Substring(2),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out value);
                }

                return ulong.TryParse(expression, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            }

            private static bool TryResolveExternalNumericSymbol(string expression, out ulong value)
            {
                switch (expression)
                {
                    case "AUTH_NONE":
                        value = 0;
                        return true;

                    case "AUTH_SYS":
                        value = 1;
                        return true;

                    case "AUTH_SHORT":
                        value = 2;
                        return true;

                    case "AUTH_DH":
                        value = 3;
                        return true;

                    case "RPCSEC_GSS":
                        value = 6;
                        return true;

                    default:
                        value = 0;
                        return false;
                }
            }

            private static string FormatUnsignedIntegerLiteral(ulong value)
            {
                if (value <= int.MaxValue)
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }

                if (value <= uint.MaxValue)
                {
                    return value.ToString(CultureInfo.InvariantCulture) + "U";
                }

                if (value <= long.MaxValue)
                {
                    return value.ToString(CultureInfo.InvariantCulture) + "L";
                }

                return value.ToString(CultureInfo.InvariantCulture) + "UL";
            }

            private void AddGeneratedFile(string typeName, string sourceText)
            {
                AddGeneratedFile(typeName, typeName + ".g.cs", sourceText);
            }

            private void AddGeneratedFile(string artifactKey, string fileName, string sourceText)
            {
                string filePath = Path.Combine(mapping.GeneratedDirectoryPath, fileName);
                if (generatedFilesByTypeName.TryGetValue(artifactKey, out GeneratedCSharpFile? existingFile))
                {
                    if (string.Equals(existingFile.SourceText, sourceText, StringComparison.Ordinal))
                    {
                        return;
                    }

                    throw new InvalidDataException("Duplicate generated C# artifact encountered with conflicting output: " + artifactKey);
                }

                GeneratedCSharpFile generatedFile = new GeneratedCSharpFile(filePath, artifactKey, sourceText);
                generatedFilesByTypeName.Add(artifactKey, generatedFile);
                generatedFiles.Add(generatedFile);
            }

            private static bool IsAnonymousTypeSpecifier(XdrTypeSpecifier typeSpecifier)
            {
                return typeSpecifier switch
                {
                    XdrEnumTypeSpecifier enumTypeSpecifier => string.IsNullOrWhiteSpace(enumTypeSpecifier.Name),
                    XdrStructTypeSpecifier structTypeSpecifier => string.IsNullOrWhiteSpace(structTypeSpecifier.Name),
                    XdrUnionTypeSpecifier unionTypeSpecifier => string.IsNullOrWhiteSpace(unionTypeSpecifier.Name),
                    _ => false,
                };
            }

            private static string GetUniquePropertyName(string ownerTypeName, string baseName, ISet<string> usedNames)
            {
                string candidate = baseName;
                int suffix = 2;
                while (!IsSafePropertyName(ownerTypeName, candidate) || !usedNames.Add(candidate))
                {
                    candidate = baseName + "_value";
                    if (suffix > 2)
                    {
                        candidate += "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    }

                    suffix++;
                }

                return candidate;
            }

            private static bool IsSafePropertyName(string ownerTypeName, string propertyName)
            {
                if (string.Equals(ownerTypeName, propertyName, StringComparison.Ordinal))
                {
                    return false;
                }

                if (string.Equals(ownerTypeName, "get_" + propertyName, StringComparison.Ordinal))
                {
                    return false;
                }

                if (string.Equals(ownerTypeName, "set_" + propertyName, StringComparison.Ordinal))
                {
                    return false;
                }

                return true;
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
                if (!string.Equals(mapping.OutputNamespace, "OpenNFS.Rpc.Generated", StringComparison.Ordinal)
                    && string.Equals(identifier, "authsys_parms", StringComparison.Ordinal))
                {
                    externalType = new CSharpTypeReference("OpenNFS.Rpc.Generated.authsys_parms", isValueType: false);
                    return true;
                }

                externalType = null!;
                return false;
            }

            private static string EscapeIdentifier(string identifier)
            {
                if (CSharpKeywords.Contains(identifier))
                {
                    return "@" + identifier;
                }

                return identifier;
            }

            private static string EscapeXml(string value)
            {
                return value
                    .Replace("&", "&amp;", StringComparison.Ordinal)
                    .Replace("<", "&lt;", StringComparison.Ordinal)
                    .Replace(">", "&gt;", StringComparison.Ordinal);
            }

            private static string CreatePascalCase(string value)
            {
                string[] parts = value
                    .Split(new[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder builder = new StringBuilder();
                foreach (string part in parts)
                {
                    if (part.Length == 0)
                    {
                        continue;
                    }

                    builder.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                    {
                        builder.Append(part.Substring(1));
                    }
                }

                return builder.Length == 0 ? "Xdr" : builder.ToString();
            }

            private sealed class PropertyDefinition
            {
                public PropertyDefinition(
                    string name,
                    string wireName,
                    CSharpTypeReference typeReference,
                    CodecValueDescriptor valueDescriptor,
                    XdrDeclarator declarator,
                    string summary)
                {
                    Name = name;
                    WireName = wireName;
                    TypeReference = typeReference;
                    ValueDescriptor = valueDescriptor;
                    Declarator = declarator;
                    Summary = summary;
                }

                public string Name { get; }

                public string WireName { get; }

                public CSharpTypeReference TypeReference { get; }

                public CodecValueDescriptor ValueDescriptor { get; }

                public XdrDeclarator Declarator { get; }

                public string TypeName => TypeReference.DisplayName;

                public string Summary { get; }
            }

            private sealed class UnionArmDefinition
            {
                public UnionArmDefinition(PropertyDefinition property, IReadOnlyList<string> caseLabels, bool isDefault)
                {
                    Property = property;
                    CaseLabels = caseLabels;
                    IsDefault = isDefault;
                }

                public PropertyDefinition Property { get; }

                public IReadOnlyList<string> CaseLabels { get; }

                public bool IsDefault { get; }
            }

            private sealed class CodecValueDescriptor
            {
                public CodecValueDescriptor(CodecValueKind kind, string typeName, bool isValueType)
                {
                    Kind = kind;
                    TypeName = typeName;
                    IsValueType = isValueType;
                }

                public CodecValueKind Kind { get; }

                public string TypeName { get; }

                public bool IsValueType { get; }
            }

            private enum CodecValueKind
            {
                Bool,
                Int32,
                UInt32,
                Int64,
                UInt64,
                Float,
                Double,
                Opaque,
                String,
                Enum,
                Class,
            }

            private enum EmittedTypeKind
            {
                Enum,
                Class,
            }

            private sealed class CSharpTypeReference
            {
                public CSharpTypeReference(string baseTypeName, bool isValueType, bool isNullable = false)
                {
                    BaseTypeName = baseTypeName;
                    IsValueType = isValueType;
                    IsNullable = isNullable;
                }

                public string BaseTypeName { get; }

                public bool IsValueType { get; }

                public bool IsNullable { get; }

                public string DisplayName
                {
                    get
                    {
                        if (!IsNullable)
                        {
                            return BaseTypeName;
                        }

                        return BaseTypeName + "?";
                    }
                }

                public string NonNullableDisplayName => BaseTypeName;

                public CSharpTypeReference DefaultForProperty()
                {
                    if (IsValueType)
                    {
                        return this;
                    }

                    return new CSharpTypeReference(BaseTypeName, isValueType: false, isNullable: true);
                }

                public CSharpTypeReference MakeNullable()
                {
                    if (IsNullable)
                    {
                        return this;
                    }

                    return new CSharpTypeReference(BaseTypeName, IsValueType, isNullable: true);
                }
            }

            private sealed class CodeWriter
            {
                private readonly StringBuilder builder;

                public CodeWriter()
                {
                    builder = new StringBuilder();
                }

                public void AppendLine()
                {
                    builder.Append('\n');
                }

                public void AppendLine(string line)
                {
                    builder.Append(line);
                    builder.Append('\n');
                }

                public void AppendLine(int indentLevel, string line)
                {
                    builder.Append(' ', indentLevel * 4);
                    builder.Append(line);
                    builder.Append('\n');
                }

                public void RemoveTrailingBlankLine()
                {
                    string value = builder.ToString();
                    while (value.EndsWith("\n\n", StringComparison.Ordinal))
                    {
                        value = value.Substring(0, value.Length - 1);
                    }

                    builder.Clear();
                    builder.Append(value);
                }

                public override string ToString()
                {
                    return builder.ToString();
                }
            }
        }
    }
}
