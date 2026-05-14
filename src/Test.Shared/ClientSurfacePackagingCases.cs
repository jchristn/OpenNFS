namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Apis;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientSurfaceSuiteSupport;

    /// <summary>
    /// Packed-consumer and negative lifetime client suites.
    /// </summary>
    internal static class ClientSurfacePackagingCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageExecutesFromCleanConsumerApp",
                        displayName: "Packed client package restores and executes from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageSupportsRpcSecGssConfigurationFromCleanConsumerApp",
                        displayName: "Packed client package restores the RPCSEC_GSS builder and settings surface from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageSupportsRpcSecGssConfigurationFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageSupportsRawNfs42CompoundFromCleanConsumerApp",
                        displayName: "Packed client package restores bundled NFSv4.2 generated types and executes a raw NFSv4.2 COMPOUND from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageSupportsRawNfs42CompoundFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageSupportsGroupedNfs42PlanningFromCleanConsumerApp",
                        displayName: "Packed client package restores grouped NFSv4.2 file helpers and prepares minor-version-2 plans from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageSupportsGroupedNfs42PlanningFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageReusesGroupedNfs42SessionFromCleanConsumerApp",
                        displayName: "Packed client package restores the reusable grouped NFSv4.2 session path and reuses one healthy session across consecutive grouped calls",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageReusesGroupedNfs42SessionFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageReconnectsGroupedNfs42SessionAfterTransportBreakFromCleanConsumerApp",
                        displayName: "Packed client package restores the grouped NFSv4.2 reconnect-and-bind path across a transport break",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageReconnectsGroupedNfs42SessionAfterTransportBreakFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackagePreservesNegativeLifetimeFailures",
                        displayName: "Packed client package preserves clear negative lifetime failures from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync),
            };
        }
    }
}
