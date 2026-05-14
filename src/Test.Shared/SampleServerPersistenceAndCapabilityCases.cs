namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Persistent restart and capability-surface sample-server suites.
    /// </summary>
    internal static class SampleServerPersistenceAndCapabilityCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "PersistentFileHandleRestart",
                        displayName: "Sample artifact preserves mounted filehandles across restart when mappings persist",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePersistentFileHandleRestartAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "PersistentFileHandleRestartNegative",
                        displayName: "Sample artifact invalidates old filehandles when the persistent mapping file is replaced",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePersistentFileHandleRestartNegativeAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "CapabilitySurfaceRoundTripsAndPersistsAcrossRestart",
                        displayName: "Sample artifact exercises ACL, idmap, delegation, and locking flows and persists ACL state across restart",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "CapabilitySurfaceReportsNegativeLockAndLookupPaths",
                        displayName: "Sample artifact reports negative lookup and lock-conflict paths through the public client",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync),

            };
        }
    }
}
