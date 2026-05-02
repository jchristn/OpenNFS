namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;
    using OpenNFS.Client;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V41.Backchannel;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Hosting;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V41.State;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering NFSv4.1 session-management primitives: EXCHANGE_ID, CREATE_SESSION,
    /// DESTROY_SESSION, DESTROY_CLIENTID, SEQUENCE slot tables with replay caching, and BIND_CONN_TO_SESSION.
    /// </summary>
    /// <remarks>
    /// These cases exercise the operation processor at the typed-argument level. The wire-level
    /// COMPOUND-over-RPC dispatcher is a separate task; once that lands, additional cases will exercise
    /// the same flows over the network surface.
    /// </remarks>
    public static class NfsV41Suites
    {
        /// <summary>
        /// Creates the shared NFSv4.1 suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NfsV41Suites",
                displayName: "NFSv4.1 Session Surface",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ExchangeIdAssignsAndRefreshesClientId",
                        displayName: "EXCHANGE_ID issues a clientid for a fresh owner and refreshes the same clientid for a repeat owner",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            Nfs41SessionOperationProcessor processor = CreateProcessor();
                            EXCHANGE_ID4args first = BuildExchangeIdArguments(verifier: 0xAA, ownerSeed: 1);
                            EXCHANGE_ID4res firstResult = processor.ProcessExchangeId(first);
                            EnsureSuccess(firstResult.eir_status, nameof(processor.ProcessExchangeId));
                            ulong firstClientId = firstResult.eir_resok4!.eir_clientid!.Value;
                            uint firstSequence = firstResult.eir_resok4!.eir_sequenceid!.Value;
                            if (firstClientId == 0 || firstSequence != 1)
                            {
                                throw new InvalidOperationException("Initial EXCHANGE_ID must issue a non-zero clientid and sequenceid 1.");
                            }

                            EXCHANGE_ID4args repeat = BuildExchangeIdArguments(verifier: 0xAA, ownerSeed: 1);
                            EXCHANGE_ID4res repeatResult = processor.ProcessExchangeId(repeat);
                            EnsureSuccess(repeatResult.eir_status, nameof(processor.ProcessExchangeId));
                            if (repeatResult.eir_resok4!.eir_clientid!.Value != firstClientId
                                || repeatResult.eir_resok4!.eir_sequenceid!.Value != firstSequence + 1)
                            {
                                throw new InvalidOperationException(
                                    "Repeat EXCHANGE_ID with the same owner must keep the clientid and increment the sequenceid.");
                            }

                            EXCHANGE_ID4args distinctOwner = BuildExchangeIdArguments(verifier: 0xAA, ownerSeed: 2);
                            EXCHANGE_ID4res distinctResult = processor.ProcessExchangeId(distinctOwner);
                            EnsureSuccess(distinctResult.eir_status, nameof(processor.ProcessExchangeId));
                            if (distinctResult.eir_resok4!.eir_clientid!.Value == firstClientId)
                            {
                                throw new InvalidOperationException(
                                    "Distinct owners must receive distinct clientids.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ExchangeIdCreateSessionSequence",
                        displayName: "EXCHANGE_ID, CREATE_SESSION, and SEQUENCE flow end-to-end and produce a usable fore-channel slot table",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            Nfs41SessionOperationProcessor processor = CreateProcessor();
                            Nfs41OperationContext context = new Nfs41OperationContext("127.0.0.1:50001");

                            EXCHANGE_ID4res exchangeResult = processor.ProcessExchangeId(BuildExchangeIdArguments(0xAB, 11));
                            EnsureSuccess(exchangeResult.eir_status, nameof(processor.ProcessExchangeId));
                            ulong clientId = exchangeResult.eir_resok4!.eir_clientid!.Value;

                            CREATE_SESSION4args createArgs = BuildCreateSessionArguments(
                                clientId,
                                sequenceId: exchangeResult.eir_resok4!.eir_sequenceid!.Value,
                                requestedSlots: 16);
                            CREATE_SESSION4res createResult = processor.ProcessCreateSession(createArgs, context);
                            EnsureSuccess(createResult.csr_status, nameof(processor.ProcessCreateSession));
                            byte[] sessionIdBytes = createResult.csr_resok4!.csr_sessionid!.Value!;

                            SEQUENCE4args sequenceArgs = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: false, highestSlotId: 15);
                            Nfs41SequenceOutcome outcome = processor.ProcessSequence(sequenceArgs, context);
                            if (outcome.State != Nfs41SlotState.Fresh
                                || outcome.Result.sr_status != nfsstat4.NFS4_OK
                                || context.CurrentSession is null
                                || context.CurrentSession.ClientId != clientId)
                            {
                                throw new InvalidOperationException("First SEQUENCE must produce a Fresh outcome with the resolved session attached.");
                            }

                            processor.RecordSequenceReply(outcome, ReadOnlyMemory<byte>.Empty);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ExactlyOnceSlotReplay",
                        displayName: "Slot table replays cached SEQUENCE replies byte-for-byte and rejects retries when caching was not requested",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            Nfs41SessionOperationProcessor processor = CreateProcessor();
                            Nfs41OperationContext context = new Nfs41OperationContext("127.0.0.1:50002");
                            byte[] sessionIdBytes = EstablishSession(processor, context, ownerSeed: 21, requestedSlots: 4);

                            SEQUENCE4args cachedArgs = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 3);
                            Nfs41SequenceOutcome cachedOutcome = processor.ProcessSequence(cachedArgs, context);
                            if (cachedOutcome.State != Nfs41SlotState.Fresh)
                            {
                                throw new InvalidOperationException("Cached request must initially be Fresh.");
                            }

                            byte[] cachedReplyBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
                            processor.RecordSequenceReply(cachedOutcome, cachedReplyBytes);

                            Nfs41SequenceOutcome replayOutcome = processor.ProcessSequence(cachedArgs, context);
                            if (replayOutcome.State != Nfs41SlotState.Replay)
                            {
                                throw new InvalidOperationException("Retried cached request must be Replay.");
                            }

                            if (!replayOutcome.CachedReply.Span.SequenceEqual(cachedReplyBytes))
                            {
                                throw new InvalidOperationException("Replay outcome must surface the cached reply bytes byte-for-byte.");
                            }

                            SEQUENCE4args uncachedArgs = BuildSequenceArguments(sessionIdBytes, slotId: 1, sequenceId: 1, cacheThis: false, highestSlotId: 3);
                            Nfs41SequenceOutcome uncachedOutcome = processor.ProcessSequence(uncachedArgs, context);
                            if (uncachedOutcome.State != Nfs41SlotState.Fresh)
                            {
                                throw new InvalidOperationException("Uncached request must initially be Fresh.");
                            }

                            processor.RecordSequenceReply(uncachedOutcome, ReadOnlyMemory<byte>.Empty);
                            Nfs41SequenceOutcome retryUncachedOutcome = processor.ProcessSequence(uncachedArgs, context);
                            if (retryUncachedOutcome.State != Nfs41SlotState.RetryUncached
                                || retryUncachedOutcome.Result.sr_status != nfsstat4.NFS4ERR_RETRY_UNCACHED_REP)
                            {
                                throw new InvalidOperationException(
                                    "Retried uncached request must surface NFS4ERR_RETRY_UNCACHED_REP.");
                            }

                            SEQUENCE4args misorderedArgs = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 99, cacheThis: false, highestSlotId: 3);
                            Nfs41SequenceOutcome misorderedOutcome = processor.ProcessSequence(misorderedArgs, context);
                            if (misorderedOutcome.State != Nfs41SlotState.Misordered
                                || misorderedOutcome.Result.sr_status != nfsstat4.NFS4ERR_BAD_SEQID)
                            {
                                throw new InvalidOperationException(
                                    "Out-of-range sequenceid must surface NFS4ERR_BAD_SEQID.");
                            }

                            SEQUENCE4args badSlotArgs = BuildSequenceArguments(sessionIdBytes, slotId: 999, sequenceId: 1, cacheThis: false, highestSlotId: 3);
                            Nfs41SequenceOutcome badSlotOutcome = processor.ProcessSequence(badSlotArgs, context);
                            if (badSlotOutcome.State != Nfs41SlotState.BadSlot
                                || badSlotOutcome.Result.sr_status != nfsstat4.NFS4ERR_BADSLOT)
                            {
                                throw new InvalidOperationException(
                                    "Out-of-range slotid must surface NFS4ERR_BADSLOT.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "BindConnToSession",
                        displayName: "BIND_CONN_TO_SESSION binds a new connection to an existing session and rejects unknown session ids",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            Nfs41SessionOperationProcessor processor = CreateProcessor();
                            Nfs41OperationContext context = new Nfs41OperationContext("127.0.0.1:50010");
                            byte[] sessionIdBytes = EstablishSession(processor, context, ownerSeed: 42, requestedSlots: 8);

                            Nfs41OperationContext alternateConnection = new Nfs41OperationContext("127.0.0.1:50011");
                            BIND_CONN_TO_SESSION4args bindArgs = new BIND_CONN_TO_SESSION4args
                            {
                                bctsa_sessid = new sessionid4 { Value = sessionIdBytes },
                                bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                                bctsa_use_conn_in_rdma_mode = false,
                            };
                            BIND_CONN_TO_SESSION4res bindResult = processor.ProcessBindConnToSession(bindArgs, alternateConnection);
                            EnsureSuccess(bindResult.bctsr_status, nameof(processor.ProcessBindConnToSession));
                            if (bindResult.bctsr_resok4!.bctsr_dir != channel_dir_from_server4.CDFS4_FORE)
                            {
                                throw new InvalidOperationException("BIND_CONN_TO_SESSION must reflect the requested fore-channel direction.");
                            }

                            BIND_CONN_TO_SESSION4args unknownSessionArgs = new BIND_CONN_TO_SESSION4args
                            {
                                bctsa_sessid = new sessionid4 { Value = new byte[Nfs41SessionId.Length] },
                                bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                                bctsa_use_conn_in_rdma_mode = false,
                            };
                            BIND_CONN_TO_SESSION4res unknownSessionResult = processor.ProcessBindConnToSession(unknownSessionArgs, alternateConnection);
                            if (unknownSessionResult.bctsr_status != nfsstat4.NFS4ERR_BADSESSION)
                            {
                                throw new InvalidOperationException("Unknown sessionid must surface NFS4ERR_BADSESSION.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "DestroySessionAndClientId",
                        displayName: "DESTROY_SESSION and DESTROY_CLIENTID remove server-side state and reject unknown ids",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            Nfs41SessionOperationProcessor processor = CreateProcessor();
                            Nfs41OperationContext context = new Nfs41OperationContext("127.0.0.1:50020");
                            byte[] sessionIdBytes = EstablishSession(processor, context, ownerSeed: 73, requestedSlots: 4);

                            DESTROY_SESSION4args destroySessionArgs = new DESTROY_SESSION4args
                            {
                                dsa_sessionid = new sessionid4 { Value = sessionIdBytes },
                            };
                            DESTROY_SESSION4res destroySessionResult = processor.ProcessDestroySession(destroySessionArgs);
                            EnsureSuccess(destroySessionResult.dsr_status, nameof(processor.ProcessDestroySession));

                            DESTROY_SESSION4res missingSessionResult = processor.ProcessDestroySession(destroySessionArgs);
                            if (missingSessionResult.dsr_status != nfsstat4.NFS4ERR_BADSESSION)
                            {
                                throw new InvalidOperationException("DESTROY_SESSION on a removed session must surface NFS4ERR_BADSESSION.");
                            }

                            DESTROY_CLIENTID4args missingClientArgs = new DESTROY_CLIENTID4args
                            {
                                dca_clientid = new clientid4 { Value = ulong.MaxValue },
                            };
                            DESTROY_CLIENTID4res missingClientResult = processor.ProcessDestroyClientId(missingClientArgs);
                            if (missingClientResult.dcr_status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                            {
                                throw new InvalidOperationException("DESTROY_CLIENTID on an unknown clientid must surface NFS4ERR_STALE_CLIENTID.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "WireCompoundExchangeIdCreateSessionSequence",
                        displayName: "Wire-level COMPOUND dispatcher round-trips EXCHANGE_ID, CREATE_SESSION, and SEQUENCE through the RPC envelope",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs41CompoundService service = new Nfs41CompoundService(CreateProcessor());

                            COMPOUND4args bootstrap = new COMPOUND4args
                            {
                                tag = MakeTag("bootstrap"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC1, 91) },
                                },
                            };
                            COMPOUND4res bootstrapResponse = await DispatchCompoundAsync(service, bootstrap, "127.0.0.1:51001", xid: 1, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(bootstrapResponse.status, "EXCHANGE_ID COMPOUND");
                            ulong clientId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_clientid!.Value;
                            uint sequenceId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_sequenceid!.Value;

                            COMPOUND4args createSession = new COMPOUND4args
                            {
                                tag = MakeTag("create-session"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_CREATE_SESSION,
                                        opcreate_session = BuildCreateSessionArguments(clientId, sequenceId, requestedSlots: 8),
                                    },
                                },
                            };
                            COMPOUND4res createSessionResponse = await DispatchCompoundAsync(service, createSession, "127.0.0.1:51001", xid: 2, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(createSessionResponse.status, "CREATE_SESSION COMPOUND");
                            byte[] sessionIdBytes = createSessionResponse.resarray![0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;

                            COMPOUND4args sequenceCompound = new COMPOUND4args
                            {
                                tag = MakeTag("sequence"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_SEQUENCE,
                                        opsequence = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: false, highestSlotId: 7),
                                    },
                                },
                            };
                            COMPOUND4res sequenceResponse = await DispatchCompoundAsync(service, sequenceCompound, "127.0.0.1:51001", xid: 3, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(sequenceResponse.status, "SEQUENCE COMPOUND");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "WireCompoundSlotReplayIsByteStable",
                        displayName: "Wire-level COMPOUND replays the cached reply byte-for-byte when the same SEQUENCE is retried",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs41CompoundService service = new Nfs41CompoundService(CreateProcessor());
                            byte[] sessionIdBytes = await EstablishSessionOverWireAsync(service, "127.0.0.1:51002", ownerSeed: 92, requestedSlots: 4, cancellationToken).ConfigureAwait(false);

                            COMPOUND4args cachedCompound = new COMPOUND4args
                            {
                                tag = MakeTag("cached"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_SEQUENCE,
                                        opsequence = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 3),
                                    },
                                },
                            };

                            byte[] firstReplyBytes = await DispatchCompoundRawAsync(service, cachedCompound, "127.0.0.1:51002", xid: 10, cancellationToken).ConfigureAwait(false);
                            byte[] replayReplyBytes = await DispatchCompoundRawAsync(service, cachedCompound, "127.0.0.1:51002", xid: 11, cancellationToken).ConfigureAwait(false);

                            if (!firstReplyBytes.SequenceEqual(replayReplyBytes))
                            {
                                throw new InvalidOperationException("Replayed SEQUENCE must surface a byte-stable cached reply.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "WireCompoundRejectsMinorVersionMismatch",
                        displayName: "Wire-level COMPOUND surfaces NFS4ERR_MINOR_VERS_MISMATCH for unsupported minor versions",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs41CompoundService service = new Nfs41CompoundService(CreateProcessor());
                            COMPOUND4args wrongMinor = new COMPOUND4args
                            {
                                tag = MakeTag("wrong-minor"),
                                minorversion = 2,
                                argarray = new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC2, 93) },
                                },
                            };
                            COMPOUND4res response = await DispatchCompoundAsync(service, wrongMinor, "127.0.0.1:51003", xid: 20, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH)
                            {
                                throw new InvalidOperationException("Unsupported minor version must surface NFS4ERR_MINOR_VERS_MISMATCH.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TcpHostExchangeIdCreateSessionSequenceRoundTrip",
                        displayName: "Real TCP host accepts EXCHANGE_ID, CREATE_SESSION, and SEQUENCE COMPOUND from a real socket client",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            using TcpClient tcpClient = new TcpClient();
                            await tcpClient.ConnectAsync("127.0.0.1", host.NfsPort, cancellationToken).ConfigureAwait(false);
                            using NetworkStream stream = tcpClient.GetStream();
                            RpcTcpTransport transport = new RpcTcpTransport(
                                stream,
                                new RpcTransportOptions(timeouts: new RpcTransportTimeouts(
                                    readTimeout: TimeSpan.FromSeconds(15),
                                    writeTimeout: TimeSpan.FromSeconds(15))));

                            COMPOUND4args bootstrap = new COMPOUND4args
                            {
                                tag = MakeTag("real-net-bootstrap"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC4, 121) },
                                },
                            };
                            COMPOUND4res bootstrapResponse = await SendCompoundOverTransportAsync(transport, bootstrap, xid: 1001, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(bootstrapResponse.status, "real-network EXCHANGE_ID");
                            ulong clientId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_clientid!.Value;
                            uint sequenceId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_sequenceid!.Value;

                            COMPOUND4args createSession = new COMPOUND4args
                            {
                                tag = MakeTag("real-net-create"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_CREATE_SESSION,
                                        opcreate_session = BuildCreateSessionArguments(clientId, sequenceId, requestedSlots: 4),
                                    },
                                },
                            };
                            COMPOUND4res createSessionResponse = await SendCompoundOverTransportAsync(transport, createSession, xid: 1002, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(createSessionResponse.status, "real-network CREATE_SESSION");
                            byte[] sessionIdBytes = createSessionResponse.resarray![0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;

                            COMPOUND4args sequenceCompound = new COMPOUND4args
                            {
                                tag = MakeTag("real-net-sequence"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4
                                    {
                                        argop = nfs_opnum4.OP_SEQUENCE,
                                        opsequence = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: false, highestSlotId: 3),
                                    },
                                },
                            };
                            COMPOUND4res sequenceResponse = await SendCompoundOverTransportAsync(transport, sequenceCompound, xid: 1003, cancellationToken).ConfigureAwait(false);
                            EnsureSuccess(sequenceResponse.status, "real-network SEQUENCE");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionEstablishAndCleanTeardown",
                        displayName: "OpenNfsV41ClientSession establishes a session over a real socket and tears it down cleanly",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                                endpoint: new IPEndPoint(IPAddress.Loopback, host.NfsPort),
                                clientOwner: BuildClientOwner(verifier: 0xD0, ownerSeed: 200));
                            options.RequestedSlots = 4;
                            // Generous timeouts so the test stays robust on slower CI runners where
                            // loopback TCP connects + the in-process v4.1 host's EXCHANGE_ID/
                            // CREATE_SESSION round trips can take longer than the previous 15s budget.
                            options.ConnectTimeout = TimeSpan.FromSeconds(60);
                            options.CallTimeout = TimeSpan.FromSeconds(60);

                            await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                                .EstablishAsync(options, cancellationToken)
                                .ConfigureAwait(false);

                            if (session.SessionId.Count != 16
                                || session.ClientId == 0
                                || session.NegotiatedSlotCount == 0)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsV41ClientSession.EstablishAsync must return a 16-byte sessionid, a non-zero clientid, and a non-zero negotiated slot count.");
                            }

                            OpenNfsV41CompoundOutcome outcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "client-noop",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (outcome.Response.status != nfsstat4.NFS4_OK
                                || outcome.SequenceId != 1
                                || outcome.SlotId != 0)
                            {
                                throw new InvalidOperationException(
                                    "First SendCompoundAsync must surface NFS4_OK on slot 0 with sequenceid 1.");
                            }

                            OpenNfsV41CompoundOutcome secondOutcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "client-noop-2",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (secondOutcome.Response.status != nfsstat4.NFS4_OK
                                || secondOutcome.SequenceId != 2)
                            {
                                throw new InvalidOperationException(
                                    "Second SendCompoundAsync must surface NFS4_OK with the per-slot sequenceid advanced to 2.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "StatusExceptionMappingClassifiesNativeStatusCodes",
                        displayName: "OpenNfsV41StatusException maps native nfsstat4 codes to the documented OpenNfsErrorCategory values",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            EnsureCategory(nfsstat4.NFS4ERR_NOENT, OpenNfsErrorCategory.NotFound);
                            EnsureCategory(nfsstat4.NFS4ERR_ACCESS, OpenNfsErrorCategory.AccessDenied);
                            EnsureCategory(nfsstat4.NFS4ERR_PERM, OpenNfsErrorCategory.AccessDenied);
                            EnsureCategory(nfsstat4.NFS4ERR_EXIST, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_NOTDIR, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_ISDIR, OpenNfsErrorCategory.Conflict);
                            EnsureCategory(nfsstat4.NFS4ERR_NOTSUPP, OpenNfsErrorCategory.Unsupported);
                            EnsureCategory(nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH, OpenNfsErrorCategory.Unsupported);
                            EnsureCategory(nfsstat4.NFS4ERR_IO, OpenNfsErrorCategory.IoError);
                            EnsureCategory(nfsstat4.NFS4ERR_NOSPC, OpenNfsErrorCategory.IoError);
                            EnsureCategory(nfsstat4.NFS4ERR_BAD_SEQID, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_BADSESSION, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_BAD_STATEID, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_OP_NOT_IN_SESSION, OpenNfsErrorCategory.ProtocolError);
                            EnsureCategory(nfsstat4.NFS4ERR_STALE, OpenNfsErrorCategory.NotFound);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "GetOutcomeOrThrowSurfacesTypedExceptionOnPartial",
                        displayName: "OpenNfsV41CompoundResult.GetOutcomeOrThrow throws OpenNfsV41StatusException on partial-state COMPOUNDs and returns the outcome on full success",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 245, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult fullSuccess = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-throw-success",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            OpenNfsV41CompoundOutcome outcome = fullSuccess.GetOutcomeOrThrow("test-success");
                            if (outcome.Response.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("GetOutcomeOrThrow must return the outcome unchanged on full success.");
                            }

                            OpenNfsV41CompoundResult partial = await session.TrySendCompoundAsync(
                                operations: new[] { new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH } },
                                cacheReply: false,
                                tag: "envelope-throw-partial",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            try
                            {
                                partial.GetOutcomeOrThrow("test-partial");
                                throw new InvalidOperationException("GetOutcomeOrThrow must throw OpenNfsV41StatusException on a partial-state COMPOUND.");
                            }
                            catch (OpenNfsV41StatusException ex)
                            {
                                if (ex.Status != nfsstat4.NFS4ERR_NOTSUPP
                                    || ex.Category != OpenNfsErrorCategory.Unsupported
                                    || ex.OperationName != "test-partial"
                                    || ex.FailedOperationIndex < 0)
                                {
                                    throw new InvalidOperationException(
                                        "OpenNfsV41StatusException must carry status=NFS4ERR_NOTSUPP, category=Unsupported, the operation name, and the failing op index.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "GetOutcomeOrThrowRethrowsTransportFailure",
                        displayName: "OpenNfsV41CompoundResult.GetOutcomeOrThrow rethrows the transport-level failure when the call did not reach the server",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 246, cancellationToken).ConfigureAwait(false);

                            session.AbortConnectionForTest();

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-throw-transport",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            bool rethrown = false;
                            try
                            {
                                result.GetOutcomeOrThrow("test-transport");
                            }
                            catch (OpenNfsV41StatusException)
                            {
                                throw new InvalidOperationException(
                                    "GetOutcomeOrThrow must NOT surface a status exception when no outcome was received; it must rethrow the transport-level failure.");
                            }
                            catch (System.IO.EndOfStreamException)
                            {
                                rethrown = true;
                            }
                            catch (System.IO.IOException)
                            {
                                rethrown = true;
                            }
                            catch (SocketException)
                            {
                                rethrown = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                rethrown = true;
                            }
                            catch (OperationCanceledException)
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    rethrown = true;
                                }
                                else
                                {
                                    throw;
                                }
                            }

                            if (!rethrown)
                            {
                                throw new InvalidOperationException(
                                    "GetOutcomeOrThrow must rethrow a transport-level failure when the call did not reach the server.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PublicV41ClientSurfaceShapeIsPinned",
                        displayName: "Public NFSv4.1 client surface exposes the expected types and methods for the OpenCIFS-aligned compatibility shape",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            EnsurePublicType(typeof(OpenNfsV41ClientOwner));
                            EnsurePublicType(typeof(OpenNfsV41ClientSessionOptions));
                            EnsurePublicType(typeof(OpenNfsV41ClientSession));
                            EnsurePublicType(typeof(OpenNfsV41CompoundOutcome));
                            EnsurePublicType(typeof(OpenNfsV41CompoundResult));
                            EnsurePublicType(typeof(OpenNfsV41CallbackHandler));
                            EnsurePublicType(typeof(OpenNfsV41CallbackDispatcher));
                            EnsurePublicType(typeof(Nfs41CallbackChannelHost));
                            EnsurePublicType(typeof(OpenNfsV41PathOperations));
                            EnsurePublicType(typeof(OpenNfsV41MountSession));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionMetadata));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionFiles));
                            EnsurePublicType(typeof(OpenNfsV41MountSessionDirectories));

                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.CreateMountSession));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Session));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Metadata));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Files));
                            EnsurePublicProperty(typeof(OpenNfsV41MountSession), nameof(OpenNfsV41MountSession.Directories));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionMetadata), nameof(OpenNfsV41MountSessionMetadata.GetAttributesAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionFiles), nameof(OpenNfsV41MountSessionFiles.ReadAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41MountSessionDirectories), nameof(OpenNfsV41MountSessionDirectories.ListAsync));

                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildAttributeMask));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildStatLikeAttributeMask));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.SplitPathComponents));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildGetAttributesOps));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildReadOps));
                            EnsurePublicMethod(typeof(OpenNfsV41PathOperations), nameof(OpenNfsV41PathOperations.BuildReaddirOps));

                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.EstablishAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.SendCompoundAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.TrySendCompoundAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ReconnectAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.DisposeAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.IsSameServerInstance));

                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.SessionId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ClientId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.NegotiatedSlotCount));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerMajorId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerMinorId));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSession), nameof(OpenNfsV41ClientSession.ServerScope));

                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.AutoReconnect));
                            EnsurePublicProperty(typeof(OpenNfsV41ClientSessionOptions), nameof(OpenNfsV41ClientSessionOptions.MaximumReconnectAttempts));

                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.IsFullSuccess));
                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.HasPartialResults));
                            EnsurePublicProperty(typeof(OpenNfsV41CompoundResult), nameof(OpenNfsV41CompoundResult.ReachedServer));

                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnRecallAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnGetAttributesAsync));
                            EnsurePublicMethod(typeof(OpenNfsV41CallbackHandler), nameof(OpenNfsV41CallbackHandler.OnRecallAnyAsync));

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundFullSuccessSurfacesEnvelope",
                        displayName: "TrySendCompoundAsync surfaces a full-success envelope when every COMPOUND op completes",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 240, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-full-success",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (!result.IsFullSuccess
                                || result.HasPartialResults
                                || result.Failure is not null
                                || result.Outcome is null
                                || result.OperationsObservedSuccessfully != 1)
                            {
                                throw new InvalidOperationException(
                                    "A SEQUENCE-only COMPOUND must surface IsFullSuccess=true with HasPartialResults=false and one OK op.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundPartialResultsAfterUnsupportedOp",
                        displayName: "TrySendCompoundAsync surfaces a partial-success envelope when SEQUENCE succeeds but a follow-on op returns NFS4ERR_NOTSUPP",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 241, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
                                },
                                cacheReply: false,
                                tag: "envelope-partial",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (result.IsFullSuccess
                                || !result.HasPartialResults
                                || !result.ReachedServer
                                || result.Outcome is null
                                || result.Outcome.Response.status != nfsstat4.NFS4ERR_NOTSUPP
                                || result.OperationsObservedSuccessfully != 1
                                || result.Outcome.Response.resarray is null
                                || result.Outcome.Response.resarray.Length != 2
                                || result.Outcome.Response.resarray[0].opsequence?.sr_status != nfsstat4.NFS4_OK
                                || result.Outcome.Response.resarray[1].opillegal?.status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException(
                                    "A SEQUENCE+PUTROOTFH COMPOUND must surface HasPartialResults=true with the SEQUENCE OK and the unsupported op carrying NFS4ERR_NOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "TrySendCompoundTransportFailureSurfacesEnvelope",
                        displayName: "TrySendCompoundAsync surfaces a transport-failure envelope when the underlying connection drops",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 242, cancellationToken).ConfigureAwait(false);

                            session.AbortConnectionForTest();

                            OpenNfsV41CompoundResult result = await session.TrySendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "envelope-transport-fail",
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (result.IsFullSuccess
                                || result.HasPartialResults
                                || result.ReachedServer
                                || result.Failure is null)
                            {
                                throw new InvalidOperationException(
                                    "A forced disconnect on a session without AutoReconnect must surface a transport-failure envelope.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "CallbackChannelRecall",
                        displayName: "Server invoker sends CB_RECALL over a real back-channel transport and the client dispatcher routes it to the handler",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionIdBytes = new byte[16];
                            sessionIdBytes[15] = 0x77;
                            uint cbProgram = 0x40000001u;

                            RecordingCallbackHandler handler = new RecordingCallbackHandler(nfsstat4.NFS4_OK);
                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionIdBytes,
                                backChannelSlotCount: 4,
                                handler: handler);

                            await using Nfs41CallbackChannelHost host = Nfs41CallbackChannelHost.Start(dispatcher, cbProgram);

                            using TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork)
                            {
                                NoDelay = true,
                            };
                            await tcpClient.ConnectAsync(IPAddress.Loopback, host.CallbackPort, cancellationToken).ConfigureAwait(false);
                            using NetworkStream stream = tcpClient.GetStream();
                            RpcTcpTransport transport = new RpcTcpTransport(
                                stream,
                                new RpcTransportOptions(
                                    timeouts: new RpcTransportTimeouts(
                                        readTimeout: TimeSpan.FromSeconds(15),
                                        writeTimeout: TimeSpan.FromSeconds(15))));

                            Nfs41Session session = BuildSessionForCallback(sessionIdBytes, cbProgram);
                            Nfs41CallbackInvoker invoker = new Nfs41CallbackInvoker(session, transport);

                            nfs_cb_argop4 recallOp = new nfs_cb_argop4
                            {
                                argop = (uint)nfs_cb_opnum4.OP_CB_RECALL,
                                opcbrecall = new CB_RECALL4args
                                {
                                    stateid = new stateid4 { seqid = 7, other = new byte[12] },
                                    truncate = false,
                                    fh = new nfs_fh4 { Value = new byte[] { 0xCA, 0xFE } },
                                },
                            };

                            Nfs41CallbackOutcome outcome = await invoker.InvokeAsync(new[] { recallOp }, cancellationToken).ConfigureAwait(false);
                            if (outcome.Response.status != nfsstat4.NFS4_OK
                                || handler.RecallCount != 1
                                || outcome.Response.resarray is null
                                || outcome.Response.resarray.Length != 2
                                || outcome.Response.resarray[1].opcbrecall?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException(
                                    "Wire-level CB_RECALL must reach the dispatcher's handler and surface NFS4_OK in the response.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "BackchannelConnectionLossRecovery",
                        displayName: "Back-channel connection loss surfaces a transport failure on the server invoker rather than hanging silently",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionIdBytes = new byte[16];
                            sessionIdBytes[15] = 0x78;
                            uint cbProgram = 0x40000002u;

                            DefaultCallbackHandler handler = new DefaultCallbackHandler();
                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionIdBytes,
                                backChannelSlotCount: 1,
                                handler: handler);

                            Nfs41CallbackChannelHost host = Nfs41CallbackChannelHost.Start(dispatcher, cbProgram);
                            int callbackPort = host.CallbackPort;

                            using TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork)
                            {
                                NoDelay = true,
                            };
                            await tcpClient.ConnectAsync(IPAddress.Loopback, callbackPort, cancellationToken).ConfigureAwait(false);
                            using NetworkStream stream = tcpClient.GetStream();
                            RpcTcpTransport transport = new RpcTcpTransport(
                                stream,
                                new RpcTransportOptions(
                                    timeouts: new RpcTransportTimeouts(
                                        readTimeout: TimeSpan.FromSeconds(2),
                                        writeTimeout: TimeSpan.FromSeconds(2))));

                            Nfs41Session session = BuildSessionForCallback(sessionIdBytes, cbProgram);
                            Nfs41CallbackInvoker invoker = new Nfs41CallbackInvoker(session, transport);

                            // Tear down the host before invoking; the connection drops mid-flight.
                            await host.DisposeAsync().ConfigureAwait(false);

                            bool surfacedFailure = false;
                            try
                            {
                                await invoker.InvokeAsync(
                                    new[]
                                    {
                                        new nfs_cb_argop4
                                        {
                                            argop = (uint)nfs_cb_opnum4.OP_CB_RECALL,
                                            opcbrecall = new CB_RECALL4args
                                            {
                                                stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                                truncate = false,
                                                fh = new nfs_fh4 { Value = Array.Empty<byte>() },
                                            },
                                        },
                                    },
                                    cancellationToken).ConfigureAwait(false);
                            }
                            catch (System.IO.EndOfStreamException)
                            {
                                surfacedFailure = true;
                            }
                            catch (System.IO.IOException)
                            {
                                surfacedFailure = true;
                            }
                            catch (SocketException)
                            {
                                surfacedFailure = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                surfacedFailure = true;
                            }
                            catch (System.IO.InvalidDataException)
                            {
                                surfacedFailure = true;
                            }
                            catch (OperationCanceledException)
                            {
                                // The transport read-timeout cancels via an internal CTS; the outer
                                // cancellationToken is unaffected, so an OCE here is a transport
                                // failure surface, not a test cancellation.
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    surfacedFailure = true;
                                }
                                else
                                {
                                    throw;
                                }
                            }

                            if (!surfacedFailure)
                            {
                                throw new InvalidOperationException(
                                    "Back-channel connection loss must surface a transport failure on the invoker rather than completing successfully.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "CallbackDispatcherRoutesRecallToHandler",
                        displayName: "OpenNfsV41CallbackDispatcher routes CB_RECALL to a custom handler with the configured back-channel slot table",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionId = new byte[16];
                            sessionId[15] = 0x42;

                            RecordingCallbackHandler handler = new RecordingCallbackHandler(nfsstat4.NFS4_OK);
                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionId,
                                backChannelSlotCount: 4,
                                handler: handler);

                            CB_COMPOUND4args compound = BuildCallbackCompound(
                                sessionId: sessionId,
                                slotId: 0,
                                sequenceId: 1,
                                cacheThis: false,
                                ops: new[]
                                {
                                    new nfs_cb_argop4
                                    {
                                        argop = (uint)nfs_cb_opnum4.OP_CB_RECALL,
                                        opcbrecall = new CB_RECALL4args
                                        {
                                            stateid = new stateid4 { seqid = 7, other = new byte[12] },
                                            truncate = false,
                                            fh = new nfs_fh4 { Value = new byte[] { 0xAA, 0xBB, 0xCC } },
                                        },
                                    },
                                });

                            CB_COMPOUND4res response = await dispatcher.ProcessCompoundAsync(compound, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4_OK
                                || handler.RecallCount != 1
                                || response.resarray is null
                                || response.resarray.Length != 2
                                || response.resarray[1].opcbrecall?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException(
                                    "CB_RECALL must be routed to the registered handler and surface NFS4_OK in the resarray.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "CallbackDispatcherDefaultHandlerReturnsNotSupp",
                        displayName: "Default OpenNfsV41CallbackHandler surfaces NFS4ERR_NOTSUPP for callbacks the host has not overridden",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionId = new byte[16];
                            sessionId[15] = 0x43;

                            DefaultCallbackHandler handler = new DefaultCallbackHandler();
                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionId,
                                backChannelSlotCount: 2,
                                handler: handler);

                            CB_COMPOUND4args compound = BuildCallbackCompound(
                                sessionId: sessionId,
                                slotId: 0,
                                sequenceId: 1,
                                cacheThis: false,
                                ops: new[]
                                {
                                    new nfs_cb_argop4
                                    {
                                        argop = (uint)nfs_cb_opnum4.OP_CB_GETATTR,
                                        opcbgetattr = new CB_GETATTR4args
                                        {
                                            fh = new nfs_fh4 { Value = new byte[] { 0x01 } },
                                            attr_request = new bitmap4 { Value = Array.Empty<uint>() },
                                        },
                                    },
                                });

                            CB_COMPOUND4res response = await dispatcher.ProcessCompoundAsync(compound, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_NOTSUPP
                                || response.resarray is null
                                || response.resarray.Length != 2
                                || response.resarray[1].opcbgetattr?.status != nfsstat4.NFS4ERR_NOTSUPP)
                            {
                                throw new InvalidOperationException(
                                    "Default callback handler must surface NFS4ERR_NOTSUPP for unimplemented callbacks.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "CallbackDispatcherRejectsCompoundWithoutSequenceFirst",
                        displayName: "OpenNfsV41CallbackDispatcher rejects CB_COMPOUND that does not lead with CB_SEQUENCE",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionId = new byte[16];
                            sessionId[15] = 0x44;

                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionId,
                                backChannelSlotCount: 1,
                                handler: new DefaultCallbackHandler());

                            CB_COMPOUND4args misordered = new CB_COMPOUND4args
                            {
                                tag = MakeTag("misordered-cb"),
                                minorversion = 1,
                                callback_ident = 0,
                                argarray = new[]
                                {
                                    new nfs_cb_argop4
                                    {
                                        argop = (uint)nfs_cb_opnum4.OP_CB_RECALL,
                                        opcbrecall = new CB_RECALL4args
                                        {
                                            stateid = new stateid4 { seqid = 1, other = new byte[12] },
                                            truncate = false,
                                            fh = new nfs_fh4 { Value = Array.Empty<byte>() },
                                        },
                                    },
                                },
                            };

                            CB_COMPOUND4res response = await dispatcher.ProcessCompoundAsync(misordered, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_OP_NOT_IN_SESSION)
                            {
                                throw new InvalidOperationException(
                                    "CB_COMPOUND without leading CB_SEQUENCE must surface NFS4ERR_OP_NOT_IN_SESSION.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "CallbackDispatcherRejectsUnknownSession",
                        displayName: "OpenNfsV41CallbackDispatcher rejects CB_COMPOUND addressed to a different sessionid",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            byte[] sessionId = new byte[16];
                            sessionId[15] = 0x45;

                            byte[] foreignSessionId = new byte[16];
                            foreignSessionId[15] = 0x99;

                            OpenNfsV41CallbackDispatcher dispatcher = new OpenNfsV41CallbackDispatcher(
                                expectedSessionId: sessionId,
                                backChannelSlotCount: 1,
                                handler: new DefaultCallbackHandler());

                            CB_COMPOUND4args foreign = BuildCallbackCompound(
                                sessionId: foreignSessionId,
                                slotId: 0,
                                sequenceId: 1,
                                cacheThis: false,
                                ops: Array.Empty<nfs_cb_argop4>());

                            CB_COMPOUND4res response = await dispatcher.ProcessCompoundAsync(foreign, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_BADSESSION)
                            {
                                throw new InvalidOperationException(
                                    "CB_COMPOUND for a different sessionid must surface NFS4ERR_BADSESSION.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionReconnect",
                        displayName: "OpenNfsV41ClientSession transparently reconnects and retries SendCompoundAsync after a forced TCP disconnect",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                                endpoint: new IPEndPoint(IPAddress.Loopback, host.NfsPort),
                                clientOwner: BuildClientOwner(verifier: 0xE5, ownerSeed: 230));
                            options.RequestedSlots = 4;
                            options.CallTimeout = TimeSpan.FromSeconds(15);
                            options.ConnectTimeout = TimeSpan.FromSeconds(15);
                            options.AutoReconnect = true;
                            options.MaximumReconnectAttempts = 2;

                            await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                                .EstablishAsync(options, cancellationToken)
                                .ConfigureAwait(false);

                            OpenNfsV41CompoundOutcome firstOutcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "before-forced-drop",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (firstOutcome.Response.status != nfsstat4.NFS4_OK || firstOutcome.SequenceId != 1)
                            {
                                throw new InvalidOperationException(
                                    "Pre-disconnect SendCompoundAsync must surface NFS4_OK with sequenceid 1.");
                            }

                            // Forcibly tear down the underlying TCP socket from the client side. The next
                            // SendCompoundAsync must transparently reconnect and deliver a fresh request.
                            session.AbortConnectionForTest();

                            OpenNfsV41CompoundOutcome reconnectedOutcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "after-forced-drop",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (reconnectedOutcome.Response.status != nfsstat4.NFS4_OK
                                || reconnectedOutcome.SequenceId != 2)
                            {
                                throw new InvalidOperationException(
                                    "Post-disconnect SendCompoundAsync must transparently reconnect and continue advancing the per-slot sequenceid to 2.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionReconnectIsDisabledByDefault",
                        displayName: "OpenNfsV41ClientSession surfaces transport failures when AutoReconnect is not enabled",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 231, cancellationToken).ConfigureAwait(false);
                            session.AbortConnectionForTest();

                            bool surfacedFailure = false;
                            try
                            {
                                await session.SendCompoundAsync(
                                    operations: Array.Empty<nfs_argop4>(),
                                    cacheReply: false,
                                    tag: "no-auto-reconnect",
                                    cancellationToken: cancellationToken).ConfigureAwait(false);
                            }
                            catch (System.IO.IOException)
                            {
                                surfacedFailure = true;
                            }
                            catch (SocketException)
                            {
                                surfacedFailure = true;
                            }
                            catch (ObjectDisposedException)
                            {
                                surfacedFailure = true;
                            }

                            if (!surfacedFailure)
                            {
                                throw new InvalidOperationException(
                                    "When AutoReconnect is disabled, SendCompoundAsync must surface a transport failure rather than silently recover.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionReconnectRebindsToExistingSession",
                        displayName: "OpenNfsV41ClientSession.ReconnectAsync replaces the underlying connection and re-binds it to the existing session",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 220, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41CompoundOutcome firstOutcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "before-reconnect",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (firstOutcome.Response.status != nfsstat4.NFS4_OK || firstOutcome.SequenceId != 1)
                            {
                                throw new InvalidOperationException(
                                    "Pre-reconnect SendCompoundAsync must surface NFS4_OK with sequenceid 1.");
                            }

                            IPEndPoint preReconnectLocal = session.LocalEndpoint;

                            await session.ReconnectAsync(cancellationToken).ConfigureAwait(false);

                            IPEndPoint postReconnectLocal = session.LocalEndpoint;
                            if (postReconnectLocal.Equals(preReconnectLocal))
                            {
                                throw new InvalidOperationException(
                                    "ReconnectAsync must replace the underlying TCP connection, but the local endpoint did not change.");
                            }

                            OpenNfsV41CompoundOutcome secondOutcome = await session.SendCompoundAsync(
                                operations: Array.Empty<nfs_argop4>(),
                                cacheReply: false,
                                tag: "after-reconnect",
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            if (secondOutcome.Response.status != nfsstat4.NFS4_OK
                                || secondOutcome.SequenceId != 2)
                            {
                                throw new InvalidOperationException(
                                    "Post-reconnect SendCompoundAsync must continue from the pre-reconnect per-slot sequenceid, advancing to 2.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ExchangeIdServerOwnerComparison",
                        displayName: "Two sessions against the same server compare equal by server-owner; sessions against a different server scope do not",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            // Two hosts share the same server-owner identity (same major id and scope) but bind on different ports.
                            Nfs41SessionOperationProcessor sharedConfigProcessorA = CreateProcessor();
                            Nfs41SessionOperationProcessor sharedConfigProcessorB = CreateProcessor();
                            await using OpenNfsTcpNfs41ServerHost hostA = OpenNfsTcpNfs41ServerHost.Start(sharedConfigProcessorA, listenerAddress: "127.0.0.1", nfsPort: 0);
                            await using OpenNfsTcpNfs41ServerHost hostB = OpenNfsTcpNfs41ServerHost.Start(sharedConfigProcessorB, listenerAddress: "127.0.0.1", nfsPort: 0);

                            await using OpenNfsV41ClientSession sessionA = await EstablishClientSessionAsync(hostA.NfsPort, ownerSeed: 210, cancellationToken).ConfigureAwait(false);
                            await using OpenNfsV41ClientSession sessionB = await EstablishClientSessionAsync(hostB.NfsPort, ownerSeed: 211, cancellationToken).ConfigureAwait(false);

                            if (!sessionA.IsSameServerInstance(sessionB))
                            {
                                throw new InvalidOperationException(
                                    "Sessions against hosts with the same server-owner major id and scope must compare equal for trunking detection.");
                            }

                            if (sessionA.ServerMajorId.Count == 0 || sessionA.ServerScope.Count == 0)
                            {
                                throw new InvalidOperationException("Server-owner major id and scope must be populated by EXCHANGE_ID.");
                            }

                            // A third host with a different scope should not compare equal.
                            Nfs41SessionOperationProcessor distinctProcessor = CreateProcessorWithScope("distinct-scope");
                            await using OpenNfsTcpNfs41ServerHost hostC = OpenNfsTcpNfs41ServerHost.Start(distinctProcessor, listenerAddress: "127.0.0.1", nfsPort: 0);

                            await using OpenNfsV41ClientSession sessionC = await EstablishClientSessionAsync(hostC.NfsPort, ownerSeed: 212, cancellationToken).ConfigureAwait(false);
                            if (sessionA.IsSameServerInstance(sessionC))
                            {
                                throw new InvalidOperationException(
                                    "Sessions against hosts with different server scope must not compare equal.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "ClientSessionEstablishFailsOnUnreachableEndpoint",
                        displayName: "OpenNfsV41ClientSession.EstablishAsync surfaces a connection failure when the endpoint is unreachable",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            // Bind a listener and immediately stop it so the port is closed.
                            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                            listener.Start();
                            int closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                            listener.Stop();

                            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                                endpoint: new IPEndPoint(IPAddress.Loopback, closedPort),
                                clientOwner: BuildClientOwner(verifier: 0xD1, ownerSeed: 201));
                            options.ConnectTimeout = TimeSpan.FromSeconds(2);

                            bool establishmentSucceeded = false;
                            try
                            {
                                OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                                    .EstablishAsync(options, cancellationToken)
                                    .ConfigureAwait(false);
                                establishmentSucceeded = true;
                                await session.DisposeAsync().ConfigureAwait(false);
                            }
                            catch (SocketException)
                            {
                            }
                            catch (System.IO.IOException)
                            {
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                            }

                            if (establishmentSucceeded)
                            {
                                throw new InvalidOperationException(
                                    "EstablishAsync must fail when the server endpoint is unreachable.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "SessionReplayAfterReconnect",
                        displayName: "Wire-level COMPOUND replays the cached reply byte-for-byte when the same slot is retried over a fresh TCP connection",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);

                            byte[] sessionIdBytes;
                            byte[] firstSequencePayload;

                            // Establish session and issue the cached SEQUENCE on the first connection.
                            using (TcpClient firstTcp = new TcpClient())
                            {
                                await firstTcp.ConnectAsync(IPAddress.Loopback, host.NfsPort, cancellationToken).ConfigureAwait(false);
                                using NetworkStream firstStream = firstTcp.GetStream();
                                RpcTcpTransport firstTransport = new RpcTcpTransport(
                                    firstStream,
                                    new RpcTransportOptions(timeouts: new RpcTransportTimeouts(
                                        readTimeout: TimeSpan.FromSeconds(15),
                                        writeTimeout: TimeSpan.FromSeconds(15))));

                                COMPOUND4args bootstrap = new COMPOUND4args
                                {
                                    tag = MakeTag("reconnect-bootstrap"),
                                    minorversion = 1,
                                    argarray = new[]
                                    {
                                        new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC5, 122) },
                                    },
                                };
                                COMPOUND4res bootstrapResponse = await SendCompoundOverTransportAsync(firstTransport, bootstrap, xid: 2001, cancellationToken).ConfigureAwait(false);
                                EnsureSuccess(bootstrapResponse.status, "reconnect EXCHANGE_ID");
                                ulong clientId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_clientid!.Value;
                                uint sequenceId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_sequenceid!.Value;

                                COMPOUND4args createSession = new COMPOUND4args
                                {
                                    tag = MakeTag("reconnect-create"),
                                    minorversion = 1,
                                    argarray = new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_CREATE_SESSION,
                                            opcreate_session = BuildCreateSessionArguments(clientId, sequenceId, requestedSlots: 4),
                                        },
                                    },
                                };
                                COMPOUND4res createSessionResponse = await SendCompoundOverTransportAsync(firstTransport, createSession, xid: 2002, cancellationToken).ConfigureAwait(false);
                                EnsureSuccess(createSessionResponse.status, "reconnect CREATE_SESSION");
                                sessionIdBytes = createSessionResponse.resarray![0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;

                                COMPOUND4args cachedSequence = new COMPOUND4args
                                {
                                    tag = MakeTag("reconnect-cached"),
                                    minorversion = 1,
                                    argarray = new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SEQUENCE,
                                            opsequence = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 3),
                                        },
                                    },
                                };
                                firstSequencePayload = await SendCompoundRawOverTransportAsync(firstTransport, cachedSequence, xid: 2003, cancellationToken).ConfigureAwait(false);
                            }

                            // Reconnect on a fresh TCP socket and replay the same SEQUENCE on slot 0.
                            byte[] replayPayload;
                            using (TcpClient secondTcp = new TcpClient())
                            {
                                await secondTcp.ConnectAsync(IPAddress.Loopback, host.NfsPort, cancellationToken).ConfigureAwait(false);
                                using NetworkStream secondStream = secondTcp.GetStream();
                                RpcTcpTransport secondTransport = new RpcTcpTransport(
                                    secondStream,
                                    new RpcTransportOptions(timeouts: new RpcTransportTimeouts(
                                        readTimeout: TimeSpan.FromSeconds(15),
                                        writeTimeout: TimeSpan.FromSeconds(15))));

                                COMPOUND4args replaySequence = new COMPOUND4args
                                {
                                    tag = MakeTag("reconnect-cached"),
                                    minorversion = 1,
                                    argarray = new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SEQUENCE,
                                            opsequence = BuildSequenceArguments(sessionIdBytes, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 3),
                                        },
                                    },
                                };
                                replayPayload = await SendCompoundRawOverTransportAsync(secondTransport, replaySequence, xid: 2004, cancellationToken).ConfigureAwait(false);
                            }

                            if (!firstSequencePayload.SequenceEqual(replayPayload))
                            {
                                throw new InvalidOperationException(
                                    "After a TCP reconnect, replaying the same SEQUENCE on slot 0 must surface a byte-stable cached reply per RFC 8881 §2.10.6.1 exactly-once semantics.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "WireCompoundRejectsOperationsBeforeSequence",
                        displayName: "Wire-level COMPOUND surfaces NFS4ERR_OP_NOT_IN_SESSION when a non-session op precedes SEQUENCE",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            Nfs41CompoundService service = new Nfs41CompoundService(CreateProcessor());
                            COMPOUND4args misordered = new COMPOUND4args
                            {
                                tag = MakeTag("misordered"),
                                minorversion = 1,
                                argarray = new[]
                                {
                                    new nfs_argop4 { argop = nfs_opnum4.OP_PUTROOTFH },
                                },
                            };
                            COMPOUND4res response = await DispatchCompoundAsync(service, misordered, "127.0.0.1:51004", xid: 30, cancellationToken).ConfigureAwait(false);
                            if (response.status != nfsstat4.NFS4ERR_OP_NOT_IN_SESSION)
                            {
                                throw new InvalidOperationException("A non-session op without preceding SEQUENCE must surface NFS4ERR_OP_NOT_IN_SESSION.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsGetAttributesCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + GETATTR COMPOUND op sequence with the requested attribute mask",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            bitmap4 mask = OpenNfsV41PathOperations.BuildAttributeMask(new ulong[]
                            {
                                Nfs41Constants.FATTR4_TYPE,
                                Nfs41Constants.FATTR4_SIZE,
                                Nfs41Constants.FATTR4_MODE,
                            });
                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildGetAttributesOps("/foo/bar/baz.txt", mask);

                            if (ops.Count != 5)
                            {
                                throw new InvalidOperationException("Expected PUTROOTFH + 3 LOOKUPs + GETATTR for a 3-component path. Observed op count: " + ops.Count);
                            }

                            if (ops[0].argop != nfs_opnum4.OP_PUTROOTFH)
                            {
                                throw new InvalidOperationException("First op must be PUTROOTFH. Observed: " + ops[0].argop);
                            }

                            string[] expectedComponents = new[] { "foo", "bar", "baz.txt" };
                            for (int index = 0; index < expectedComponents.Length; index++)
                            {
                                nfs_argop4 op = ops[index + 1];
                                if (op.argop != nfs_opnum4.OP_LOOKUP || op.oplookup?.objname?.Value?.Value?.Value is not byte[] actualBytes)
                                {
                                    throw new InvalidOperationException("Op at index " + (index + 1) + " must be a LOOKUP carrying a non-null objname.");
                                }

                                string actualText = System.Text.Encoding.UTF8.GetString(actualBytes);
                                if (!string.Equals(actualText, expectedComponents[index], StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException(
                                        "LOOKUP at index " + (index + 1) + " must carry component '"
                                        + expectedComponents[index] + "'. Observed: '" + actualText + "'");
                                }
                            }

                            nfs_argop4 terminal = ops[4];
                            if (terminal.argop != nfs_opnum4.OP_GETATTR
                                || terminal.opgetattr?.attr_request?.Value is not uint[] words
                                || !words.SequenceEqual(mask.Value!))
                            {
                                throw new InvalidOperationException("Terminal op must be GETATTR carrying the supplied attribute mask.");
                            }

                            // Empty / root path produces just PUTROOTFH + GETATTR.
                            IReadOnlyList<nfs_argop4> rootOps = OpenNfsV41PathOperations.BuildGetAttributesOps("/", mask);
                            if (rootOps.Count != 2
                                || rootOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootOps[1].argop != nfs_opnum4.OP_GETATTR)
                            {
                                throw new InvalidOperationException("Root path must produce exactly PUTROOTFH + GETATTR.");
                            }

                            // ".." segments must be rejected.
                            bool dotDotRejected = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildGetAttributesOps("/foo/../bar", mask);
                            }
                            catch (ArgumentException)
                            {
                                dotDotRejected = true;
                            }

                            _ = cancellationToken;

                            if (!dotDotRejected)
                            {
                                throw new InvalidOperationException("Path-first operations must reject '..' navigation segments.");
                            }

                            // The stat-like default mask must include FATTR4_TYPE, FATTR4_SIZE, FATTR4_MODE.
                            bitmap4 statMask = OpenNfsV41PathOperations.BuildStatLikeAttributeMask();
                            if (!IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_TYPE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_SIZE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_MODE)
                                || !IsAttributeBitSet(statMask, Nfs41Constants.FATTR4_OWNER))
                            {
                                throw new InvalidOperationException("BuildStatLikeAttributeMask must include FATTR4_TYPE, FATTR4_SIZE, FATTR4_MODE, and FATTR4_OWNER.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "MountSessionRoutesPathFirstGetAttributesThroughSession",
                        displayName: "OpenNfsV41MountSession.Metadata.GetAttributesAsync routes a path-first COMPOUND through the underlying session and surfaces a typed envelope",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            await using OpenNfsTcpNfs41ServerHost host = OpenNfsTcpNfs41ServerHost.Start(
                                CreateProcessor(),
                                listenerAddress: "127.0.0.1",
                                nfsPort: 0);
                            await using OpenNfsV41ClientSession session = await EstablishClientSessionAsync(host.NfsPort, ownerSeed: 250, cancellationToken).ConfigureAwait(false);

                            OpenNfsV41MountSession mountSession = session.CreateMountSession();
                            if (!ReferenceEquals(mountSession.Session, session))
                            {
                                throw new InvalidOperationException("OpenNfsV41MountSession.Session must reference the source session.");
                            }

                            OpenNfsV41CompoundResult result = await mountSession.Metadata
                                .GetAttributesAsync("/anywhere", cancellationToken)
                                .ConfigureAwait(false);

                            // Server's current v4.1 surface returns NFS4ERR_NOTSUPP for non-session ops.
                            // The mount-session facade must surface that as a partial-success envelope
                            // (SEQUENCE OK, terminal op carrying NOTSUPP) without flattening into a
                            // generic failure or hiding the partial state.
                            if (!result.ReachedServer)
                            {
                                throw new InvalidOperationException("Mount-session GETATTR must reach the server. Failure: " + result.Failure);
                            }

                            if (!result.HasPartialResults || result.Outcome is null)
                            {
                                throw new InvalidOperationException("Mount-session GETATTR against current v4.1 surface must surface partial results.");
                            }

                            // SEQUENCE was OK; the path-first ops surfaced NOTSUPP.
                            if (result.OperationsObservedSuccessfully < 1)
                            {
                                throw new InvalidOperationException("SEQUENCE inside the GETATTR COMPOUND must be observed as successful.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsReadCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + READ COMPOUND op sequence with the supplied stateid, offset, and count",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            stateid4 stateid = new stateid4
                            {
                                seqid = 7,
                                other = new byte[12] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
                            };

                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReadOps(
                                "/data/file.bin",
                                stateid,
                                offset: 4096,
                                count: 8192);

                            if (ops.Count != 4
                                || ops[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || ops[1].argop != nfs_opnum4.OP_LOOKUP
                                || ops[2].argop != nfs_opnum4.OP_LOOKUP
                                || ops[3].argop != nfs_opnum4.OP_READ)
                            {
                                throw new InvalidOperationException("BuildReadOps must produce PUTROOTFH + 2 LOOKUPs + READ for /data/file.bin.");
                            }

                            READ4args? readArgs = ops[3].opread;
                            if (readArgs?.stateid is null
                                || readArgs.stateid.seqid != 7
                                || readArgs.stateid.other is not byte[] otherBytes
                                || otherBytes.Length != 12
                                || readArgs.offset?.Value != 4096
                                || readArgs.count?.Value != 8192)
                            {
                                throw new InvalidOperationException("READ args must carry the supplied stateid (seqid 7 + 12-byte other), offset 4096, and count 8192.");
                            }

                            // Root path must produce just PUTROOTFH + READ (server will return ISDIR).
                            IReadOnlyList<nfs_argop4> rootReadOps = OpenNfsV41PathOperations.BuildReadOps(
                                string.Empty,
                                stateid,
                                offset: 0,
                                count: 1);
                            if (rootReadOps.Count != 2
                                || rootReadOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootReadOps[1].argop != nfs_opnum4.OP_READ)
                            {
                                throw new InvalidOperationException("Empty path with BuildReadOps must produce exactly PUTROOTFH + READ.");
                            }

                            // Null stateid must throw.
                            bool stateidGuardFired = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildReadOps("/x", stateid: null!, offset: 0, count: 0);
                            }
                            catch (ArgumentNullException)
                            {
                                stateidGuardFired = true;
                            }

                            if (!stateidGuardFired)
                            {
                                throw new InvalidOperationException("BuildReadOps must reject a null stateid argument.");
                            }

                            _ = cancellationToken;
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV41Suites",
                        caseId: "PathOperationsBuildsReaddirCompound",
                        displayName: "OpenNfsV41PathOperations builds a PUTROOTFH + LOOKUP-walk + READDIR COMPOUND op sequence with the supplied cookie, verifier, and attribute mask",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            byte[] cookieVerifier = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };
                            bitmap4 mask = OpenNfsV41PathOperations.BuildStatLikeAttributeMask();

                            IReadOnlyList<nfs_argop4> ops = OpenNfsV41PathOperations.BuildReaddirOps(
                                "/dir",
                                cookie: 1024,
                                cookieVerifier: cookieVerifier,
                                dircount: 4096,
                                maxcount: 8192,
                                attributeMask: mask);

                            if (ops.Count != 3
                                || ops[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || ops[1].argop != nfs_opnum4.OP_LOOKUP
                                || ops[2].argop != nfs_opnum4.OP_READDIR)
                            {
                                throw new InvalidOperationException("BuildReaddirOps must produce PUTROOTFH + 1 LOOKUP + READDIR for /dir.");
                            }

                            READDIR4args? readdirArgs = ops[2].opreaddir;
                            if (readdirArgs?.cookie?.Value != 1024
                                || readdirArgs.cookieverf?.Value is not byte[] verifierBytes
                                || !verifierBytes.SequenceEqual(cookieVerifier)
                                || readdirArgs.dircount?.Value != 4096
                                || readdirArgs.maxcount?.Value != 8192
                                || readdirArgs.attr_request is null
                                || readdirArgs.attr_request.Value is not uint[] attrWords
                                || !attrWords.SequenceEqual(mask.Value!))
                            {
                                throw new InvalidOperationException("READDIR args must carry cookie 1024, the supplied 8-byte verifier, dircount 4096, maxcount 8192, and the supplied attribute mask.");
                            }

                            // The verifier must be defensively copied: mutating the caller's array
                            // must not change the produced op.
                            cookieVerifier[0] = 0x00;
                            if (readdirArgs.cookieverf!.Value is not byte[] preservedBytes
                                || preservedBytes[0] != 0xAA)
                            {
                                throw new InvalidOperationException("READDIR cookie verifier must be defensively copied so caller mutation cannot tamper with the produced op.");
                            }

                            // A non-8-byte verifier must be rejected.
                            bool verifierLengthGuardFired = false;
                            try
                            {
                                _ = OpenNfsV41PathOperations.BuildReaddirOps(
                                    "/dir",
                                    cookie: 0,
                                    cookieVerifier: new byte[7],
                                    dircount: 1,
                                    maxcount: 1,
                                    attributeMask: mask);
                            }
                            catch (ArgumentException)
                            {
                                verifierLengthGuardFired = true;
                            }

                            if (!verifierLengthGuardFired)
                            {
                                throw new InvalidOperationException("BuildReaddirOps must reject a cookie verifier whose length is not exactly 8 bytes.");
                            }

                            // Root path must produce just PUTROOTFH + READDIR.
                            IReadOnlyList<nfs_argop4> rootOps = OpenNfsV41PathOperations.BuildReaddirOps(
                                string.Empty,
                                cookie: 0,
                                cookieVerifier: new byte[8],
                                dircount: 1,
                                maxcount: 1,
                                attributeMask: mask);
                            if (rootOps.Count != 2
                                || rootOps[0].argop != nfs_opnum4.OP_PUTROOTFH
                                || rootOps[1].argop != nfs_opnum4.OP_READDIR)
                            {
                                throw new InvalidOperationException("Empty path with BuildReaddirOps must produce exactly PUTROOTFH + READDIR.");
                            }

                            _ = cancellationToken;
                            return Task.CompletedTask;
                        }),
                });
        }

        private static bool IsAttributeBitSet(bitmap4 mask, ulong attributeIdentifier)
        {
            uint[]? words = mask.Value;
            if (words is null)
            {
                return false;
            }

            int wordIndex = (int)(attributeIdentifier / 32);
            int bitIndex = (int)(attributeIdentifier % 32);
            if (wordIndex >= words.Length)
            {
                return false;
            }

            return (words[wordIndex] & (1u << bitIndex)) != 0;
        }

        private static utf8str_cs MakeTag(string text)
        {
            return new utf8str_cs
            {
                Value = new utf8string { Value = System.Text.Encoding.UTF8.GetBytes(text) },
            };
        }

        private static CB_COMPOUND4args BuildCallbackCompound(
            byte[] sessionId,
            uint slotId,
            uint sequenceId,
            bool cacheThis,
            IReadOnlyList<nfs_cb_argop4> ops)
        {
            nfs_cb_argop4 sequenceOp = new nfs_cb_argop4
            {
                argop = (uint)nfs_cb_opnum4.OP_CB_SEQUENCE,
                opcbsequence = new CB_SEQUENCE4args
                {
                    csa_sessionid = new sessionid4 { Value = sessionId },
                    csa_sequenceid = new sequenceid4 { Value = sequenceId },
                    csa_slotid = new slotid4 { Value = slotId },
                    csa_highest_slotid = new slotid4 { Value = slotId },
                    csa_cachethis = cacheThis,
                    csa_referring_call_lists = Array.Empty<referring_call_list4>(),
                },
            };

            nfs_cb_argop4[] argarray = new nfs_cb_argop4[ops.Count + 1];
            argarray[0] = sequenceOp;
            for (int index = 0; index < ops.Count; index++)
            {
                argarray[index + 1] = ops[index];
            }

            return new CB_COMPOUND4args
            {
                tag = MakeTag("cb-test"),
                minorversion = 1,
                callback_ident = 0,
                argarray = argarray,
            };
        }

        private static void EnsureCategory(nfsstat4 status, OpenNfsErrorCategory expected)
        {
            OpenNfsErrorCategory actual = OpenNfsV41StatusException.ClassifyCategory(status);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    "Expected " + status + " to map to " + expected + " but got " + actual + ".");
            }
        }

        private static void EnsurePublicType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!type.IsPublic)
            {
                throw new InvalidOperationException(
                    "Type " + type.FullName + " must be publicly visible on the OpenNFS.Client surface.");
            }
        }

        private static void EnsurePublicMethod(Type type, string methodName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            System.Reflection.MethodInfo[] candidates = type.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Name == methodName)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "Type " + type.FullName + " must expose a public method named '" + methodName + "'.");
        }

        private static void EnsurePublicProperty(Type type, string propertyName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            System.Reflection.PropertyInfo? property = type.GetProperty(propertyName,
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            if (property is null)
            {
                throw new InvalidOperationException(
                    "Type " + type.FullName + " must expose a public property named '" + propertyName + "'.");
            }
        }

        private static Nfs41Session BuildSessionForCallback(byte[] sessionIdBytes, uint cbProgram)
        {
            Nfs41ChannelAttributes channelAttrs = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 65536,
                maximumResponseSize: 65536,
                maximumCachedResponseSize: 0,
                maximumOperations: 16,
                maximumRequests: 4);

            return new Nfs41Session(
                sessionId: new Nfs41SessionId(sessionIdBytes),
                clientId: 1ul,
                foreChannelAttributes: channelAttrs,
                backChannelAttributes: channelAttrs,
                callbackProgramNumber: cbProgram,
                initialConnectionIdentity: "127.0.0.1:cb-test");
        }

        private sealed class DefaultCallbackHandler : OpenNfsV41CallbackHandler
        {
        }

        private sealed class RecordingCallbackHandler : OpenNfsV41CallbackHandler
        {
            private readonly nfsstat4 recallStatus;
            private int recallCount;

            internal RecordingCallbackHandler(nfsstat4 recallStatus)
            {
                this.recallStatus = recallStatus;
            }

            internal int RecallCount => recallCount;

            public override ValueTask<CB_RECALL4res> OnRecallAsync(
                CB_RECALL4args arguments,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref recallCount);
                return ValueTask.FromResult(new CB_RECALL4res { status = recallStatus });
            }
        }

        private static OpenNfsV41ClientOwner BuildClientOwner(byte verifier, byte ownerSeed)
        {
            return new OpenNfsV41ClientOwner(
                verifier: new byte[] { verifier, verifier, verifier, verifier, verifier, verifier, verifier, verifier },
                ownerId: new byte[] { ownerSeed, 0xCC, 0x55, 0xEE });
        }

        private static async Task<OpenNfsV41ClientSession> EstablishClientSessionAsync(
            int nfsPort,
            byte ownerSeed,
            CancellationToken cancellationToken)
        {
            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                endpoint: new IPEndPoint(IPAddress.Loopback, nfsPort),
                clientOwner: BuildClientOwner(verifier: 0xE0, ownerSeed: ownerSeed));
            options.RequestedSlots = 4;
            options.CallTimeout = TimeSpan.FromSeconds(15);
            return await OpenNfsV41ClientSession.EstablishAsync(options, cancellationToken).ConfigureAwait(false);
        }

        private static Nfs41SessionOperationProcessor CreateProcessorWithScope(string scope)
        {
            Nfs41ServerOwner serverOwner = new Nfs41ServerOwner(
                minorId: 1,
                majorId: new byte[] { 0x4F, 0x70, 0x65, 0x6E, 0x4E, 0x46, 0x53 });
            Nfs41ChannelAttributes maximums = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 1024 * 1024,
                maximumResponseSize: 1024 * 1024,
                maximumCachedResponseSize: 64 * 1024,
                maximumOperations: 64,
                maximumRequests: 64);
            Nfs41ServerConfiguration configuration = new Nfs41ServerConfiguration(
                serverOwner,
                serverScope: System.Text.Encoding.UTF8.GetBytes(scope),
                foreChannelMaximums: maximums,
                backChannelMaximums: maximums);
            Nfs41ClientRegistry clientRegistry = new Nfs41ClientRegistry();
            Nfs41SessionRegistry sessionRegistry = new Nfs41SessionRegistry();

            int allocatedCount = 0;
            byte[] AllocateSessionId()
            {
                byte[] bytes = new byte[Nfs41SessionId.Length];
                int counter = Interlocked.Increment(ref allocatedCount);
                bytes[Nfs41SessionId.Length - 1] = (byte)(counter & 0xFF);
                bytes[Nfs41SessionId.Length - 2] = (byte)((counter >> 8) & 0xFF);
                return bytes;
            }

            return new Nfs41SessionOperationProcessor(configuration, clientRegistry, sessionRegistry, AllocateSessionId);
        }

        private static async Task<COMPOUND4res> DispatchCompoundAsync(
            Nfs41CompoundService service,
            COMPOUND4args arguments,
            string connectionIdentity,
            uint xid,
            CancellationToken cancellationToken)
        {
            byte[] replyBytes = await DispatchCompoundRawAsync(service, arguments, connectionIdentity, xid, cancellationToken).ConfigureAwait(false);
            XdrReader reader = new XdrReader(replyBytes);
            COMPOUND4res value = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        private static async Task<byte[]> DispatchCompoundRawAsync(
            Nfs41CompoundService service,
            COMPOUND4args arguments,
            string connectionIdentity,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            RpcMessageEnvelope reply = await service.DispatchAsync(request, connectionIdentity, cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Wire-level COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            return reply.ProcedurePayload.ToArray();
        }

        private static async Task<COMPOUND4res> SendCompoundOverTransportAsync(
            RpcTcpTransport transport,
            COMPOUND4args arguments,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Real-network COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            XdrReader reader = new XdrReader(reply.ProcedurePayload);
            COMPOUND4res value = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        private static async Task<byte[]> SendCompoundRawOverTransportAsync(
            RpcTcpTransport transport,
            COMPOUND4args arguments,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Real-network COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            return reply.ProcedurePayload.ToArray();
        }

        private static async Task<byte[]> EstablishSessionOverWireAsync(
            Nfs41CompoundService service,
            string connectionIdentity,
            byte ownerSeed,
            uint requestedSlots,
            CancellationToken cancellationToken)
        {
            COMPOUND4args bootstrap = new COMPOUND4args
            {
                tag = MakeTag("est-bootstrap"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC3, ownerSeed) },
                },
            };
            COMPOUND4res bootstrapResponse = await DispatchCompoundAsync(service, bootstrap, connectionIdentity, xid: 100, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(bootstrapResponse.status, "wire EXCHANGE_ID");
            ulong clientId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_clientid!.Value;
            uint sequenceId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_sequenceid!.Value;

            COMPOUND4args createSession = new COMPOUND4args
            {
                tag = MakeTag("est-create"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_CREATE_SESSION,
                        opcreate_session = BuildCreateSessionArguments(clientId, sequenceId, requestedSlots),
                    },
                },
            };
            COMPOUND4res createSessionResponse = await DispatchCompoundAsync(service, createSession, connectionIdentity, xid: 101, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(createSessionResponse.status, "wire CREATE_SESSION");
            return createSessionResponse.resarray![0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;
        }

        private static Nfs41SessionOperationProcessor CreateProcessor()
        {
            Nfs41ServerOwner serverOwner = new Nfs41ServerOwner(
                minorId: 1,
                majorId: new byte[] { 0x4F, 0x70, 0x65, 0x6E, 0x4E, 0x46, 0x53 });
            Nfs41ChannelAttributes maximums = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 1024 * 1024,
                maximumResponseSize: 1024 * 1024,
                maximumCachedResponseSize: 64 * 1024,
                maximumOperations: 64,
                maximumRequests: 64);
            Nfs41ServerConfiguration configuration = new Nfs41ServerConfiguration(
                serverOwner,
                serverScope: new byte[] { 0x6F, 0x70, 0x65, 0x6E, 0x6E, 0x66, 0x73 },
                foreChannelMaximums: maximums,
                backChannelMaximums: maximums);
            Nfs41ClientRegistry clientRegistry = new Nfs41ClientRegistry();
            Nfs41SessionRegistry sessionRegistry = new Nfs41SessionRegistry();

            int allocatedCount = 0;
            byte[] AllocateSessionId()
            {
                byte[] bytes = new byte[Nfs41SessionId.Length];
                int counter = Interlocked.Increment(ref allocatedCount);
                bytes[Nfs41SessionId.Length - 1] = (byte)(counter & 0xFF);
                bytes[Nfs41SessionId.Length - 2] = (byte)((counter >> 8) & 0xFF);
                return bytes;
            }

            return new Nfs41SessionOperationProcessor(configuration, clientRegistry, sessionRegistry, AllocateSessionId);
        }

        private static EXCHANGE_ID4args BuildExchangeIdArguments(byte verifier, byte ownerSeed)
        {
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4 { Value = new byte[] { verifier, verifier, verifier, verifier, verifier, verifier, verifier, verifier } },
                    co_ownerid = new byte[] { ownerSeed, 0x10, 0x20, 0x30 },
                },
                eia_flags = 0,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        private static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            channel_attrs4 attrs = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0 },
                ca_maxrequestsize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new count4 { Value = 64 * 1024 },
                ca_maxoperations = new count4 { Value = 16 },
                ca_maxrequests = new count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4 { Value = clientId },
                csa_sequence = new sequenceid4 { Value = sequenceId },
                csa_flags = 0,
                csa_fore_chan_attrs = attrs,
                csa_back_chan_attrs = attrs,
                csa_cb_program = 0x40000000u,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        private static SEQUENCE4args BuildSequenceArguments(byte[] sessionIdBytes, uint slotId, uint sequenceId, bool cacheThis, uint highestSlotId)
        {
            return new SEQUENCE4args
            {
                sa_sessionid = new sessionid4 { Value = sessionIdBytes },
                sa_sequenceid = new sequenceid4 { Value = sequenceId },
                sa_slotid = new slotid4 { Value = slotId },
                sa_highest_slotid = new slotid4 { Value = highestSlotId },
                sa_cachethis = cacheThis,
            };
        }

        private static byte[] EstablishSession(
            Nfs41SessionOperationProcessor processor,
            Nfs41OperationContext context,
            byte ownerSeed,
            uint requestedSlots)
        {
            EXCHANGE_ID4res exchangeResult = processor.ProcessExchangeId(BuildExchangeIdArguments(0xC0, ownerSeed));
            EnsureSuccess(exchangeResult.eir_status, nameof(processor.ProcessExchangeId));
            ulong clientId = exchangeResult.eir_resok4!.eir_clientid!.Value;
            uint sequenceId = exchangeResult.eir_resok4!.eir_sequenceid!.Value;

            CREATE_SESSION4res createResult = processor.ProcessCreateSession(
                BuildCreateSessionArguments(clientId, sequenceId, requestedSlots),
                context);
            EnsureSuccess(createResult.csr_status, nameof(processor.ProcessCreateSession));
            return createResult.csr_resok4!.csr_sessionid!.Value!;
        }

        private static void EnsureSuccess(nfsstat4? status, string operationName)
        {
            if (status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to return NFS4_OK but received " + status?.ToString() + ".");
            }
        }
    }
}
