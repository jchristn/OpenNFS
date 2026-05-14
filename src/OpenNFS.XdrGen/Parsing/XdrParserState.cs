namespace OpenNFS.XdrGen.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class XdrParserState
    {
        private readonly IReadOnlyList<XdrToken> tokens;
        private int position;

        internal XdrParserState(string filePath, IReadOnlyList<XdrToken> tokens)
        {
            FilePath = filePath;
            this.tokens = tokens;
        }

        internal string FilePath { get; }

        internal XdrToken Current => tokens[position];

        internal bool IsAtEnd => Current.Kind == XdrTokenKind.EndOfFile;

        internal void Advance()
        {
            if (!IsAtEnd)
            {
                position++;
            }
        }

        internal bool MatchIdentifier(string value)
        {
            if (IsIdentifier(value))
            {
                position++;
                return true;
            }

            return false;
        }

        internal string? MatchIdentifierToken()
        {
            if (Current.Kind == XdrTokenKind.Identifier)
            {
                string text = Current.Text;
                position++;
                return text;
            }

            return null;
        }

        internal void ExpectIdentifier(string value, string message)
        {
            if (!MatchIdentifier(value))
            {
                Throw(message);
            }
        }

        internal XdrToken ExpectIdentifier(string message)
        {
            if (Current.Kind != XdrTokenKind.Identifier)
            {
                Throw(message);
            }

            XdrToken token = Current;
            position++;
            return token;
        }

        internal bool MatchSymbol(string value)
        {
            if (IsSymbol(value))
            {
                position++;
                return true;
            }

            return false;
        }

        internal void ExpectSymbol(string value, string message)
        {
            if (!MatchSymbol(value))
            {
                Throw(message);
            }
        }

        internal bool PeekNextSymbol(string value)
        {
            if (position + 1 >= tokens.Count)
            {
                return false;
            }

            XdrToken nextToken = tokens[position + 1];
            return nextToken.Kind == XdrTokenKind.Symbol && string.Equals(nextToken.Text, value, StringComparison.Ordinal);
        }

        internal bool IsIdentifier(string value)
        {
            return Current.Kind == XdrTokenKind.Identifier && string.Equals(Current.Text, value, StringComparison.Ordinal);
        }

        internal bool IsSymbol(string value)
        {
            return Current.Kind == XdrTokenKind.Symbol && string.Equals(Current.Text, value, StringComparison.Ordinal);
        }

        internal string CollectUntilSymbol(params string[] terminators)
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

        internal void Throw(string message)
        {
            throw new XdrParseException(FilePath, Current.Line, Current.Column, message);
        }
    }
}
