namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropOpenNfsHostSupport;

    /// <summary>
    /// Mounted-session OpenNFS host and sample-host client interop suites.
    /// </summary>
    internal static class InteropOpenNfsHostMountedCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstOpenNfsServer",
                    displayName: "OpenNFS.Client mounts, browses, reads, writes, and commits against a live OpenNFS server host",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteClientAgainstOpenNfsServerAsync),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSeesDeniedMountFromOpenNfsServer",
                    displayName: "OpenNFS.Client receives a denied mount result from a live OpenNFS server host",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteNegativeClientAgainstOpenNfsServerAsync),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstSampleOpenNfsServerArtifact",
                    displayName: "OpenNFS.Client mounts, browses, reads, writes, and commits against the runnable Sample.OpenNfsServer artifact",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteClientAgainstSampleOpenNfsServerAsync),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSeesDeniedMountFromSampleOpenNfsServerArtifact",
                    displayName: "OpenNFS.Client receives a denied mount result from the runnable Sample.OpenNfsServer artifact",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteNegativeClientAgainstSampleOpenNfsServerAsync),
            };
        }
    }
}
