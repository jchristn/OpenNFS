namespace OpenNFS.XdrGen
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                XdrGeneratorOptions options = ParseArguments(args);
                XdrGeneratorCommand command = new XdrGeneratorCommand();
                CancellationToken cancellationToken = CancellationToken.None;
                XdrGenerationResult result = await command.RunAsync(options, cancellationToken).ConfigureAwait(false);

                Console.WriteLine("Validated " + result.Configuration.Projects.Count + " XDR project mapping(s).");
                Console.WriteLine("Validated " + result.Configuration.Projects.Sum(project => project.EntryPointFiles.Count) + " XDR generation entry-point file(s).");
                Console.WriteLine("Parsed " + result.ParsedDocuments.Count + " vendored XDR source document(s).");
                Console.WriteLine((result.CheckOnly ? "Validated" : "Emitted") + " " + result.GeneratedFiles.Count + " generated C# file(s).");
                Console.WriteLine((result.CheckOnly ? "Validated" : "Ensured") + " " + result.EnsuredGeneratedDirectories.Count + " generated directory path(s).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static XdrGeneratorOptions ParseArguments(string[] args)
        {
            string configurationPath = Path.Combine(AppContext.BaseDirectory, "xdrgen.json");
            bool checkOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];

                if (string.Equals(argument, "--config", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("The '--config' option requires a following file path.", nameof(args));
                    }

                    configurationPath = args[i + 1];
                    i++;
                    continue;
                }

                if (string.Equals(argument, "--check", StringComparison.OrdinalIgnoreCase))
                {
                    checkOnly = true;
                    continue;
                }

                throw new ArgumentException("Unsupported XDR generator argument: " + argument, nameof(args));
            }

            return new XdrGeneratorOptions(configurationPath, checkOnly);
        }
    }
}
