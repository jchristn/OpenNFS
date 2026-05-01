namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;

    internal static class ReadmeSnippetSupport
    {
        public static string ExtractClientExampleSnippet()
        {
            return ExtractFirstCSharpBlockAfterHeading("### Client example");
        }

        public static string ExtractServerExampleSnippet()
        {
            return ExtractFirstCSharpBlockAfterHeading("### Server example");
        }

        private static string ExtractFirstCSharpBlockAfterHeading(string heading)
        {
            string readmePath = Path.Combine(RepositoryPaths.GetRepositoryRoot(), "README.md");
            string[] lines = File.ReadAllLines(readmePath);

            int headingIndex = -1;
            for (int index = 0; index < lines.Length; index++)
            {
                if (string.Equals(lines[index].Trim(), heading, StringComparison.Ordinal))
                {
                    headingIndex = index;
                    break;
                }
            }

            if (headingIndex < 0)
            {
                throw new InvalidOperationException("Could not find README heading '" + heading + "'.");
            }

            int codeFenceStart = -1;
            for (int index = headingIndex + 1; index < lines.Length; index++)
            {
                if (string.Equals(lines[index].Trim(), "```csharp", StringComparison.Ordinal))
                {
                    codeFenceStart = index;
                    break;
                }
            }

            if (codeFenceStart < 0)
            {
                throw new InvalidOperationException(
                    "Could not find a C# code block after README heading '" + heading + "'.");
            }

            int codeFenceEnd = -1;
            for (int index = codeFenceStart + 1; index < lines.Length; index++)
            {
                if (string.Equals(lines[index].Trim(), "```", StringComparison.Ordinal))
                {
                    codeFenceEnd = index;
                    break;
                }
            }

            if (codeFenceEnd < 0)
            {
                throw new InvalidOperationException(
                    "Could not find the end of the C# code block after README heading '" + heading + "'.");
            }

            return string.Join(
                    Environment.NewLine,
                    lines.AsSpan(codeFenceStart + 1, codeFenceEnd - codeFenceStart - 1).ToArray())
                + Environment.NewLine;
        }
    }
}
