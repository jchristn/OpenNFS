namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropLinuxServerSupport;

    /// <summary>
    /// Linux userspace NFS server interop suites.
    /// </summary>
    internal static class InteropLinuxUserspaceServerCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstLinuxNfs40Server",
                    displayName: "OpenNFS.Client browses, opens, writes, and commits against a real Linux NFSv4.0 server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteClientAgainstLinuxServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxNfs40Server",
                    displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a real Linux NFSv4.0 server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteNegativeClientAgainstLinuxServerOverNfs40Async),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientReadsAndWritesAgainstLinuxNfsServer",
                    displayName: "OpenNFS.Client mounts, reads, writes, and commits against a real Linux NFS server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteClientAgainstLinuxServerAsync),

                new TestCaseDescriptor(
                    suiteId: "InteropSuites",
                    caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxNfsServer",
                    displayName: "OpenNFS.Client surfaces negative lookup behavior against a real Linux NFS server container",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteNegativeClientAgainstLinuxServerAsync),
            };
        }
    }
}
