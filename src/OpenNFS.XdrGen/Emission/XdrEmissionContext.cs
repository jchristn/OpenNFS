namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrEmissionContext
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

        internal XdrEmissionContext(XdrProjectMapping mapping, IReadOnlyList<XdrDocument> documents)
        {
            this.mapping = mapping;
            this.documents = documents;
            numericSymbols = new Dictionary<string, string>(StringComparer.Ordinal);
            declaredTypeKinds = new Dictionary<string, EmittedTypeKind>(StringComparer.Ordinal);
            resolvedNumericLiterals = new Dictionary<string, string>(StringComparer.Ordinal);
            generatedFilesByTypeName = new Dictionary<string, GeneratedCSharpFile>(StringComparer.Ordinal);
            generatedFiles = new List<GeneratedCSharpFile>();
            constantsTypeName = CreatePascalCase(mapping.Id) + "Constants";
            Symbols = new XdrEmissionSymbolSupport(this);
            Codecs = new XdrEmissionCodecSupport(this);
            Types = new XdrEmissionTypeSupport(this);
        }

        internal XdrProjectMapping Mapping => mapping;

        internal IReadOnlyList<XdrDocument> Documents => documents;

        internal Dictionary<string, string> NumericSymbols => numericSymbols;

        internal Dictionary<string, EmittedTypeKind> DeclaredTypeKinds => declaredTypeKinds;

        internal Dictionary<string, string> ResolvedNumericLiterals => resolvedNumericLiterals;

        internal Dictionary<string, GeneratedCSharpFile> GeneratedFilesByTypeName => generatedFilesByTypeName;

        internal string ConstantsTypeName => constantsTypeName;

        internal XdrEmissionSymbolSupport Symbols { get; }

        internal XdrEmissionCodecSupport Codecs { get; }

        internal XdrEmissionTypeSupport Types { get; }

        public IReadOnlyList<GeneratedCSharpFile> GeneratedFiles => generatedFiles;

        public void Emit()
        {
            Symbols.CollectDeclaredTypeKinds();
            Symbols.CollectNumericSymbols();
            Types.EmitConstantsClass();

            foreach (XdrDocument document in documents)
            {
                foreach (XdrDefinition definition in document.Definitions)
                {
                    switch (definition)
                    {
                        case XdrConstDefinition:
                            break;

                        case XdrTypedefDefinition typedefDefinition:
                            Types.EmitTypedefDefinition(typedefDefinition);
                            break;

                        case XdrTypeDefinition typeDefinition:
                            Types.EmitTopLevelTypeDefinition(typeDefinition);
                            break;

                        case XdrProgramDefinition programDefinition:
                            Types.EmitProgramDefinition(programDefinition);
                            break;

                        default:
                            throw new InvalidDataException("Unsupported XDR definition type for emission: " + definition.GetType().FullName);
                    }
                }
            }
        }

        internal void AddGeneratedFile(string typeName, string sourceText)
        {
            AddGeneratedFile(typeName, typeName + ".g.cs", sourceText);
        }

        internal void AddGeneratedFile(string artifactKey, string fileName, string sourceText)
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

        internal static bool IsAnonymousTypeSpecifier(XdrTypeSpecifier typeSpecifier)
        {
            return typeSpecifier switch
            {
                XdrEnumTypeSpecifier enumTypeSpecifier => string.IsNullOrWhiteSpace(enumTypeSpecifier.Name),
                XdrStructTypeSpecifier structTypeSpecifier => string.IsNullOrWhiteSpace(structTypeSpecifier.Name),
                XdrUnionTypeSpecifier unionTypeSpecifier => string.IsNullOrWhiteSpace(unionTypeSpecifier.Name),
                _ => false,
            };
        }

        internal static string GetUniquePropertyName(string ownerTypeName, string baseName, ISet<string> usedNames)
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

        internal static string EscapeIdentifier(string identifier)
        {
            if (CSharpKeywords.Contains(identifier))
            {
                return "@" + identifier;
            }

            return identifier;
        }

        internal static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        internal static string CreatePascalCase(string value)
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
    }
}
