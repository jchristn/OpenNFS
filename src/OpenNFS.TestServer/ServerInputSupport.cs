namespace OpenNFS.TestServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal static class ServerInputSupport
    {
        internal static IReadOnlyList<string> Tokenize(string input)
        {
            input = input.TrimStart('\uFEFF');
            List<string> tokens = new List<string>();
            StringBuilder builder = new StringBuilder();
            bool inQuotes = false;

            for (int index = 0; index < input.Length; index++)
            {
                char current = input[index];
                if (current == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(current) && !inQuotes)
                {
                    if (builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                    }

                    continue;
                }

                builder.Append(current);
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
            }

            return tokens;
        }

        internal static string? ReadLine(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        internal static string ReadRequiredValue(string prompt)
        {
            string? value = ReadValue(prompt);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("A non-empty value is required.");
            }

            return value;
        }

        internal static string? ReadValue(string prompt)
        {
            Console.Write(prompt + ": ");
            return Console.ReadLine();
        }

        internal static string ReadMultilineBlock(string prompt)
        {
            Console.WriteLine(prompt);
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                string? line = Console.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (string.Equals(line, ".", StringComparison.Ordinal))
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(line);
            }

            return builder.ToString();
        }
    }
}
