namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropLinuxServerSupport;

    /// <summary>
    /// Linux kernel NFS server interop suites.
    /// </summary>
    internal static class InteropLinuxKnfsdServerCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer",
                    displayName: "OpenNFS.Client mounts, reads, writes, and commits against a real Linux kernel NFS server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteClientAgainstLinuxKnfsdServerAsync),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServerOverNfs40",
                    displayName: "OpenNFS.Client browses, opens, writes, and commits against a real Linux kernel NFSv4.0 server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServerOverNfs40",
                    displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a real Linux kernel NFS server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async),
            };
        }
    }
}
