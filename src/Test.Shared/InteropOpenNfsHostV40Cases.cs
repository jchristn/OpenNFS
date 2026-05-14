namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropOpenNfsHostSupport;

    /// <summary>
    /// NFSv4.0 OpenNFS host and sample-host client interop suites.
    /// </summary>
    internal static class InteropOpenNfsHostV40Cases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientBrowsesAndLocksAgainstOpenNfsServerOverNfs40",
                    displayName: "OpenNFS.Client browses, mutates, opens, and locks against a live OpenNFS NFSv4.0 host",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteClientAgainstOpenNfsServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSurfacesNegativeResultsAgainstOpenNfsServerOverNfs40",
                    displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a live OpenNFS host",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientBrowsesAndManagesAgainstSampleOpenNfsServerArtifactOverNfs40",
                    displayName: "OpenNFS.Client browses, reads, mutates, and opens against the runnable Sample.OpenNfsServer artifact over NFSv4.0",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSurfacesNegativeResultsAgainstSampleOpenNfsServerArtifactOverNfs40",
                    displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and read results against the runnable Sample.OpenNfsServer artifact",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    executeAsync: ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async),
            };
        }
    }
}
