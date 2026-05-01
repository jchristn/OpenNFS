namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.Cli;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;
            List<string> requestedSuites = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--results", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    i++;
                    continue;
                }

                if (string.Equals(args[i], "--suite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    requestedSuites.Add(args[i + 1]);
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
                FilterSuites(requestedSuites),
                resultsPath: resultsPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static IReadOnlyList<TestSuiteDescriptor> FilterSuites(IReadOnlyList<string> requestedSuites)
        {
            if (requestedSuites.Count == 0)
            {
                return OpenNfsSuites.All;
            }

            HashSet<string> requestedIds = new HashSet<string>(requestedSuites, StringComparer.OrdinalIgnoreCase);
            List<TestSuiteDescriptor> filteredSuites = OpenNfsSuites.All
                .Where(suite => requestedIds.Contains(suite.SuiteId))
                .ToList();

            if (filteredSuites.Count != requestedIds.Count)
            {
                IEnumerable<string> missingSuites = requestedIds
                    .Where(requestedId => filteredSuites.All(suite => !string.Equals(suite.SuiteId, requestedId, StringComparison.OrdinalIgnoreCase)));
                throw new InvalidOperationException(
                    "Unknown Touchstone suite id(s): " + string.Join(", ", missingSuites) + ".");
            }

            return filteredSuites;
        }
    }
}
