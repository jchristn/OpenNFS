namespace OpenNFS.TestClient
{
    using System;

    internal sealed class ClientCommandLineOptions
    {
        private ClientCommandLineOptions(bool showHelp, string? scriptPath)
        {
            ShowHelp = showHelp;
            ScriptPath = scriptPath;
        }

        internal bool ShowHelp { get; }

        internal string? ScriptPath { get; }

        internal static ClientCommandLineOptions Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            bool showHelp = false;
            string? scriptPath = null;

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        showHelp = true;
                        break;

                    case "--script":
                        if (index + 1 >= args.Length)
                        {
                            throw new ArgumentException("The argument '--script' requires a following path.");
                        }

                        scriptPath = args[++index];
                        if (string.IsNullOrWhiteSpace(scriptPath))
                        {
                            throw new ArgumentException("The argument '--script' must contain a non-empty path.");
                        }

                        break;

                    default:
                        throw new ArgumentException("Unknown OpenNFS.TestClient argument '" + args[index] + "'.");
                }
            }

            return new ClientCommandLineOptions(showHelp, scriptPath);
        }
    }
}
