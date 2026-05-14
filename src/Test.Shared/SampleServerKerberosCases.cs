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
    /// Kerberos registration and negative-configuration sample-server suites.
    /// </summary>
    internal static class SampleServerKerberosCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "KerberosMount",
                        displayName: "Sample artifact registers the Kerberos mechanism and routes RPCSEC_GSS calls through the configured authenticator",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteKerberosMountAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "KerberosMountNotConfigured",
                        displayName: "Sample artifact rejects RPCSEC_GSS calls with AUTH_TOOWEAK when no Kerberos mechanism is registered",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteKerberosMountNotConfiguredAsync),
            };
        }
    }
}
