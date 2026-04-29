namespace Test.Automated
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--results", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    i++;
                }
            }

            if (!string.IsNullOrWhiteSpace(resultsPath))
            {
                string? resultsDirectory = Path.GetDirectoryName(resultsPath);
                if (!string.IsNullOrWhiteSpace(resultsDirectory))
                {
                    Directory.CreateDirectory(resultsDirectory);
                }
            }

            CancellationToken cancellationToken = CancellationToken.None;
            return await ConsoleRunner.RunAsync(
                OpenNfsSuites.All,
                resultsPath: resultsPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
