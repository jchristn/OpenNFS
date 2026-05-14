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
            List<string> requestedCases = new List<string>();

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
                    continue;
                }

                if (string.Equals(args[i], "--case", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    requestedCases.Add(args[i + 1]);
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
                FilterSuites(requestedSuites, requestedCases),
                resultsPath: resultsPath,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static IReadOnlyList<TestSuiteDescriptor> FilterSuites(
            IReadOnlyList<string> requestedSuites,
            IReadOnlyList<string> requestedCases)
        {
            IReadOnlyList<TestSuiteDescriptor> suites = requestedSuites.Count == 0
                ? OpenNfsSuites.All
                : FilterSuitesById(requestedSuites);

            if (requestedCases.Count == 0)
            {
                return suites;
            }

            HashSet<string> requestedCaseIds = new HashSet<string>(requestedCases, StringComparer.OrdinalIgnoreCase);
            List<TestSuiteDescriptor> filteredSuites = new List<TestSuiteDescriptor>();
            HashSet<string> matchedCases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TestSuiteDescriptor suite in suites)
            {
                List<TestCaseDescriptor> filteredCases = suite.Cases
                    .Where(testCase => requestedCaseIds.Contains(testCase.CaseId) || requestedCaseIds.Contains(testCase.TestId))
                    .ToList();

                if (filteredCases.Count == 0)
                {
                    continue;
                }

                foreach (TestCaseDescriptor testCase in filteredCases)
                {
                    matchedCases.Add(testCase.CaseId);
                    matchedCases.Add(testCase.TestId);
                }

                filteredSuites.Add(new TestSuiteDescriptor(
                    suiteId: suite.SuiteId,
                    displayName: suite.DisplayName,
                    cases: filteredCases,
                    beforeSuiteAsync: suite.BeforeSuiteAsync,
                    afterSuiteAsync: suite.AfterSuiteAsync));
            }

            IEnumerable<string> missingCases = requestedCaseIds
                .Where(requestedCaseId => !matchedCases.Contains(requestedCaseId));

            if (missingCases.Any())
            {
                throw new InvalidOperationException(
                    "Unknown Touchstone case id(s): " + string.Join(", ", missingCases) + ".");
            }

            return filteredSuites;
        }

        private static IReadOnlyList<TestSuiteDescriptor> FilterSuitesById(IReadOnlyList<string> requestedSuites)
        {
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
