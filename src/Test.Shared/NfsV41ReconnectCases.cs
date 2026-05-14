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

    /// <summary>
    /// Reconnect, rebind, replay-after-reconnect, and establishment-failure NFSv4.1 suites.
    /// </summary>
    internal static class NfsV41ReconnectCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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

            };
        }
    }
}
