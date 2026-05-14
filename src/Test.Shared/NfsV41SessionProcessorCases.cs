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
    using static Test.Shared.NfsV41SuiteSupport;

    internal static class NfsV41SessionProcessorCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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
            };
        }
    }
}
