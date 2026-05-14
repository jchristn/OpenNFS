namespace OpenNFS.XdrGen.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrParserEngine
    {
        private static readonly HashSet<string> BuiltinTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "bool",
            "double",
            "float",
            "hyper",
            "int",
            "opaque",
            "quadruple",
            "string",
            "void",
        };

        private readonly XdrParserState parserState;

        internal XdrParserEngine(string filePath, IReadOnlyList<XdrToken> tokens)
        {
            parserState = new XdrParserState(filePath, tokens);
        }

        internal XdrDocument ParseDocument()
        {
            List<XdrDefinition> definitions = new List<XdrDefinition>();
            while (!parserState.IsAtEnd)
            {
                definitions.Add(ParseDefinition());
            }

            return new XdrDocument(Path.GetFullPath(parserState.FilePath), definitions);
        }

        private XdrDefinition ParseDefinition()
        {
            if (parserState.MatchIdentifier("const"))
            {
                string name = parserState.ExpectIdentifier("Expected a constant identifier.").Text;
                parserState.ExpectSymbol("=", "Expected '=' after constant identifier.");
                string valueExpression = parserState.CollectUntilSymbol(";");
                parserState.ExpectSymbol(";", "Expected ';' after constant definition.");
                return new XdrConstDefinition(name, valueExpression);
            }

            if (parserState.MatchIdentifier("typedef"))
            {
                XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier();
                XdrDeclarator declarator = ParseDeclarator();
                parserState.ExpectSymbol(";", "Expected ';' after typedef.");
                return new XdrTypedefDefinition(typeSpecifier, declarator);
            }

            if (parserState.IsIdentifier("enum") || parserState.IsIdentifier("struct") || parserState.IsIdentifier("union"))
            {
                XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier();
                if (typeSpecifier is not XdrEnumTypeSpecifier
                    && typeSpecifier is not XdrStructTypeSpecifier
                    && typeSpecifier is not XdrUnionTypeSpecifier)
                {
                    parserState.Throw("Expected a type definition.");
                }

                parserState.ExpectSymbol(";", "Expected ';' after type definition.");
                return new XdrTypeDefinition(typeSpecifier);
            }

            if (parserState.MatchIdentifier("program"))
            {
                string name = parserState.ExpectIdentifier("Expected a program identifier.").Text;
                parserState.ExpectSymbol("{", "Expected '{' after program identifier.");

                List<XdrVersionDefinition> versions = new List<XdrVersionDefinition>();
                while (!parserState.MatchSymbol("}"))
                {
                    parserState.ExpectIdentifier("version", "Expected a version declaration inside program.");
                    versions.Add(ParseVersionDefinition());
                }

                parserState.ExpectSymbol("=", "Expected '=' after program body.");
                string valueExpression = parserState.CollectUntilSymbol(";");
                parserState.ExpectSymbol(";", "Expected ';' after program number.");

                return new XdrProgramDefinition(name, versions, valueExpression);
            }

            parserState.Throw("Unsupported top-level declaration.");
            return null!;
        }

        private XdrVersionDefinition ParseVersionDefinition()
        {
            string name = parserState.ExpectIdentifier("Expected a version identifier.").Text;
            parserState.ExpectSymbol("{", "Expected '{' after version identifier.");

            List<XdrProcedureDefinition> procedures = new List<XdrProcedureDefinition>();
            while (!parserState.MatchSymbol("}"))
            {
                procedures.Add(ParseProcedureDefinition());
            }

            parserState.ExpectSymbol("=", "Expected '=' after version body.");
            string valueExpression = parserState.CollectUntilSymbol(";");
            parserState.ExpectSymbol(";", "Expected ';' after version number.");

            return new XdrVersionDefinition(name, procedures, valueExpression);
        }

        private XdrProcedureDefinition ParseProcedureDefinition()
        {
            XdrTypeSpecifier returnType = ParseProcedureTypeSpecifier();
            string name = parserState.ExpectIdentifier("Expected a procedure identifier.").Text;
            parserState.ExpectSymbol("(", "Expected '(' after procedure identifier.");

            XdrTypeSpecifier argumentType = ParseProcedureTypeSpecifier();

            parserState.ExpectSymbol(")", "Expected ')' after procedure argument type.");
            parserState.ExpectSymbol("=", "Expected '=' after procedure signature.");
            string valueExpression = parserState.CollectUntilSymbol(";");
            parserState.ExpectSymbol(";", "Expected ';' after procedure number.");

            return new XdrProcedureDefinition(returnType, name, argumentType, valueExpression);
        }

        private XdrTypeSpecifier ParseProcedureTypeSpecifier()
        {
            if (parserState.IsIdentifier("void") && parserState.PeekNextSymbol(")"))
            {
                parserState.Advance();
                return new XdrBuiltinTypeSpecifier("void");
            }

            XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier();
            if (typeSpecifier is XdrEnumTypeSpecifier or XdrStructTypeSpecifier or XdrUnionTypeSpecifier)
            {
                parserState.Throw("Inline type definitions are not valid in procedure signatures.");
            }

            return typeSpecifier;
        }

        private XdrFieldDeclaration ParseFieldDeclaration()
        {
            XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier();
            XdrDeclarator declarator = ParseDeclarator();
            parserState.ExpectSymbol(";", "Expected ';' after field declaration.");
            return new XdrFieldDeclaration(typeSpecifier, declarator);
        }

        private XdrFieldDeclaration? ParseUnionArmDeclaration()
        {
            if (parserState.IsIdentifier("void") && parserState.PeekNextSymbol(";"))
            {
                parserState.Advance();
                parserState.ExpectSymbol(";", "Expected ';' after void union arm.");
                return null;
            }

            return ParseFieldDeclaration();
        }

        private XdrDeclarator ParseDeclarator()
        {
            bool isPointer = parserState.MatchSymbol("*");
            string identifier = parserState.ExpectIdentifier("Expected a declarator identifier.").Text;

            if (parserState.MatchSymbol("["))
            {
                string boundExpression = parserState.CollectUntilSymbol("]");
                parserState.ExpectSymbol("]", "Expected ']' after fixed-array bound.");
                return new XdrDeclarator(identifier, XdrDeclaratorKind.FixedArray, boundExpression);
            }

            if (parserState.MatchSymbol("<"))
            {
                string? boundExpression = parserState.IsSymbol(">")
                    ? null
                    : parserState.CollectUntilSymbol(">");
                parserState.ExpectSymbol(">", "Expected '>' after variable-array bound.");
                return new XdrDeclarator(identifier, XdrDeclaratorKind.VariableArray, boundExpression);
            }

            return new XdrDeclarator(identifier, isPointer ? XdrDeclaratorKind.Pointer : XdrDeclaratorKind.Identifier, null);
        }

        private XdrTypeSpecifier ParseTypeSpecifier()
        {
            XdrToken current = parserState.Current;
            if (current.Kind != XdrTokenKind.Identifier)
            {
                parserState.Throw("Expected a type specifier.");
            }

            if (string.Equals(current.Text, "unsigned", StringComparison.Ordinal))
            {
                parserState.Advance();
                if (parserState.Current.Kind == XdrTokenKind.Identifier
                    && (string.Equals(parserState.Current.Text, "int", StringComparison.Ordinal)
                        || string.Equals(parserState.Current.Text, "hyper", StringComparison.Ordinal)
                        || string.Equals(parserState.Current.Text, "long", StringComparison.Ordinal)))
                {
                    string composite = "unsigned " + parserState.Current.Text;
                    parserState.Advance();
                    return new XdrBuiltinTypeSpecifier(composite);
                }

                return new XdrBuiltinTypeSpecifier("unsigned");
            }

            if (BuiltinTypeNames.Contains(current.Text))
            {
                parserState.Advance();
                return new XdrBuiltinTypeSpecifier(current.Text);
            }

            if (string.Equals(current.Text, "enum", StringComparison.Ordinal))
            {
                return ParseEnumTypeSpecifier();
            }

            if (string.Equals(current.Text, "struct", StringComparison.Ordinal))
            {
                return ParseStructTypeSpecifier();
            }

            if (string.Equals(current.Text, "union", StringComparison.Ordinal))
            {
                return ParseUnionTypeSpecifier();
            }

            parserState.Advance();
            return new XdrIdentifierTypeSpecifier(current.Text);
        }

        private XdrTypeSpecifier ParseEnumTypeSpecifier()
        {
            parserState.ExpectIdentifier("enum", "Expected 'enum'.");
            string? name = parserState.MatchIdentifierToken();

            if (!parserState.MatchSymbol("{"))
            {
                if (name is null)
                {
                    parserState.Throw("Expected an enum identifier or definition body.");
                }

                return new XdrQualifiedTypeSpecifier("enum", name!);
            }

            List<XdrEnumMember> members = new List<XdrEnumMember>();
            while (!parserState.MatchSymbol("}"))
            {
                string memberName = parserState.ExpectIdentifier("Expected an enum member identifier.").Text;
                parserState.ExpectSymbol("=", "Expected '=' after enum member identifier.");
                string valueExpression = parserState.CollectUntilSymbol(",", "}");
                members.Add(new XdrEnumMember(memberName, valueExpression));

                if (!parserState.MatchSymbol(","))
                {
                    parserState.ExpectSymbol("}", "Expected '}' after enum member.");
                    break;
                }
            }

            return new XdrEnumTypeSpecifier(name, members);
        }

        private XdrTypeSpecifier ParseStructTypeSpecifier()
        {
            parserState.ExpectIdentifier("struct", "Expected 'struct'.");
            string? name = parserState.MatchIdentifierToken();

            if (!parserState.MatchSymbol("{"))
            {
                if (name is null)
                {
                    parserState.Throw("Expected a struct identifier or definition body.");
                }

                return new XdrQualifiedTypeSpecifier("struct", name!);
            }

            List<XdrFieldDeclaration> fields = new List<XdrFieldDeclaration>();
            while (!parserState.MatchSymbol("}"))
            {
                fields.Add(ParseFieldDeclaration());
            }

            return new XdrStructTypeSpecifier(name, fields);
        }

        private XdrTypeSpecifier ParseUnionTypeSpecifier()
        {
            parserState.ExpectIdentifier("union", "Expected 'union'.");

            string? name = null;
            if (parserState.Current.Kind == XdrTokenKind.Identifier && !parserState.IsIdentifier("switch"))
            {
                name = parserState.Current.Text;
                parserState.Advance();
            }

            if (!parserState.MatchIdentifier("switch"))
            {
                if (name is null)
                {
                    parserState.Throw("Expected a union identifier or 'switch'.");
                }

                return new XdrQualifiedTypeSpecifier("union", name!);
            }

            parserState.ExpectSymbol("(", "Expected '(' after 'switch'.");
            XdrTypeSpecifier switchType = ParseTypeSpecifier();
            string switchName = parserState.ExpectIdentifier("Expected a discriminant identifier.").Text;
            parserState.ExpectSymbol(")", "Expected ')' after union discriminant.");
            parserState.ExpectSymbol("{", "Expected '{' before union arms.");

            List<XdrUnionArm> arms = new List<XdrUnionArm>();
            while (!parserState.MatchSymbol("}"))
            {
                List<string> labels = new List<string>();
                bool isDefault = false;

                while (parserState.MatchIdentifier("case"))
                {
                    labels.Add(parserState.CollectUntilSymbol(":"));
                    parserState.ExpectSymbol(":", "Expected ':' after case label.");
                }

                if (parserState.MatchIdentifier("default"))
                {
                    isDefault = true;
                    parserState.ExpectSymbol(":", "Expected ':' after default label.");
                }

                if (labels.Count == 0 && !isDefault)
                {
                    parserState.Throw("Expected 'case' or 'default' inside union body.");
                }

                XdrFieldDeclaration? declaration = ParseUnionArmDeclaration();
                arms.Add(new XdrUnionArm(labels, isDefault, declaration));
            }

            return new XdrUnionTypeSpecifier(name, switchType, switchName, arms);
        }
    }
}
