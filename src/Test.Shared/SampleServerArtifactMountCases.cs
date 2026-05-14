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
    /// Sample artifact startup, mount, and deny-path suites.
    /// </summary>
    internal static class SampleServerArtifactMountCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "SampleArtifactStartsFromConfigFileAndServesMountedSessionFlow",
                        displayName: "Sample artifact starts from config and serves the public mounted-session flow",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "SampleArtifactHonorsDeniedMountsFromConfigFile",
                        displayName: "Sample artifact honors denied mounts from config",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "LinuxMountReadWrite",
                        displayName: "Linux kernel client mounts, reads, and writes through the sample artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountReadWriteAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "LinuxMountDenied",
                        displayName: "Linux kernel client sees a denied mount from the sample artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountDeniedAsync),

            };
        }
    }
}
