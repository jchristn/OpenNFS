namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static OpenNFS.TestClient.ClientRuntimeState;

    internal static class ClientInputSupport
    {
        internal static string ReadMultilineBlock(string prompt)
        {
            if (IsScripted)
            {
                throw new InvalidOperationException(
                    "Scripted mode requires inline file contents. Use: write <remote-path> <text>.");
            }

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
            if (IsScripted)
            {
                throw new InvalidOperationException(
                    "Scripted mode does not support interactive prompts. Supply the value inline for: "
                    + prompt
                    + ".");
            }

            Console.Write(prompt + ": ");
            return Console.ReadLine();
        }

        internal static async Task<int> RunScriptAsync(string scriptPath, CancellationToken cancellationToken)
        {
            string normalizedScriptPath = Path.GetFullPath(scriptPath);
            if (!File.Exists(normalizedScriptPath))
            {
                throw new FileNotFoundException("Could not find the command script.", normalizedScriptPath);
            }

            string[] lines = await File.ReadAllLinesAsync(normalizedScriptPath, cancellationToken).ConfigureAwait(false);
            for (int index = 0; index < lines.Length; index++)
            {
                string rawLine = lines[index].TrimStart('\uFEFF');
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!RunForever)
                {
                    break;
                }

                Console.WriteLine("> " + line);
                try
                {
                    await Program.ExecuteCommandAsync(line, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        "[ERROR] Script command failed at line "
                        + (index + 1).ToString(CultureInfo.InvariantCulture)
                        + ": "
                        + exception.Message);
                    return 1;
                }
            }

            return 0;
        }
    }
}
