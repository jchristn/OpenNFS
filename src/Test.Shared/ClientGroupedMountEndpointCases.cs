namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedMountEndpointCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisExecuteThroughScriptedRpcExecutor",
                    displayName: "Grouped export APIs execute MOUNT v3 requests through the client retry pipeline",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        int executionCount = 0;
                        ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                        {
                            executionCount++;

                            if (executionCount == 1)
                            {
                                throw new IOException("Simulated transient transport failure.");
                            }

                            return Task.FromResult(
                                CreateAcceptedReplyEnvelope(
                                    request.CallEnvelope.Header.xid,
                                    new mountres3
                                    {
                                        fhs_status = mountstat3.MNT3_OK,
                                        mountinfo = new mountres3_ok
                                        {
                                            fhandle = new fhandle3
                                            {
                                                Value = new byte[] { 0x10, 0x20, 0x30, 0x40 },
                                            },
                                            auth_flavors = new[] { 0, 1 },
                                        },
                                    },
                                    static (value, writer) => value.WriteTo(writer)));
                        });

                        OpenNfsClient client = new OpenNfsClient(
                            new OpenNfsClientSettings(
                                serverHost: "primary.example",
                                serverPort: 2049,
                                alternateEndpoints: new[]
                                {
                                    new OpenNfsEndpoint("failover.example", 3049),
                                },
                                endpointSelectionMode: OpenNfsEndpointSelectionMode.SequentialFailover,
                                retryPolicy: new OpenNfsRetryPolicy(
                                    maximumAttempts: 2,
                                    initialDelay: TimeSpan.FromMilliseconds(1),
                                    maximumDelay: TimeSpan.FromMilliseconds(1),
                                    useExponentialBackoff: false)),
                            rpcExecutor,
                            transportPipeline: null);

                        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                        OpenNfsMountV3Result result = await client.Exports.MountV3Async("/srv/share", cancellationToken).ConfigureAwait(false);

                        if (!result.IsSuccess
                            || !result.RootFileHandle.Span.SequenceEqual(new byte[] { 0x10, 0x20, 0x30, 0x40 })
                            || result.SupportedAuthenticationFlavors.Count != 2
                            || result.SupportedAuthenticationFlavors[0] != OpenNfsRpcAuthenticationFlavor.AuthNone
                            || result.SupportedAuthenticationFlavors[1] != OpenNfsRpcAuthenticationFlavor.AuthSys)
                        {
                            throw new InvalidOperationException("Expected grouped MOUNT execution to decode the successful reply after retry.");
                        }

                        if (rpcExecutor.Attempts.Count != 2
                            || !string.Equals(rpcExecutor.Attempts[0].Endpoint.Host, "primary.example", StringComparison.Ordinal)
                            || rpcExecutor.Attempts[0].Endpoint.Port != 2049
                            || !string.Equals(rpcExecutor.Attempts[1].Endpoint.Host, "failover.example", StringComparison.Ordinal)
                            || rpcExecutor.Attempts[1].Endpoint.Port != 3049)
                        {
                            throw new InvalidOperationException("Expected grouped MOUNT execution to advance through the configured client endpoints during retry.");
                        }

                        if (rpcExecutor.Requests.Count != 2)
                        {
                            throw new InvalidOperationException("Expected grouped MOUNT execution to issue one RPC call per transport attempt.");
                        }

                        RpcGenerated.call_body? callBody = rpcExecutor.Requests[0].CallEnvelope.Header.body?.cbody;
                        if (callBody is null
                            || callBody.prog != 100005
                            || callBody.vers != 3
                            || callBody.proc != 1
                            || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                        {
                            throw new InvalidOperationException("Expected grouped MOUNT execution to target the MOUNT v3 program and issue AUTH_SYS credentials by default.");
                        }

                        XdrReader payloadReader = new XdrReader(rpcExecutor.Requests[0].CallEnvelope.ProcedurePayload);
                        string exportPath = payloadReader.ReadString();
                        payloadReader.EnsureFullyConsumed();

                        if (!string.Equals(exportPath, "/srv/share", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected grouped MOUNT execution to XDR-encode the requested export path.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MountApisHonorDedicatedMountEndpoint",
                    displayName: "Grouped export enumeration and mount execution honor a dedicated MOUNT endpoint",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                        {
                            if (!string.Equals(attempt.Endpoint.Host, "mount.example", StringComparison.Ordinal)
                                || attempt.Endpoint.Port != 20048)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT traffic to target only the dedicated MOUNT endpoint.");
                            }

                            RpcGenerated.call_body? callBody = request.CallEnvelope.Header.body?.cbody;
                            if (callBody is null)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to emit an RPC call body.");
                            }

                            return callBody.proc switch
                            {
                                5 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new exports
                                        {
                                            Value = new exportnode
                                            {
                                                ex_dir = new dirpath
                                                {
                                                    Value = "/srv/mount",
                                                },
                                                ex_groups = new groups(),
                                                ex_next = new exports(),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                1 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new mountres3
                                        {
                                            fhs_status = mountstat3.MNT3_OK,
                                            mountinfo = new mountres3_ok
                                            {
                                                fhandle = new fhandle3
                                                {
                                                    Value = new byte[] { 0x31, 0x32, 0x33, 0x34 },
                                                },
                                                auth_flavors = new[] { 0, 1 },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                _ => throw new InvalidOperationException("Expected only MOUNT v3 EXPORT and MNT procedures in this dedicated-endpoint test."),
                            };
                        });

                        OpenNfsClient client = new OpenNfsClient(
                            new OpenNfsClientSettings(
                                serverHost: "primary.example",
                                serverPort: 2049,
                                alternateEndpoints: new[]
                                {
                                    new OpenNfsEndpoint("failover.example", 3049),
                                },
                                endpointSelectionMode: OpenNfsEndpointSelectionMode.SequentialFailover,
                                mountEndpoint: new OpenNfsEndpoint("mount.example", 20048)),
                            rpcExecutor,
                            transportPipeline: null);

                        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                        OpenNfsV3ProcedurePlan exportPlan = await client.Exports.PrepareListExportsV3Async(cancellationToken).ConfigureAwait(false);
                        OpenNfsV3ProcedurePlan mountPlan = await client.Exports.PrepareMountV3Async("/srv/mount", cancellationToken).ConfigureAwait(false);
                        IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                        OpenNfsMountV3Result mountResult = await client.Exports.MountV3Async("/srv/mount", cancellationToken).ConfigureAwait(false);

                        if (exportPlan.CandidateEndpoints.Count != 1
                            || !string.Equals(exportPlan.CandidateEndpoints[0].Host, "mount.example", StringComparison.Ordinal)
                            || exportPlan.CandidateEndpoints[0].Port != 20048)
                        {
                            throw new InvalidOperationException("Expected grouped export planning to use only the dedicated MOUNT endpoint.");
                        }

                        if (mountPlan.CandidateEndpoints.Count != 1
                            || !string.Equals(mountPlan.CandidateEndpoints[0].Host, "mount.example", StringComparison.Ordinal)
                            || mountPlan.CandidateEndpoints[0].Port != 20048)
                        {
                            throw new InvalidOperationException("Expected grouped mount planning to use only the dedicated MOUNT endpoint.");
                        }

                        if (exports.Count != 1
                            || !string.Equals(exports[0].ExportPath, "/srv/mount", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected grouped export enumeration to decode the dedicated-endpoint reply.");
                        }

                        if (!mountResult.IsSuccess
                            || !mountResult.RootFileHandle.Span.SequenceEqual(new byte[] { 0x31, 0x32, 0x33, 0x34 }))
                        {
                            throw new InvalidOperationException("Expected grouped mount execution to decode the dedicated-endpoint reply.");
                        }

                        if (rpcExecutor.Attempts.Count != 2)
                        {
                            throw new InvalidOperationException("Expected one dedicated-endpoint transport attempt per grouped MOUNT request.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "ExportEnumerationFailsThroughDedicatedMountEndpoint",
                    displayName: "Grouped export enumeration fails through the dedicated MOUNT endpoint instead of falling back to the primary endpoint",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                        {
                            throw new IOException(
                                "Dedicated MOUNT endpoint "
                                + attempt.Endpoint.Host
                                + ":"
                                + attempt.Endpoint.Port
                                + " is unavailable.");
                        });

                        OpenNfsClient client = new OpenNfsClient(
                            new OpenNfsClientSettings(
                                serverHost: "primary.example",
                                serverPort: 2049,
                                retryPolicy: new OpenNfsRetryPolicy(maximumAttempts: 1),
                                mountEndpoint: new OpenNfsEndpoint("wrong-mount.example", 20049)),
                            rpcExecutor,
                            transportPipeline: null);

                        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                        try
                        {
                            await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException("Expected grouped export enumeration to fail when the dedicated MOUNT endpoint is unavailable.");
                        }
                        catch (OpenNfsClientIoException exception)
                        {
                            if (!exception.Message.Contains("wrong-mount.example:20049", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the failure to report the dedicated MOUNT endpoint rather than the primary endpoint.");
                            }
                        }

                        if (rpcExecutor.Attempts.Count != 1
                            || !string.Equals(rpcExecutor.Attempts[0].Endpoint.Host, "wrong-mount.example", StringComparison.Ordinal)
                            || rpcExecutor.Attempts[0].Endpoint.Port != 20049)
                        {
                            throw new InvalidOperationException("Expected grouped export enumeration to attempt only the dedicated MOUNT endpoint.");
                        }
                    }),
            };
        }
    }
}
