namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ReleaseReadinessSuiteSupport;

    /// <summary>
    /// Packaged server and client peer-matrix release-readiness suites.
    /// </summary>
    internal static class ReleaseReadinessPackagingCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ReleaseReadinessSuites",
                    caseId: "PackedServerPackageServesLinuxKernelClient",
                    displayName: "A clean packaged OpenNFS.Server consumer can be mounted and used by a Linux kernel client",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecutePackedServerPackageServesLinuxKernelClientAsync),

                new TestCaseDescriptor(
                    suiteId: "ReleaseReadinessSuites",
                    caseId: "PackedServerPackageDeniesLinuxKernelClientMount",
                    displayName: "A clean packaged OpenNFS.Server consumer preserves denied Linux kernel mount behavior",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync),

                new TestCaseDescriptor(
                    suiteId: "ReleaseReadinessSuites",
                    caseId: "PackedClientPackageExecutesAgainstSampleKnfsdAndGanesha",
                    displayName: "A clean packaged OpenNFS.Client consumer executes the current peer matrix against the sample server, knfsd, and nfs-ganesha",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync),

                new TestCaseDescriptor(
                    suiteId: "ReleaseReadinessSuites",
                    caseId: "PackedClientPackageSurfacesNegativeResultsAgainstSampleKnfsdAndGanesha",
                    displayName: "A clean packaged OpenNFS.Client consumer surfaces negative results against the sample server, knfsd, and nfs-ganesha",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync),
            };
        }
    }
}
