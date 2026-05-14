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

    internal static class NfsV41SessionWireCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
