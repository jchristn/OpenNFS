namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Linq;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Provides the shared Touchstone suite catalog for every OpenNFS test runner.
    /// </summary>
    public static class OpenNfsSuites
    {
        /// <summary>
        /// Gets every shared Touchstone suite currently defined for OpenNFS.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    ReleaseReadinessSuites.Create(),
                    GeneratorSuites.RpcXdrSuites(),
                    RpcTransportSuites.Create(),
                    RpcBindSuites.Create(),
                    SecuritySuites.Create(),
                    IdMapSuites.Create(),
                    ReplaySuites.Create(),
                    FailureSuites.Create(),
                    InteropSuites.Create(),
                    NlmSuites.Create(),
                    NsmSuites.Create(),
                    NfsV40Suites.Create(),
                    NfsV41Suites.Create(),
                    NfsV42Suites.Create(),
                    ClientV40Suites.Create(),
                    NfsV3FoundationSuites.Create(),
                    MountV3Suites.Create(),
                    ServerSurfaceSuites.Create(),
                    SampleServerSuites.Create(),
                    ClientSurfaceSuites.Create(),
                    ClientRawSuites.Create(),
                    ClientGroupedSuites.Create(),
                    MountSessionSuites.Create(),
                    InteropMountSessionSuites.Create()
                };
            }
        }

        /// <summary>
        /// Gets the fast adapter-smoke catalog used by xUnit and NUnit.
        /// This excludes the heavier integration, interop, and privileged cases that are already covered
        /// by the dedicated automated runner.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> AdapterSmoke
        {
            get
            {
                return FilterCasesByTag(TestCategories.Unit);
            }
        }

        private static IReadOnlyList<TestSuiteDescriptor> FilterCasesByTag(string requiredTag)
        {
            List<TestSuiteDescriptor> filteredSuites = new List<TestSuiteDescriptor>();

            foreach (TestSuiteDescriptor suite in All)
            {
                List<TestCaseDescriptor> filteredCases = suite.Cases
                    .Where(testCase => testCase.Tags.Contains(requiredTag))
                    .ToList();

                if (filteredCases.Count == 0)
                {
                    continue;
                }

                filteredSuites.Add(new TestSuiteDescriptor(
                    suiteId: suite.SuiteId,
                    displayName: suite.DisplayName,
                    cases: filteredCases));
            }

            return filteredSuites;
        }
    }
}
