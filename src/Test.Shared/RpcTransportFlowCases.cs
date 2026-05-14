namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Rpc.RecordMarking;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.RpcTransportSuiteSupport;

    /// <summary>
    /// RPC transport receive/send, timeout, UDP policy, and client-pipeline suites.
    /// </summary>
    internal static class RpcTransportFlowCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "TcpTransportBackpressure",
                    displayName: "TCP transport handles fragmented framing and bounded fragment sizes",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        RpcMessageEnvelope expectedEnvelope = RpcMessageFactory.CreateCall(
                            xid: 0x09080706,
                            program: (uint)NFS_PROGRAM_Program.Program,
                            version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ,
                            credential: RpcAuthenticationCodec.CreateNone(),
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 });

                        RpcTransportOptions options = new RpcTransportOptions(
                            timeouts: new RpcTransportTimeouts(
                                readTimeout: TimeSpan.FromSeconds(1),
                                writeTimeout: TimeSpan.FromSeconds(1)),
                            maximumTcpFragmentPayloadLength: 6);

                        byte[] framedInboundMessage = RecordMarkingCodec.EncodeMessage(
                            RpcMessageCodec.Encode(expectedEnvelope),
                            maximumFragmentPayloadLength: 6);

                        ChunkedDuplexStream stream = new ChunkedDuplexStream(
                            framedInboundMessage,
                            readSizes: new[] { 1, 2, 1, 3, 2, 4, 1, 5, 2, 6 });
                        RpcTcpTransport transport = new RpcTcpTransport(stream, options);

                        RpcMessageEnvelope actualEnvelope = await transport.ReceiveAsync(CancellationToken.None).ConfigureAwait(false);

                        if (actualEnvelope.Header.body?.mtype != RpcGenerated.msg_type.CALL
                            || actualEnvelope.Header.body?.cbody?.prog != (uint)NFS_PROGRAM_Program.Program
                            || actualEnvelope.Header.body?.cbody?.vers != (uint)NFS_PROGRAM_Program.Version_NFS_V3)
                        {
                            throw new InvalidOperationException("Expected TCP transport receive to preserve the CALL routing information.");
                        }

                        if (!actualEnvelope.ProcedurePayload.Span.SequenceEqual(expectedEnvelope.ProcedurePayload.Span))
                        {
                            throw new InvalidOperationException("Expected TCP transport receive to reassemble the fragmented procedure payload.");
                        }

                        await transport.SendAsync(expectedEnvelope, CancellationToken.None).ConfigureAwait(false);

                        IReadOnlyList<RecordMarkingFragmentHeader> fragmentHeaders = ReadFragmentHeaders(stream.WrittenBytes);
                        if (fragmentHeaders.Count < 2)
                        {
                            throw new InvalidOperationException("Expected TCP transport send to emit multiple record-marking fragments when the fragment limit is small.");
                        }

                        if (fragmentHeaders.Any(header => header.FragmentLength > options.MaximumTcpFragmentPayloadLength))
                        {
                            throw new InvalidOperationException("Expected every emitted TCP fragment to honor the configured maximum fragment payload length.");
                        }

                        if (!RecordMarkingCodec.DecodeSingleMessage(stream.WrittenBytes).SequenceEqual(RpcMessageCodec.Encode(expectedEnvelope)))
                        {
                            throw new InvalidOperationException("Expected TCP transport send to preserve the encoded RPC envelope bytes.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "TransportTimeouts",
                    displayName: "Transport operations surface timeout failures cleanly",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        RpcTransportOptions options = new RpcTransportOptions(
                            timeouts: new RpcTransportTimeouts(
                                readTimeout: TimeSpan.FromMilliseconds(50),
                                writeTimeout: TimeSpan.FromMilliseconds(50)));

                        RpcTcpTransport transport = new RpcTcpTransport(new BlockingStream(), options);
                        RpcMessageEnvelope messageEnvelope = RpcMessageFactory.CreateCall(
                            xid: 0x12345678,
                            program: (uint)NFS_PROGRAM_Program.Program,
                            version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL,
                            credential: RpcAuthenticationCodec.CreateNone(),
                            verifier: RpcAuthenticationCodec.CreateNone());

                        await ExpectTimeoutAsync(
                            () => transport.ReceiveAsync(CancellationToken.None),
                            "read").ConfigureAwait(false);

                        await ExpectTimeoutAsync(
                            () => transport.SendAsync(messageEnvelope, CancellationToken.None),
                            "write").ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "UdpTransportV3EraFlow",
                    displayName: "UDP transport supports v3-era flows and preserves datagram boundaries",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async _ =>
                    {
                        RpcMessageEnvelope outboundCall = RpcMessageFactory.CreateCall(
                            xid: 0x0A0B0C0D,
                            program: (uint)NFS_PROGRAM_Program.Program,
                            version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR,
                            credential: RpcAuthenticationCodec.CreateNone(),
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0x11, 0x12, 0x13 });

                        RpcMessageEnvelope inboundReply = RpcMessageFactory.CreateAcceptedReply(
                            xid: 0x0A0B0C0D,
                            status: RpcGenerated.accept_stat.SUCCESS,
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0x21, 0x22, 0x23, 0x24 });

                        InMemoryDatagramChannel datagramChannel = new InMemoryDatagramChannel(
                            new[] { RpcMessageCodec.Encode(inboundReply) });
                        RpcUdpTransport transport = new RpcUdpTransport(
                            new RpcProgramBinding((uint)NFS_PROGRAM_Program.Program, (uint)NFS_PROGRAM_Program.Version_NFS_V3),
                            datagramChannel,
                            new RpcTransportOptions(
                                timeouts: new RpcTransportTimeouts(
                                    readTimeout: TimeSpan.FromSeconds(1),
                                    writeTimeout: TimeSpan.FromSeconds(1))));

                        await transport.SendAsync(outboundCall, CancellationToken.None).ConfigureAwait(false);
                        RpcMessageEnvelope receivedReply = await transport.ReceiveAsync(CancellationToken.None).ConfigureAwait(false);

                        if (datagramChannel.SentDatagrams.Count != 1)
                        {
                            throw new InvalidOperationException("Expected UDP transport send to emit exactly one datagram.");
                        }

                        if (!datagramChannel.SentDatagrams[0].SequenceEqual(RpcMessageCodec.Encode(outboundCall)))
                        {
                            throw new InvalidOperationException("Expected UDP transport send to preserve the encoded RPC envelope bytes.");
                        }

                        if (receivedReply.Header.body?.rbody?.stat != RpcGenerated.reply_stat.MSG_ACCEPTED
                            || receivedReply.Header.body?.rbody?.areply?.reply_data?.stat != RpcGenerated.accept_stat.SUCCESS)
                        {
                            throw new InvalidOperationException("Expected UDP transport receive to decode the accepted reply correctly.");
                        }

                        if (!receivedReply.ProcedurePayload.Span.SequenceEqual(inboundReply.ProcedurePayload.Span))
                        {
                            throw new InvalidOperationException("Expected UDP transport receive to preserve the reply procedure payload bytes.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "UdpTransportBlocksV4",
                    displayName: "UDP transport explicitly rejects v4-era bindings",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: ignored =>
                    {
                        try
                        {
                            RpcUdpTransport transport = new RpcUdpTransport(
                                new RpcProgramBinding((uint)NFS4_PROGRAM_Program.Program, (uint)NFS4_PROGRAM_Program.Version_NFS_V4),
                                new InMemoryDatagramChannel(Array.Empty<byte[]>()));
                            _ = transport;
                            throw new InvalidOperationException("Expected UDP transport construction for NFSv4 bindings to be rejected.");
                        }
                        catch (NotSupportedException exception)
                        {
                            if (!exception.Message.Contains("must use TCP", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected UDP transport rejection to explain the TCP-only requirement.");
                            }
                        }

                        if (!RpcTransportPolicy.IsUdpSupported(
                            new RpcProgramBinding((uint)NFS_PROGRAM_Program.Program, (uint)NFS_PROGRAM_Program.Version_NFS_V3)))
                        {
                            throw new InvalidOperationException("Expected UDP transport policy to allow NFSv3 bindings.");
                        }

                        if (RpcTransportPolicy.IsUdpSupported(
                            new RpcProgramBinding((uint)NFS4_PROGRAM_Program.Program, (uint)NFS4_PROGRAM_Program.Version_NFS_V4)))
                        {
                            throw new InvalidOperationException("Expected UDP transport policy to reject NFSv4 bindings.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "ClientRetryPolicyHonorsIdempotencyRules",
                    displayName: "Client retry policy honors idempotency rules",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        List<TimeSpan> observedRetryDelays = new List<TimeSpan>();
                        OpenNfsTransportPipeline pipeline = new OpenNfsTransportPipeline(
                            (delay, token) =>
                            {
                                observedRetryDelays.Add(delay);
                                return Task.CompletedTask;
                            });

                        OpenNfsTransportPipelineRequest idempotentRequest = new OpenNfsTransportPipelineRequest(
                            operationName: "NFSv3 READ",
                            candidateEndpoints: new OpenNfsEndpoint[]
                            {
                                new OpenNfsEndpoint("primary.example", 2049),
                                new OpenNfsEndpoint("failover.example", 3049),
                            },
                            connectionTimeout: TimeSpan.FromSeconds(5),
                            responseTimeout: TimeSpan.FromMilliseconds(40),
                            retryPolicy: new OpenNfsRetryPolicy(
                                maximumAttempts: 3,
                                initialDelay: TimeSpan.FromMilliseconds(25),
                                maximumDelay: TimeSpan.FromMilliseconds(50),
                                useExponentialBackoff: true),
                            idempotency: OpenNfsTransportPipelineIdempotency.Idempotent);

                        List<string> idempotentAttemptEndpoints = new List<string>();
                        int idempotentAttemptCount = 0;

                        string idempotentReply = await pipeline.ExecuteAsync(
                            idempotentRequest,
                            (attempt, token) =>
                            {
                                idempotentAttemptCount++;
                                idempotentAttemptEndpoints.Add(attempt.Endpoint.Host + ":" + attempt.Endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));

                                if (attempt.AttemptNumber < 3)
                                {
                                    throw new IOException("Simulated transient transport fault.");
                                }

                                return Task.FromResult("read-ok");
                            },
                            validateReply: null,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (!string.Equals(idempotentReply, "read-ok", StringComparison.Ordinal)
                            || idempotentAttemptCount != 3
                            || !idempotentAttemptEndpoints.SequenceEqual(
                                new[]
                                {
                                    "primary.example:2049",
                                    "failover.example:3049",
                                    "failover.example:3049",
                                }))
                        {
                            throw new InvalidOperationException("Expected idempotent client operations to retry across the ordered candidate endpoints until the retry policy was exhausted or a reply succeeded.");
                        }

                        if (observedRetryDelays.Count != 2
                            || observedRetryDelays[0] != TimeSpan.FromMilliseconds(25)
                            || observedRetryDelays[1] != TimeSpan.FromMilliseconds(50))
                        {
                            throw new InvalidOperationException("Expected idempotent client retries to honor the configured backoff schedule.");
                        }

                        OpenNfsTransportPipelineRequest nonIdempotentRequest = new OpenNfsTransportPipelineRequest(
                            operationName: "NFSv3 CREATE",
                            candidateEndpoints: new OpenNfsEndpoint[]
                            {
                                new OpenNfsEndpoint("primary.example", 2049),
                                new OpenNfsEndpoint("failover.example", 3049),
                            },
                            connectionTimeout: TimeSpan.FromSeconds(5),
                            responseTimeout: TimeSpan.FromMilliseconds(40),
                            retryPolicy: new OpenNfsRetryPolicy(
                                maximumAttempts: 3,
                                initialDelay: TimeSpan.FromMilliseconds(25),
                                maximumDelay: TimeSpan.FromMilliseconds(50),
                                useExponentialBackoff: true),
                            idempotency: OpenNfsTransportPipelineIdempotency.NonIdempotent);

                        int nonIdempotentAttemptCount = 0;

                        try
                        {
                            _ = await pipeline.ExecuteAsync<string>(
                                nonIdempotentRequest,
                                (attempt, token) =>
                                {
                                    nonIdempotentAttemptCount++;
                                    return Task.FromException<string>(new IOException("Simulated transient transport fault."));
                                },
                                validateReply: null,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            throw new InvalidOperationException("Expected non-idempotent client operations not to retry after a transport fault.");
                        }
                        catch (IOException)
                        {
                            if (nonIdempotentAttemptCount != 1)
                            {
                                throw new InvalidOperationException("Expected non-idempotent client operations to stop after the first failed attempt.");
                            }
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "RpcTransportSuites",
                    caseId: "ClientTransportPipelineHandlesTimeoutAndReplyValidation",
                    displayName: "Client transport pipeline handles timeout and reply-validation retries",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsTransportPipeline pipeline = new OpenNfsTransportPipeline((delay, token) => Task.CompletedTask);
                        OpenNfsTransportPipelineRequest request = new OpenNfsTransportPipelineRequest(
                            operationName: "NFSv3 GETATTR",
                            candidateEndpoints: new OpenNfsEndpoint[]
                            {
                                new OpenNfsEndpoint("pipeline.example", 2049),
                            },
                            connectionTimeout: TimeSpan.FromSeconds(5),
                            responseTimeout: TimeSpan.FromMilliseconds(25),
                            retryPolicy: new OpenNfsRetryPolicy(
                                maximumAttempts: 3,
                                initialDelay: TimeSpan.FromMilliseconds(10),
                                maximumDelay: TimeSpan.FromMilliseconds(10),
                                useExponentialBackoff: false),
                            idempotency: OpenNfsTransportPipelineIdempotency.Idempotent);

                        int attemptCount = 0;
                        string reply = await pipeline.ExecuteAsync(
                            request,
                            async (attempt, token) =>
                            {
                                attemptCount++;

                                if (attempt.AttemptNumber == 1)
                                {
                                    await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
                                    return "too-late";
                                }

                                if (attempt.AttemptNumber == 2)
                                {
                                    return "wrong-reply";
                                }

                                return "good-reply";
                            },
                            (attempt, value) =>
                            {
                                if (!string.Equals(value, "good-reply", StringComparison.Ordinal))
                                {
                                    throw new OpenNfsReplyValidationException(
                                        "The reply payload did not match the expected procedure result shape.",
                                        isRetryable: true);
                                }
                            },
                            cancellationToken).ConfigureAwait(false);

                        if (!string.Equals(reply, "good-reply", StringComparison.Ordinal) || attemptCount != 3)
                        {
                            throw new InvalidOperationException("Expected the client transport pipeline to retry after a timeout and after a retryable reply-validation failure.");
                        }

                        OpenNfsTransportPipelineRequest fatalValidationRequest = new OpenNfsTransportPipelineRequest(
                            operationName: "NFSv3 REMOVE",
                            candidateEndpoints: new OpenNfsEndpoint[]
                            {
                                new OpenNfsEndpoint("pipeline.example", 2049),
                            },
                            connectionTimeout: TimeSpan.FromSeconds(5),
                            responseTimeout: TimeSpan.FromMilliseconds(25),
                            retryPolicy: new OpenNfsRetryPolicy(
                                maximumAttempts: 3,
                                initialDelay: TimeSpan.FromMilliseconds(10),
                                maximumDelay: TimeSpan.FromMilliseconds(10),
                                useExponentialBackoff: false),
                            idempotency: OpenNfsTransportPipelineIdempotency.Idempotent);

                        int fatalValidationAttemptCount = 0;

                        try
                        {
                            _ = await pipeline.ExecuteAsync(
                                fatalValidationRequest,
                                (attempt, token) =>
                                {
                                    fatalValidationAttemptCount++;
                                    return Task.FromResult("fatal-reply");
                                },
                                (attempt, value) =>
                                {
                                    throw new OpenNfsReplyValidationException(
                                        "The reply verifier was malformed.",
                                        isRetryable: false);
                                },
                                cancellationToken).ConfigureAwait(false);

                            throw new InvalidOperationException("Expected non-retryable reply validation failures to stop the client pipeline immediately.");
                        }
                        catch (OpenNfsReplyValidationException exception)
                        {
                            if (exception.IsRetryable || fatalValidationAttemptCount != 1)
                            {
                                throw new InvalidOperationException("Expected non-retryable reply validation failures to stop after the first attempt.");
                            }
                        }
                    }),
            };
        }
    }
}
