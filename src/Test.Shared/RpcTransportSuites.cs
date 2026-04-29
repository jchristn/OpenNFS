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

    /// <summary>
    /// Touchstone suites covering RFC 5531 message envelopes and record marking.
    /// </summary>
    public static class RpcTransportSuites
    {
        /// <summary>
        /// Creates the shared ONC RPC transport suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "RpcTransportSuites",
                displayName: "RPC Transport Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "RpcTransportSuites",
                        caseId: "RecordMarkingFragmentation",
                        displayName: "Record marking fragments and reassembles call messages",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcGenerated.authsys_parms expectedSystemCredential = new RpcGenerated.authsys_parms
                            {
                                stamp = 99,
                                machinename = "transport-client",
                                uid = 501,
                                gid = 20,
                                gids = new uint[] { 30, 31, 32 },
                            };

                            RpcMessageEnvelope expectedEnvelope = RpcMessageFactory.CreateCall(
                                xid: 0x01020304,
                                program: 100003,
                                version: 3,
                                procedure: 1,
                                credential: RpcAuthenticationCodec.CreateSystem(expectedSystemCredential),
                                verifier: RpcAuthenticationCodec.CreateNone(),
                                procedurePayload: new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0 });

                            byte[] encodedMessage = RpcMessageCodec.Encode(expectedEnvelope);
                            byte[] recordMarkedMessage = RecordMarkingCodec.EncodeMessage(encodedMessage, maximumFragmentPayloadLength: 9);

                            IReadOnlyList<RecordMarkingFragmentHeader> fragmentHeaders = ReadFragmentHeaders(recordMarkedMessage);
                            if (fragmentHeaders.Count < 2)
                            {
                                throw new InvalidOperationException("Expected record marking to split the call message across multiple fragments.");
                            }

                            for (int index = 0; index < fragmentHeaders.Count - 1; index++)
                            {
                                if (fragmentHeaders[index].IsLastFragment)
                                {
                                    throw new InvalidOperationException("Only the final record-marking fragment may be flagged as the last fragment.");
                                }
                            }

                            RecordMarkingFragmentHeader finalHeader = fragmentHeaders[fragmentHeaders.Count - 1];
                            if (!finalHeader.IsLastFragment)
                            {
                                throw new InvalidOperationException("Expected the final record-marking fragment to terminate the record.");
                            }

                            byte[] decodedMessage = RecordMarkingCodec.DecodeSingleMessage(recordMarkedMessage);
                            RpcMessageEnvelope actualEnvelope = RpcMessageCodec.Decode(decodedMessage);

                            RpcGenerated.rpc_msg_body? actualBody = actualEnvelope.Header.body;
                            if (actualBody?.mtype != RpcGenerated.msg_type.CALL)
                            {
                                throw new InvalidOperationException("Expected the reassembled RPC envelope to remain a CALL message.");
                            }

                            RpcGenerated.call_body? actualCallBody = actualBody.cbody;
                            if (actualCallBody is null)
                            {
                                throw new InvalidOperationException("Expected the reassembled RPC envelope to retain a call body.");
                            }

                            if (actualCallBody.rpcvers != RpcProtocolConstants.RpcVersion
                                || actualCallBody.prog != 100003
                                || actualCallBody.vers != 3
                                || actualCallBody.proc != 1)
                            {
                                throw new InvalidOperationException("Expected the reassembled call header to preserve program routing fields.");
                            }

                            RpcGenerated.authsys_parms actualSystemCredential = RpcAuthenticationCodec.ReadSystem(
                                actualCallBody.cred ?? throw new InvalidOperationException("Expected the call credential to remain present."));

                            if (actualSystemCredential.stamp != expectedSystemCredential.stamp
                                || !string.Equals(actualSystemCredential.machinename, expectedSystemCredential.machinename, StringComparison.Ordinal)
                                || actualSystemCredential.uid != expectedSystemCredential.uid
                                || actualSystemCredential.gid != expectedSystemCredential.gid
                                || actualSystemCredential.gids is null
                                || !actualSystemCredential.gids.SequenceEqual(expectedSystemCredential.gids ?? Array.Empty<uint>()))
                            {
                                throw new InvalidOperationException("Expected the AUTH_SYS credential payload to round-trip through record marking intact.");
                            }

                            if (!actualEnvelope.ProcedurePayload.Span.SequenceEqual(expectedEnvelope.ProcedurePayload.Span))
                            {
                                throw new InvalidOperationException("Expected the call procedure payload to survive fragmentation and reassembly intact.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcTransportSuites",
                        caseId: "AcceptedReplyRoundTrip",
                        displayName: "Accepted replies round-trip through the RPC envelope codec",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcMessageEnvelope expectedSuccessReply = RpcMessageFactory.CreateAcceptedReply(
                                xid: 0x11112222,
                                status: RpcGenerated.accept_stat.SUCCESS,
                                verifier: RpcAuthenticationCodec.CreateNone(),
                                procedurePayload: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

                            RpcMessageEnvelope actualSuccessReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedSuccessReply));
                            RpcGenerated.reply_body? successReplyBody = actualSuccessReply.Header.body?.rbody;
                            if (successReplyBody?.stat != RpcGenerated.reply_stat.MSG_ACCEPTED)
                            {
                                throw new InvalidOperationException("Expected the successful reply to remain an accepted reply.");
                            }

                            RpcGenerated.accepted_reply_reply_data? successReplyData = successReplyBody.areply?.reply_data;
                            if (successReplyData?.stat != RpcGenerated.accept_stat.SUCCESS)
                            {
                                throw new InvalidOperationException("Expected the successful reply to preserve SUCCESS status.");
                            }

                            if (successReplyData.results is null || successReplyData.results.Length != 0)
                            {
                                throw new InvalidOperationException("Expected successful replies to preserve the zero-length results sentinel.");
                            }

                            if (!actualSuccessReply.ProcedurePayload.Span.SequenceEqual(expectedSuccessReply.ProcedurePayload.Span))
                            {
                                throw new InvalidOperationException("Expected successful accepted replies to preserve their procedure result payload.");
                            }

                            RpcMessageEnvelope expectedMismatchReply = RpcMessageFactory.CreateAcceptedReply(
                                xid: 0x33334444,
                                status: RpcGenerated.accept_stat.PROG_MISMATCH,
                                verifier: RpcAuthenticationCodec.CreateNone(),
                                mismatchLowVersion: 3,
                                mismatchHighVersion: 4);

                            RpcMessageEnvelope actualMismatchReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedMismatchReply));
                            RpcGenerated.accepted_reply_reply_data_mismatch_info? mismatchInfo =
                                actualMismatchReply.Header.body?.rbody?.areply?.reply_data?.mismatch_info;

                            if (actualMismatchReply.Header.body?.rbody?.areply?.reply_data?.stat != RpcGenerated.accept_stat.PROG_MISMATCH
                                || mismatchInfo is null
                                || mismatchInfo.low != 3
                                || mismatchInfo.high != 4)
                            {
                                throw new InvalidOperationException("Expected PROG_MISMATCH accepted replies to preserve version mismatch information.");
                            }

                            if (actualMismatchReply.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected PROG_MISMATCH accepted replies to remain payload-free.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcTransportSuites",
                        caseId: "RejectedReplyRoundTrip",
                        displayName: "Rejected replies round-trip through the RPC envelope codec",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcMessageEnvelope expectedRpcMismatchReply = RpcMessageFactory.CreateRejectedReply(
                                xid: 0x55556666,
                                status: RpcGenerated.reject_stat.RPC_MISMATCH,
                                mismatchLowVersion: 2,
                                mismatchHighVersion: 4);

                            RpcMessageEnvelope actualRpcMismatchReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedRpcMismatchReply));
                            RpcGenerated.rejected_reply_mismatch_info? rpcMismatchInfo =
                                actualRpcMismatchReply.Header.body?.rbody?.rreply?.mismatch_info;

                            if (actualRpcMismatchReply.Header.body?.rbody?.stat != RpcGenerated.reply_stat.MSG_DENIED
                                || actualRpcMismatchReply.Header.body?.rbody?.rreply?.stat != RpcGenerated.reject_stat.RPC_MISMATCH
                                || rpcMismatchInfo is null
                                || rpcMismatchInfo.low != 2
                                || rpcMismatchInfo.high != 4)
                            {
                                throw new InvalidOperationException("Expected RPC_MISMATCH rejected replies to preserve version mismatch information.");
                            }

                            if (actualRpcMismatchReply.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected RPC_MISMATCH replies to remain payload-free.");
                            }

                            RpcMessageEnvelope expectedAuthErrorReply = RpcMessageFactory.CreateRejectedReply(
                                xid: 0x77778888,
                                status: RpcGenerated.reject_stat.AUTH_ERROR,
                                authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED);

                            RpcMessageEnvelope actualAuthErrorReply = RpcMessageCodec.Decode(RpcMessageCodec.Encode(expectedAuthErrorReply));
                            RpcGenerated.auth_stat? authStatus = actualAuthErrorReply.Header.body?.rbody?.rreply?.stat_value;

                            if (actualAuthErrorReply.Header.body?.rbody?.rreply?.stat != RpcGenerated.reject_stat.AUTH_ERROR
                                || authStatus != RpcGenerated.auth_stat.AUTH_BADCRED)
                            {
                                throw new InvalidOperationException("Expected AUTH_ERROR rejected replies to preserve the authentication failure status.");
                            }

                            if (actualAuthErrorReply.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected AUTH_ERROR replies to remain payload-free.");
                            }

                            return Task.CompletedTask;
                        }),

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

                            return;
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

                            return;
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
                });
        }

        private static async Task ExpectTimeoutAsync(Func<Task> action, string expectedMessageFragment)
        {
            try
            {
                await action().ConfigureAwait(false);
                throw new InvalidOperationException("Expected the transport operation to time out.");
            }
            catch (TimeoutException exception)
            {
                if (!exception.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected a timeout message containing '" + expectedMessageFragment + "', but received '" + exception.Message + "'.");
                }
            }
        }

        private static IReadOnlyList<RecordMarkingFragmentHeader> ReadFragmentHeaders(byte[] recordMarkedMessage)
        {
            List<RecordMarkingFragmentHeader> fragmentHeaders = new List<RecordMarkingFragmentHeader>();
            int offset = 0;

            while (offset < recordMarkedMessage.Length)
            {
                RecordMarkingFragmentHeader header = RecordMarkingCodec.ReadHeader(recordMarkedMessage.AsSpan(offset));
                fragmentHeaders.Add(header);
                offset += RecordMarkingCodec.HeaderLength + header.FragmentLength;
            }

            return fragmentHeaders;
        }
    }
}
