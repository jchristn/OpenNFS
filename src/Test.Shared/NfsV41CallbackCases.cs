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
    /// Callback-channel and backchannel dispatcher NFSv4.1 suites.
    /// </summary>
    internal static class NfsV41CallbackCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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

            };
        }
    }
}
