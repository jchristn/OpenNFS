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

    internal static class NfsV41SessionIntegrationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
