namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ReplaySuiteSupport;

    /// <summary>
    /// Disconnect/reconnect replay cases.
    /// </summary>
    internal static class ReplayReconnectCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ReplaySuites",
                    caseId: "DisconnectReplayRecovery",
                    displayName: "NFSv3 replay recovers a dropped post-mutation reply across reconnect without reapplying the write",
                    tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        DictionaryNfsFileSystem fileSystem = CreateReplayFileSystem();
                        OpenNfsServer server = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .Build();

                        NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/data.bin", @"C:\exports\data.bin"),
                            cancellationToken).ConfigureAwait(false);

                        await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                        await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                            "127.0.0.1",
                            host.NfsPort,
                            FaultInjectingRpcProxyMode.DropFirstReplyAfterForwarding);

                        await using OpenNfsClient client = new OpenNfsClientBuilder()
                            .WithServer("127.0.0.1", proxy.LocalPort)
                            .WithRetryPolicy(new OpenNfsRetryPolicy(maximumAttempts: 2))
                            .Build();
                        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                        OpenNfsV3ProcedureReply reply = await client.ExecuteV3ProcedureAsync(
                            CreateRawWriteRequest(fileHandle, new byte[] { 0x44, 0x55, 0x66 }),
                            OpenNfsOperationIdempotency.Idempotent,
                            cancellationToken).ConfigureAwait(false);
                        WRITE3res replayedWriteResult = Nfs3ProcedurePayloadCodec.ReadPayload(
                            reply.ReadAcceptedSuccessProcedurePayload(),
                            WRITE3res.ReadFrom);

                        if (fileSystem.WriteRequestCount != 1)
                        {
                            throw new InvalidOperationException("Expected dropped-reply retry recovery to replay the cached NFSv3 WRITE reply without reapplying the host mutation.");
                        }

                        if (replayedWriteResult.status != nfsstat3.NFS3_OK
                            || replayedWriteResult.resok?.count?.Value is null
                            || replayedWriteResult.resok.count.Value.Value != 3U)
                        {
                            throw new InvalidOperationException("Expected replay-driven reconnect recovery to preserve the original NFSv3 WRITE success payload.");
                        }

                        RpcCaptureAssertions.AssertCallRoutingSequence(
                            proxy.CapturedClientRequests,
                            (uint)NFS_PROGRAM_Program.Program,
                            (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                            (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE);
                        RpcCaptureAssertions.AssertAuthSysCredential(
                            proxy.CapturedClientRequests[0],
                            TestPrincipalIdentity.CreateDefaultAuthSys());

                        if (proxy.CapturedServerReplies.Count != 2
                            || !proxy.CapturedServerReplies[0].EncodedMessage.AsSpan().SequenceEqual(proxy.CapturedServerReplies[1].EncodedMessage))
                        {
                            throw new InvalidOperationException("Expected reconnect replay recovery to observe byte-stable duplicate WRITE replies from the server.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ReplaySuites",
                    caseId: "DisconnectReplayRequesterIdentityNegative",
                    displayName: "NFSv3 reconnect replay does not collapse the retried write when the requester identity changes",
                    tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        DictionaryNfsFileSystem fileSystem = CreateReplayFileSystem();
                        OpenNfsServer server = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .Build();

                        NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/data.bin", @"C:\exports\data.bin"),
                            cancellationToken).ConfigureAwait(false);

                        await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                        await using FaultInjectingRpcProxy proxy = FaultInjectingRpcProxy.Start(
                            "127.0.0.1",
                            host.NfsPort,
                            FaultInjectingRpcProxyMode.DropFirstReplyAfterForwarding);

                        const uint xid = 0x40404040;
                        RpcMessageEnvelope firstCall = CreateWriteCall(
                            xid,
                            fileHandle,
                            "client-disconnect-a",
                            new byte[] { 0x77, 0x88, 0x99 });
                        RpcMessageEnvelope secondCall = CreateWriteCall(
                            xid,
                            fileHandle,
                            "client-disconnect-b",
                            new byte[] { 0x77, 0x88, 0x99 });

                        using (TcpClient firstClient = new TcpClient())
                        {
                            await firstClient.ConnectAsync("127.0.0.1", proxy.LocalPort, cancellationToken).ConfigureAwait(false);
                            using NetworkStream firstStream = firstClient.GetStream();
                            RpcTcpTransport firstTransport = CreateClientTransport(firstStream);
                            await firstTransport.SendAsync(firstCall, cancellationToken).ConfigureAwait(false);

                            try
                            {
                                _ = await firstTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected the first replay-recovery attempt to lose its reply after the server processed the write.");
                            }
                            catch (EndOfStreamException)
                            {
                            }
                            catch (IOException)
                            {
                            }
                        }

                        using (TcpClient secondClient = new TcpClient())
                        {
                            await secondClient.ConnectAsync("127.0.0.1", proxy.LocalPort, cancellationToken).ConfigureAwait(false);
                            using NetworkStream secondStream = secondClient.GetStream();
                            RpcTcpTransport secondTransport = CreateClientTransport(secondStream);
                            await secondTransport.SendAsync(secondCall, cancellationToken).ConfigureAwait(false);
                            RpcMessageEnvelope secondReply = await secondTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                            WRITE3res secondWriteResult = ReadAcceptedSuccessReply(secondReply, WRITE3res.ReadFrom);

                            if (secondWriteResult.status != nfsstat3.NFS3_OK
                                || secondWriteResult.resok?.count?.Value is null
                                || secondWriteResult.resok.count.Value.Value != 3U)
                            {
                                throw new InvalidOperationException("Expected the retried WRITE with a changed requester identity to complete successfully rather than replaying a malformed result.");
                            }
                        }

                        if (fileSystem.WriteRequestCount != 2)
                        {
                            throw new InvalidOperationException("Expected reconnect replay not to collapse same-XID writes when the AUTH_SYS requester identity changes.");
                        }
                    }),
            };
        }
    }
}
