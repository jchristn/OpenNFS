namespace OpenNFS.XdrGen.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Tokenizes normalized XDR source.
    /// </summary>
    public sealed class XdrTokenizer
    {
        private const string Symbols = "{}[]<>()=;,:*+-";

        /// <summary>
        /// Tokenizes the provided source text.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        /// <param name="source">Raw source text.</param>
        /// <returns>Token sequence terminated by EOF.</returns>
        public IReadOnlyList<XdrToken> Tokenize(string filePath, string source)
        {
            string normalizedSource = NormalizeSource(source);
            List<XdrToken> tokens = new List<XdrToken>();

            int index = 0;
            int line = 1;
            int column = 1;

            while (index < normalizedSource.Length)
            {
                char current = normalizedSource[index];

                if (current == '\r')
                {
                    index++;
                    continue;
                }

                if (current == '\n')
                {
                    line++;
                    column = 1;
                    index++;
                    continue;
                }

                if (char.IsWhiteSpace(current))
                {
                    column++;
                    index++;
                    continue;
                }

                if (IsIdentifierStart(current))
                {
                    int start = index;
                    int startColumn = column;

                    index++;
                    column++;

                    while (index < normalizedSource.Length && IsIdentifierPart(normalizedSource[index]))
                    {
                        index++;
                        column++;
                    }

                    tokens.Add(new XdrToken(
                        XdrTokenKind.Identifier,
                        normalizedSource.Substring(start, index - start),
                        line,
                        startColumn));
                    continue;
                }

                if (char.IsDigit(current))
                {
                    int start = index;
                    int startColumn = column;

                    index++;
                    column++;

                    while (index < normalizedSource.Length && IsNumberPart(normalizedSource[index]))
                    {
                        index++;
                        column++;
                    }

                    tokens.Add(new XdrToken(
                        XdrTokenKind.Number,
                        normalizedSource.Substring(start, index - start),
                        line,
                        startColumn));
                    continue;
                }

                if (Symbols.IndexOf(current, StringComparison.Ordinal) >= 0)
                {
                    tokens.Add(new XdrToken(XdrTokenKind.Symbol, current.ToString(), line, column));
                    index++;
                    column++;
                    continue;
                }

                throw new XdrParseException(filePath, line, column, "Unsupported character '" + current + "'.");
            }

            tokens.Add(new XdrToken(XdrTokenKind.EndOfFile, string.Empty, line, column));
            return tokens;
        }

        private static bool IsIdentifierStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }

        private static bool IsIdentifierPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsNumberPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string NormalizeSource(string source)
        {
            StringBuilder withoutBlockComments = new StringBuilder(source.Length);
            bool inBlockComment = false;

            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';

                if (!inBlockComment && current == '/' && next == '*')
                {
                    inBlockComment = true;
                    index++;
                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        index++;
                        continue;
                    }

                    if (current == '\r' || current == '\n')
                    {
                        withoutBlockComments.Append(current);
                    }

                    continue;
                }

                withoutBlockComments.Append(current);
            }

            StringBuilder normalized = new StringBuilder(withoutBlockComments.Length);
            string[] lines = withoutBlockComments.ToString().Split('\n');

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("%", StringComparison.Ordinal))
                {
                    normalized.AppendLine();
                    continue;
                }

                normalized.Append(line);
                if (index < lines.Length - 1)
                {
                    normalized.Append('\n');
                }
            }

            return normalized.ToString();
        }
    }
}
