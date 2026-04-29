namespace OpenNFS.XdrGen.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using OpenNFS.XdrGen.Model;

    /// <summary>
    /// Parses XDR source files into a stable AST.
    /// </summary>
    public sealed class XdrParser
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

        /// <summary>
        /// Parses the given source file into an AST.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        /// <param name="source">Raw source text.</param>
        /// <returns>The parsed document.</returns>
        public XdrDocument ParseDocument(string filePath, string source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(source);

            XdrTokenizer tokenizer = new XdrTokenizer();
            IReadOnlyList<XdrToken> tokens = tokenizer.Tokenize(filePath, source);
            ParserState parserState = new ParserState(filePath, tokens);

            List<XdrDefinition> definitions = new List<XdrDefinition>();
            while (!parserState.IsAtEnd)
            {
                definitions.Add(ParseDefinition(parserState));
            }

            return new XdrDocument(Path.GetFullPath(filePath), definitions);
        }

        private static XdrDefinition ParseDefinition(ParserState parserState)
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
                XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier(parserState);
                XdrDeclarator declarator = ParseDeclarator(parserState);
                parserState.ExpectSymbol(";", "Expected ';' after typedef.");
                return new XdrTypedefDefinition(typeSpecifier, declarator);
            }

            if (parserState.IsIdentifier("enum") || parserState.IsIdentifier("struct") || parserState.IsIdentifier("union"))
            {
                XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier(parserState);
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
                    versions.Add(ParseVersionDefinition(parserState));
                }

                parserState.ExpectSymbol("=", "Expected '=' after program body.");
                string valueExpression = parserState.CollectUntilSymbol(";");
                parserState.ExpectSymbol(";", "Expected ';' after program number.");

                return new XdrProgramDefinition(name, versions, valueExpression);
            }

            parserState.Throw("Unsupported top-level declaration.");
            return null!;
        }

        private static XdrVersionDefinition ParseVersionDefinition(ParserState parserState)
        {
            string name = parserState.ExpectIdentifier("Expected a version identifier.").Text;
            parserState.ExpectSymbol("{", "Expected '{' after version identifier.");

            List<XdrProcedureDefinition> procedures = new List<XdrProcedureDefinition>();
            while (!parserState.MatchSymbol("}"))
            {
                procedures.Add(ParseProcedureDefinition(parserState));
            }

            parserState.ExpectSymbol("=", "Expected '=' after version body.");
            string valueExpression = parserState.CollectUntilSymbol(";");
            parserState.ExpectSymbol(";", "Expected ';' after version number.");

            return new XdrVersionDefinition(name, procedures, valueExpression);
        }

        private static XdrProcedureDefinition ParseProcedureDefinition(ParserState parserState)
        {
            XdrTypeSpecifier returnType = ParseProcedureTypeSpecifier(parserState);
            string name = parserState.ExpectIdentifier("Expected a procedure identifier.").Text;
            parserState.ExpectSymbol("(", "Expected '(' after procedure identifier.");

            XdrTypeSpecifier argumentType = ParseProcedureTypeSpecifier(parserState);

            parserState.ExpectSymbol(")", "Expected ')' after procedure argument type.");
            parserState.ExpectSymbol("=", "Expected '=' after procedure signature.");
            string valueExpression = parserState.CollectUntilSymbol(";");
            parserState.ExpectSymbol(";", "Expected ';' after procedure number.");

            return new XdrProcedureDefinition(returnType, name, argumentType, valueExpression);
        }

        private static XdrTypeSpecifier ParseProcedureTypeSpecifier(ParserState parserState)
        {
            if (parserState.IsIdentifier("void") && parserState.PeekNextSymbol(")"))
            {
                parserState.Advance();
                return new XdrBuiltinTypeSpecifier("void");
            }

            XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier(parserState);
            if (typeSpecifier is XdrEnumTypeSpecifier or XdrStructTypeSpecifier or XdrUnionTypeSpecifier)
            {
                parserState.Throw("Inline type definitions are not valid in procedure signatures.");
            }

            return typeSpecifier;
        }

        private static XdrFieldDeclaration ParseFieldDeclaration(ParserState parserState)
        {
            XdrTypeSpecifier typeSpecifier = ParseTypeSpecifier(parserState);
            XdrDeclarator declarator = ParseDeclarator(parserState);
            parserState.ExpectSymbol(";", "Expected ';' after field declaration.");
            return new XdrFieldDeclaration(typeSpecifier, declarator);
        }

        private static XdrFieldDeclaration? ParseUnionArmDeclaration(ParserState parserState)
        {
            if (parserState.IsIdentifier("void") && parserState.PeekNextSymbol(";"))
            {
                parserState.Advance();
                parserState.ExpectSymbol(";", "Expected ';' after void union arm.");
                return null;
            }

            return ParseFieldDeclaration(parserState);
        }

        private static XdrDeclarator ParseDeclarator(ParserState parserState)
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

        private static XdrTypeSpecifier ParseTypeSpecifier(ParserState parserState)
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
                return ParseEnumTypeSpecifier(parserState);
            }

            if (string.Equals(current.Text, "struct", StringComparison.Ordinal))
            {
                return ParseStructTypeSpecifier(parserState);
            }

            if (string.Equals(current.Text, "union", StringComparison.Ordinal))
            {
                return ParseUnionTypeSpecifier(parserState);
            }

            parserState.Advance();
            return new XdrIdentifierTypeSpecifier(current.Text);
        }

        private static XdrTypeSpecifier ParseEnumTypeSpecifier(ParserState parserState)
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

        private static XdrTypeSpecifier ParseStructTypeSpecifier(ParserState parserState)
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
                fields.Add(ParseFieldDeclaration(parserState));
            }

            return new XdrStructTypeSpecifier(name, fields);
        }

        private static XdrTypeSpecifier ParseUnionTypeSpecifier(ParserState parserState)
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
            XdrTypeSpecifier switchType = ParseTypeSpecifier(parserState);
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

                XdrFieldDeclaration? declaration = ParseUnionArmDeclaration(parserState);
                arms.Add(new XdrUnionArm(labels, isDefault, declaration));
            }

            return new XdrUnionTypeSpecifier(name, switchType, switchName, arms);
        }

        private sealed class ParserState
        {
            private readonly IReadOnlyList<XdrToken> tokens;
            private int position;

            public ParserState(string filePath, IReadOnlyList<XdrToken> tokens)
            {
                FilePath = filePath;
                this.tokens = tokens;
            }

            public string FilePath { get; }

            public XdrToken Current => tokens[position];

            public bool IsAtEnd => Current.Kind == XdrTokenKind.EndOfFile;

            public void Advance()
            {
                if (!IsAtEnd)
                {
                    position++;
                }
            }

            public bool MatchIdentifier(string value)
            {
                if (IsIdentifier(value))
                {
                    position++;
                    return true;
                }

                return false;
            }

            public string? MatchIdentifierToken()
            {
                if (Current.Kind == XdrTokenKind.Identifier)
                {
                    string text = Current.Text;
                    position++;
                    return text;
                }

                return null;
            }

            public void ExpectIdentifier(string value, string message)
            {
                if (!MatchIdentifier(value))
                {
                    Throw(message);
                }
            }

            public XdrToken ExpectIdentifier(string message)
            {
                if (Current.Kind != XdrTokenKind.Identifier)
                {
                    Throw(message);
                }

                XdrToken token = Current;
                position++;
                return token;
            }

            public bool MatchSymbol(string value)
            {
                if (IsSymbol(value))
                {
                    position++;
                    return true;
                }

                return false;
            }

            public void ExpectSymbol(string value, string message)
            {
                if (!MatchSymbol(value))
                {
                    Throw(message);
                }
            }

            public bool PeekSymbol(string value)
            {
                return IsSymbol(value);
            }

            public bool PeekNextSymbol(string value)
            {
                if (position + 1 >= tokens.Count)
                {
                    return false;
                }

                XdrToken nextToken = tokens[position + 1];
                return nextToken.Kind == XdrTokenKind.Symbol && string.Equals(nextToken.Text, value, StringComparison.Ordinal);
            }

            public bool IsIdentifier(string value)
            {
                return Current.Kind == XdrTokenKind.Identifier && string.Equals(Current.Text, value, StringComparison.Ordinal);
            }

            public bool IsSymbol(string value)
            {
                return Current.Kind == XdrTokenKind.Symbol && string.Equals(Current.Text, value, StringComparison.Ordinal);
            }

            public string CollectUntilSymbol(params string[] terminators)
            {
                List<string> parts = new List<string>();
                int nestedAngleDepth = 0;
                int nestedBracketDepth = 0;
                int nestedParenthesisDepth = 0;

                while (!IsAtEnd)
                {
                    if (Current.Kind == XdrTokenKind.Symbol)
                    {
                        if (string.Equals(Current.Text, "<", StringComparison.Ordinal))
                        {
                            nestedAngleDepth++;
                        }
                        else if (string.Equals(Current.Text, ">", StringComparison.Ordinal))
                        {
                            if (nestedAngleDepth == 0 && terminators.Contains(">", StringComparer.Ordinal))
                            {
                                break;
                            }

                            nestedAngleDepth = Math.Max(0, nestedAngleDepth - 1);
                        }
                        else if (string.Equals(Current.Text, "[", StringComparison.Ordinal))
                        {
                            nestedBracketDepth++;
                        }
                        else if (string.Equals(Current.Text, "]", StringComparison.Ordinal))
                        {
                            if (nestedBracketDepth == 0 && terminators.Contains("]", StringComparer.Ordinal))
                            {
                                break;
                            }

                            nestedBracketDepth = Math.Max(0, nestedBracketDepth - 1);
                        }
                        else if (string.Equals(Current.Text, "(", StringComparison.Ordinal))
                        {
                            nestedParenthesisDepth++;
                        }
                        else if (string.Equals(Current.Text, ")", StringComparison.Ordinal))
                        {
                            if (nestedParenthesisDepth == 0 && terminators.Contains(")", StringComparer.Ordinal))
                            {
                                break;
                            }

                            nestedParenthesisDepth = Math.Max(0, nestedParenthesisDepth - 1);
                        }
                        else if (nestedAngleDepth == 0
                            && nestedBracketDepth == 0
                            && nestedParenthesisDepth == 0
                            && terminators.Contains(Current.Text, StringComparer.Ordinal))
                        {
                            break;
                        }
                    }

                    parts.Add(Current.Text);
                    position++;
                }

                if (parts.Count == 0)
                {
                    Throw("Expected an expression before '" + string.Join("' or '", terminators) + "'.");
                }

                return string.Concat(parts);
            }

            public void Throw(string message)
            {
                throw new XdrParseException(FilePath, Current.Line, Current.Column, message);
            }
        }
    }
}
