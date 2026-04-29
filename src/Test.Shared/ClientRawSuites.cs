namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the public raw-client request and planning surface.
    /// </summary>
    public static class ClientRawSuites
    {
        /// <summary>
        /// Creates the shared raw-client suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClientRawSuites",
                displayName: "Client Raw Surface Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ClientRawSuites",
                        caseId: "V3ProcedurePlanUsesClientPolicy",
                        displayName: "Raw NFSv3 procedure planning uses client endpoint, auth, transport, and retry policy",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("primary.example", 2049)
                                .AddAlternateEndpoint("failover.example", 3049)
                                .WithEndpointSelectionMode(OpenNfsEndpointSelectionMode.SequentialFailover)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.AuthSys)
                                .WithRetryPolicy(
                                    maximumAttempts: 4,
                                    initialDelay: TimeSpan.FromMilliseconds(150),
                                    maximumDelay: TimeSpan.FromMilliseconds(450),
                                    useExponentialBackoff: true)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan plan = await client.PrepareV3ProcedureAsync(
                                new OpenNfsV3ProcedureRequest(
                                    procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                                    procedurePayload: new byte[] { 0x10, 0x20, 0x30 },
                                    retryMode: OpenNfsRetryMode.UseClientPolicy),
                                cancellationToken).ConfigureAwait(false);

                            if (plan.ProcedureNumber != (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ
                                || plan.ProgramNumber != NFS_PROGRAM_Program.Program
                                || plan.VersionNumber != NFS_PROGRAM_Program.Version_NFS_V3
                                || !plan.ProcedurePayload.Span.SequenceEqual(new byte[] { 0x10, 0x20, 0x30 })
                                || plan.TransportPolicy != OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                                || plan.AuthenticationFlavor != OpenNfsAuthenticationFlavor.AuthSys
                                || plan.RetryMode != OpenNfsRetryMode.UseClientPolicy)
                            {
                                throw new InvalidOperationException("Expected the raw NFSv3 procedure plan to preserve the requested procedure payload and client policy selections.");
                            }

                            if (plan.CandidateEndpoints.Count != 2
                                || !string.Equals(plan.CandidateEndpoints[0].Host, "primary.example", StringComparison.Ordinal)
                                || plan.CandidateEndpoints[0].Port != 2049
                                || !string.Equals(plan.CandidateEndpoints[1].Host, "failover.example", StringComparison.Ordinal)
                                || plan.CandidateEndpoints[1].Port != 3049)
                            {
                                throw new InvalidOperationException("Expected the raw NFSv3 procedure plan to preserve client endpoint ordering.");
                            }

                            if (plan.RetryPlan.Count != 4
                                || plan.RetryPlan[0].AttemptNumber != 1
                                || plan.RetryPlan[0].DelayBeforeAttempt != TimeSpan.Zero
                                || plan.RetryPlan[1].DelayBeforeAttempt != TimeSpan.FromMilliseconds(150)
                                || plan.RetryPlan[2].DelayBeforeAttempt != TimeSpan.FromMilliseconds(300)
                                || plan.RetryPlan[3].DelayBeforeAttempt != TimeSpan.FromMilliseconds(450))
                            {
                                throw new InvalidOperationException("Expected the raw NFSv3 procedure plan to expose client retry timing, including bounded exponential backoff.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientRawSuites",
                        caseId: "CompoundPlanIsOrderedAndTcpOnly",
                        displayName: "Raw NFSv4 COMPOUND planning preserves operation order and forces TCP",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("compound.example", 2049)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.RpcSecGss)
                                .WithRetryPolicy(
                                    maximumAttempts: 6,
                                    initialDelay: TimeSpan.FromMilliseconds(100),
                                    maximumDelay: TimeSpan.FromSeconds(1),
                                    useExponentialBackoff: true)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsCompoundPlan plan = await client.PrepareCompoundAsync(
                                new OpenNfsCompoundRequest(
                                    protocolVersion: OpenNfsProtocolVersion.Nfs42,
                                    tag: "probe",
                                    operations: new OpenNfsCompoundOperation[]
                                    {
                                        new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                                        new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, new byte[] { 0x01, 0x02 }),
                                    },
                                    retryMode: OpenNfsRetryMode.SingleAttemptOnly),
                                cancellationToken).ConfigureAwait(false);

                            if (plan.ProtocolVersion != OpenNfsProtocolVersion.Nfs42
                                || plan.MinorVersion != 2
                                || !string.Equals(plan.Tag, "probe", StringComparison.Ordinal)
                                || plan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                || plan.AuthenticationFlavor != OpenNfsAuthenticationFlavor.RpcSecGss
                                || plan.RetryMode != OpenNfsRetryMode.SingleAttemptOnly)
                            {
                                throw new InvalidOperationException("Expected the raw NFSv4 COMPOUND plan to preserve version and authentication selections while forcing TCP transport.");
                            }

                            if (plan.Operations.Count != 2
                                || plan.Operations[0].OperationNumber != (uint)nfs_opnum4.OP_PUTROOTFH
                                || plan.Operations[1].OperationNumber != (uint)nfs_opnum4.OP_GETFH
                                || !plan.Operations[1].OperationPayload.Span.SequenceEqual(new byte[] { 0x01, 0x02 }))
                            {
                                throw new InvalidOperationException("Expected the raw NFSv4 COMPOUND plan to preserve operation order and payload content.");
                            }

                            if (plan.RetryPlan.Count != 1
                                || plan.RetryPlan[0].AttemptNumber != 1
                                || plan.RetryPlan[0].DelayBeforeAttempt != TimeSpan.Zero)
                            {
                                throw new InvalidOperationException("Expected single-attempt COMPOUND planning to collapse the retry plan to one attempt.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientRawSuites",
                        caseId: "RawPlanningRequiresOpenClientAndHonorsCancellation",
                        displayName: "Raw client planning requires an open client and honors cancellation",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("raw.example", 2049)
                                .Build();

                            try
                            {
                                await client.PrepareV3ProcedureAsync(
                                    new OpenNfsV3ProcedureRequest(0, Array.Empty<byte>()),
                                    CancellationToken.None).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected raw client planning to require an open client.");
                            }
                            catch (InvalidOperationException exception)
                            {
                                if (!exception.Message.Contains("OpenAsync", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the closed-client raw-planning failure to direct the caller toward OpenAsync.");
                                }
                            }

                            await client.OpenAsync(CancellationToken.None).ConfigureAwait(false);

                            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                            cancellationTokenSource.Cancel();

                            try
                            {
                                await client.PrepareCompoundAsync(
                                    new OpenNfsCompoundRequest(
                                        OpenNfsProtocolVersion.Nfs41,
                                        string.Empty,
                                        new OpenNfsCompoundOperation[]
                                        {
                                            new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                                        }),
                                    cancellationTokenSource.Token).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected raw client planning to honor cancellation tokens.");
                            }
                            catch (OperationCanceledException)
                            {
                                if (client.State != OpenNfsClientState.Open)
                                {
                                    throw new InvalidOperationException("Expected canceled raw planning to leave the client in the open state.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientRawSuites",
                        caseId: "V3ProcedureExecutionReturnsRawReplyAndAcceptedPayload",
                        displayName: "Raw NFSv3 execution returns full reply bytes and accepted-success payload data",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] expectedReplyPayload = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
                            ScriptedRpcExecutor executor = new ScriptedRpcExecutor(
                                (request, attempt, token) =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    token.ThrowIfCancellationRequested();

                                    if (attempt.AttemptNumber != 1
                                        || !string.Equals(request.OperationName, "Raw RPC program 100005 version 3 procedure 5", StringComparison.Ordinal))
                                    {
                                        throw new InvalidOperationException("Expected public raw execution to emit a stable diagnostic operation name and use the first pipeline attempt.");
                                    }

                                    RpcGenerated.call_body? callBody = request.CallEnvelope.Header.body?.cbody;
                                    if (callBody is null
                                        || callBody.prog != 100005
                                        || callBody.vers != 3
                                        || callBody.proc != 5
                                        || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS
                                        || !request.CallEnvelope.ProcedurePayload.Span.SequenceEqual(new byte[] { 0x10, 0x20, 0x30 }))
                                    {
                                        throw new InvalidOperationException("Expected public raw execution to preserve the requested v3-era call envelope.");
                                    }

                                    return Task.FromResult(
                                        RpcMessageFactory.CreateAcceptedReply(
                                            xid: request.CallEnvelope.Header.xid,
                                            status: RpcGenerated.accept_stat.SUCCESS,
                                            verifier: RpcAuthenticationCodec.CreateNone(),
                                            procedurePayload: expectedReplyPayload));
                                });

                            OpenNfsClient client = new OpenNfsClient(
                                new OpenNfsClientSettings(
                                    serverHost: "raw.example",
                                    serverPort: 2049,
                                    authenticationFlavor: OpenNfsAuthenticationFlavor.AuthSys),
                                executor,
                                transportPipeline: null);

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedureReply reply = await client.ExecuteV3ProcedureAsync(
                                new OpenNfsV3ProcedureRequest(
                                    procedureNumber: 5,
                                    procedurePayload: new byte[] { 0x10, 0x20, 0x30 },
                                    retryMode: OpenNfsRetryMode.UseClientPolicy,
                                    programNumber: 100005,
                                    versionNumber: 3),
                                OpenNfsOperationIdempotency.Idempotent,
                                cancellationToken).ConfigureAwait(false);

                            if (reply.Plan.ProgramNumber != 100005
                                || reply.Plan.VersionNumber != 3
                                || reply.Plan.ProcedureNumber != 5
                                || !string.Equals(reply.OperationName, "Raw RPC program 100005 version 3 procedure 5", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the public raw execution reply to preserve the validated v3 procedure plan and diagnostic name.");
                            }

                            if (!reply.ReadAcceptedSuccessProcedurePayload().AsSpan().SequenceEqual(expectedReplyPayload))
                            {
                                throw new InvalidOperationException("Expected the public raw execution reply to expose the accepted-success procedure payload bytes.");
                            }

                            RpcMessageEnvelope decodedReply = RpcMessageCodec.Decode(reply.EncodedReply);
                            if (decodedReply.Header.body?.mtype != RpcGenerated.msg_type.REPLY
                                || !decodedReply.ProcedurePayload.Span.SequenceEqual(expectedReplyPayload))
                            {
                                throw new InvalidOperationException("Expected the public raw execution reply to preserve the full encoded RPC reply envelope.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientRawSuites",
                        caseId: "V3ProcedureExecutionFallsBackToUdpForV3Policy",
                        displayName: "Raw NFSv3 execution falls back to UDP for v3-era transport policy when TCP fails",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            using UdpClient udpServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                            int port = ((IPEndPoint)udpServer.Client.LocalEndPoint!).Port;
                            byte[] expectedReplyPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

                            Task serverTask = Task.Run(
                                async () =>
                                {
                                    UdpReceiveResult receivedDatagram = await udpServer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                    RpcMessageEnvelope request = RpcMessageCodec.Decode(receivedDatagram.Buffer);
                                    RpcGenerated.call_body? callBody = request.Header.body?.cbody;
                                    if (callBody is null
                                        || callBody.prog != 100005
                                        || callBody.vers != 3
                                        || callBody.proc != 5)
                                    {
                                        throw new InvalidOperationException("Expected raw UDP fallback execution to preserve the requested MOUNT v3 EXPORT envelope.");
                                    }

                                    byte[] replyBytes = RpcMessageCodec.Encode(
                                        RpcMessageFactory.CreateAcceptedReply(
                                            xid: request.Header.xid,
                                            status: RpcGenerated.accept_stat.SUCCESS,
                                            verifier: RpcAuthenticationCodec.CreateNone(),
                                            procedurePayload: expectedReplyPayload));

                                    _ = await udpServer.SendAsync(
                                        replyBytes,
                                        replyBytes.Length,
                                        receivedDatagram.RemoteEndPoint).ConfigureAwait(false);
                                },
                                cancellationToken);

                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithConnectionTimeout(TimeSpan.FromMilliseconds(200))
                                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedureReply reply = await client.ExecuteV3ProcedureAsync(
                                new OpenNfsV3ProcedureRequest(
                                    procedureNumber: 5,
                                    procedurePayload: Array.Empty<byte>(),
                                    retryMode: OpenNfsRetryMode.UseClientPolicy,
                                    programNumber: 100005,
                                    versionNumber: 3),
                                OpenNfsOperationIdempotency.Idempotent,
                                cancellationToken).ConfigureAwait(false);

                            if (!reply.ReadAcceptedSuccessProcedurePayload().AsSpan().SequenceEqual(expectedReplyPayload))
                            {
                                throw new InvalidOperationException("Expected raw client UDP fallback execution to preserve the accepted-success procedure payload.");
                            }

                            await serverTask.ConfigureAwait(false);
                        }),
                });
        }
    }
}
