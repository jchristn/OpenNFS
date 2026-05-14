namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ServerSurfaceSuiteSupport;

    /// <summary>
    /// Server README, runtime, and packaged-consumer suites.
    /// </summary>
    internal static class ServerSurfacePackagingCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "ReadmeServerSnippetCompilesFromCleanConsumerApp",
                        displayName: "The canonical README server snippet compiles from a clean packaged consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltApplicationServesV3AndV40Flows",
                        displayName: "Built application serves real NFSv3 mount flow, NFSv4.0 direct flow, NFSv4.1 session-management flow, and initial NFSv4.2 COMPOUND flow",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuiltApplicationServesV3AndV40FlowsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltApplicationPreservesDeniedMountBehavior",
                        displayName: "Built application preserves denied MOUNT behavior on the public server surface",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "PackedServerPackageExecutesFromCleanConsumerApp",
                        displayName: "Packed server package runs a real server from a clean consumer app, including the opt-in NFSv4.1 and initial NFSv4.2 surfaces",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "PackedServerPackagePreservesNegativeExportValidation",
                        displayName: "Packed server package preserves denied mount behavior from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackagePreservesNegativeExportValidationAsync),
            };
        }
    }
}
