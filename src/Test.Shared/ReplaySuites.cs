namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Replay;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering replay-cache and request-correlation primitives.
    /// </summary>
    public static class ReplaySuites
    {
        /// <summary>
        /// Creates the shared replay suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ReplaySuites",
                displayName: "Replay Primitives",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ReplaySuites",
                        caseId: "DuplicateCallReturnsStableReply",
                        displayName: "Replay cache returns a stable reply for duplicate calls",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            DateTimeOffset storedAt = new DateTimeOffset(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);
                            RpcReplayCache<RpcRequestCorrelationKey, RpcMessageEnvelope> replayCache =
                                new RpcReplayCache<RpcRequestCorrelationKey, RpcMessageEnvelope>(TimeSpan.FromMinutes(5));

                            RpcRequestCorrelationKey originalKey = new RpcRequestCorrelationKey(
                                requesterIdentity: "127.0.0.1:1400",
                                transactionId: 0x10101010,
                                programNumber: (uint)NFS_PROGRAM_Program.Program,
                                versionNumber: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ);

                            RpcMessageEnvelope originalReply = RpcMessageFactory.CreateAcceptedReply(
                                xid: 0x10101010,
                                status: OpenNFS.Rpc.Generated.accept_stat.SUCCESS,
                                verifier: RpcAuthenticationCodec.CreateNone(),
                                procedurePayload: new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE });

                            replayCache.Store(originalKey, originalReply, storedAt);

                            RpcRequestCorrelationKey duplicateKey = new RpcRequestCorrelationKey(
                                requesterIdentity: "127.0.0.1:1400",
                                transactionId: 0x10101010,
                                programNumber: (uint)NFS_PROGRAM_Program.Program,
                                versionNumber: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                                procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ);

                            if (!replayCache.TryGet(duplicateKey, storedAt.AddMinutes(1), out RpcMessageEnvelope? cachedReply) || cachedReply is null)
                            {
                                throw new InvalidOperationException("Expected the replay cache to return a cached reply for a duplicate request correlation key.");
                            }

                            byte[] originalReplyBytes = RpcMessageCodec.Encode(originalReply);
                            byte[] cachedReplyBytes = RpcMessageCodec.Encode(cachedReply);
                            if (!originalReplyBytes.SequenceEqual(cachedReplyBytes))
                            {
                                throw new InvalidOperationException("Expected the replay cache to return a byte-stable reply for duplicate requests.");
                            }

                            if (replayCache.Count != 1)
                            {
                                throw new InvalidOperationException("Expected duplicate replay lookup to leave exactly one cache entry.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReplaySuites",
                        caseId: "V3DuplicateWriteIsIdempotentPositive",
                        displayName: "NFSv3 duplicate WRITE replies are replayed byte-for-byte without reapplying the mutation",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = CreateReplayFileSystem();
                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/data.bin", @"C:\exports\data.bin"),
                                cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            RpcMessageEnvelope firstReply = await dispatcher.DispatchAsync(
                                CreateWriteCall(0x20202020, fileHandle, "client-a", new byte[] { 0x41, 0x42, 0x43 }),
                                cancellationToken).ConfigureAwait(false);
                            RpcMessageEnvelope duplicateReply = await dispatcher.DispatchAsync(
                                CreateWriteCall(0x20202020, fileHandle, "client-a", new byte[] { 0x41, 0x42, 0x43 }),
                                cancellationToken).ConfigureAwait(false);

                            if (fileSystem.WriteRequestCount != 1)
                            {
                                throw new InvalidOperationException("Expected duplicate NFSv3 WRITE requests to hit the duplicate-request cache and avoid a second host write.");
                            }

                            if (!RpcMessageCodec.Encode(firstReply).SequenceEqual(RpcMessageCodec.Encode(duplicateReply)))
                            {
                                throw new InvalidOperationException("Expected duplicate NFSv3 WRITE replies to be replayed byte-for-byte.");
                            }

                            WRITE3res duplicateWriteResult = ReadAcceptedSuccessReply(duplicateReply, WRITE3res.ReadFrom);
                            if (duplicateWriteResult.status != nfsstat3.NFS3_OK
                                || duplicateWriteResult.resok?.count?.Value is null
                                || duplicateWriteResult.resok.count.Value.Value != 3U)
                            {
                                throw new InvalidOperationException("Expected the replayed NFSv3 WRITE reply to preserve the original successful write result payload.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ReplaySuites",
                        caseId: "V3DuplicateWriteDistinctRequestsNegative",
                        displayName: "NFSv3 duplicate-request replay does not collapse distinct payloads or distinct requesters",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = CreateReplayFileSystem();
                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/data.bin", @"C:\exports\data.bin"),
                                cancellationToken).ConfigureAwait(false);

                            Nfs3ProcedureDispatcher dispatcher =
                                new Nfs3ProcedureDispatcher(Nfs3ProcedureHandlerFactory.CreateDefault(server));

                            _ = await dispatcher.DispatchAsync(
                                CreateWriteCall(0x30303030, fileHandle, "client-a", new byte[] { 0x11, 0x22 }),
                                cancellationToken).ConfigureAwait(false);
                            _ = await dispatcher.DispatchAsync(
                                CreateWriteCall(0x30303030, fileHandle, "client-a", new byte[] { 0x11, 0x22, 0x33, 0x44 }),
                                cancellationToken).ConfigureAwait(false);
                            _ = await dispatcher.DispatchAsync(
                                CreateWriteCall(0x30303030, fileHandle, "client-b", new byte[] { 0x11, 0x22, 0x33, 0x44 }),
                                cancellationToken).ConfigureAwait(false);

                            if (fileSystem.WriteRequestCount != 3)
                            {
                                throw new InvalidOperationException("Expected the duplicate-request cache not to collapse same-XID writes when the payload or requester differs.");
                            }

                            NfsReadFileResponse readResponse = await fileSystem.ReadFileAsync(
                                new NfsReadFileRequest(@"C:\exports\data.bin", 0, 16, cancellationToken)).ConfigureAwait(false);
                            if (!readResponse.Data.Span.SequenceEqual(new byte[] { 0x11, 0x22, 0x33, 0x44 }))
                            {
                                throw new InvalidOperationException("Expected the final file bytes to reflect the latest non-duplicate NFSv3 WRITE request.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "ReplaySuites",
                        caseId: "ExpiredEntryIsPurgedSafely",
                        displayName: "Replay cache safely purges expired entries",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            DateTimeOffset now = new DateTimeOffset(2026, 4, 27, 12, 30, 0, TimeSpan.Zero);
                            RpcReplayCache<RpcRequestCorrelationKey, string> replayCache =
                                new RpcReplayCache<RpcRequestCorrelationKey, string>(TimeSpan.FromSeconds(30));

                            RpcRequestCorrelationKey expiredKeyOne = new RpcRequestCorrelationKey("client-a", 1, 100003, 3, 6);
                            RpcRequestCorrelationKey expiredKeyTwo = new RpcRequestCorrelationKey("client-b", 2, 100003, 3, 7);
                            RpcRequestCorrelationKey freshKey = new RpcRequestCorrelationKey("client-c", 3, 100003, 3, 8);

                            replayCache.Store(expiredKeyOne, "expired-a", now.AddMinutes(-2));
                            replayCache.Store(expiredKeyTwo, "expired-b", now.AddMinutes(-1));
                            replayCache.Store(freshKey, "fresh", now);

                            int purgedEntryCount = replayCache.PurgeExpired(now);
                            if (purgedEntryCount != 2)
                            {
                                throw new InvalidOperationException("Expected replay-cache purge to remove both expired entries.");
                            }

                            if (replayCache.TryGet(expiredKeyOne, now, out string? expiredValue))
                            {
                                throw new InvalidOperationException("Expected the first expired replay entry to be unavailable after purge, but received '" + expiredValue + "'.");
                            }

                            if (replayCache.TryGet(expiredKeyTwo, now, out expiredValue))
                            {
                                throw new InvalidOperationException("Expected the second expired replay entry to be unavailable after purge, but received '" + expiredValue + "'.");
                            }

                            if (!replayCache.TryGet(freshKey, now.AddSeconds(10), out string? freshValue) || !string.Equals(freshValue, "fresh", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the non-expired replay entry to survive purge and remain retrievable.");
                            }

                            if (replayCache.Count != 1)
                            {
                                throw new InvalidOperationException("Expected exactly one replay-cache entry to remain after purging expired entries.");
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static DictionaryNfsFileSystem CreateReplayFileSystem()
        {
            return new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\data.bin"] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\data.bin"] = Array.Empty<byte>(),
                });
        }

        private static RpcTcpTransport CreateClientTransport(NetworkStream stream)
        {
            return new RpcTcpTransport(
                stream,
                new RpcTransportOptions(
                    timeouts: new RpcTransportTimeouts(
                        readTimeout: TimeSpan.FromSeconds(5),
                        writeTimeout: TimeSpan.FromSeconds(5))));
        }

        private static OpenNfsV3ProcedureRequest CreateRawWriteRequest(NfsFileHandle fileHandle, byte[] data)
        {
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                procedurePayload: WritePayload(
                    new WRITE3args
                    {
                        file = ToWireFileHandle(fileHandle),
                        offset = new offset3
                        {
                            Value = new uint64
                            {
                                Value = 0UL,
                            },
                        },
                        count = new count3
                        {
                            Value = new uint32
                            {
                                Value = (uint)data.Length,
                            },
                        },
                        stable = stable_how.FILE_SYNC,
                        data = data,
                    },
                    static (value, writer) => value.WriteTo(writer)));
        }

        private static RpcMessageEnvelope CreateWriteCall(
            uint xid,
            NfsFileHandle fileHandle,
            string requesterMachineName,
            byte[] data)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS_PROGRAM_Program.Program,
                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                credential: RpcAuthenticationCodec.CreateSystem(
                    new authsys_parms
                    {
                        stamp = xid,
                        machinename = requesterMachineName,
                        uid = 0,
                        gid = 0,
                        gids = Array.Empty<uint>(),
                    }),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: WritePayload(
                    new WRITE3args
                    {
                        file = ToWireFileHandle(fileHandle),
                        offset = new offset3
                        {
                            Value = new uint64
                            {
                                Value = 0UL,
                            },
                        },
                        count = new count3
                        {
                            Value = new uint32
                            {
                                Value = (uint)data.Length,
                            },
                        },
                        stable = stable_how.FILE_SYNC,
                        data = data,
                    },
                    static (value, writer) => value.WriteTo(writer)));
        }

        private static T ReadAcceptedSuccessReply<T>(RpcMessageEnvelope reply, Func<XdrReader, T> readValue)
        {
            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv3 replay result.");
            }

            return Nfs3ProcedurePayloadCodec.ReadPayload(reply.ProcedurePayload, readValue);
        }

        private static nfs_fh3 ToWireFileHandle(NfsFileHandle fileHandle)
        {
            return new nfs_fh3
            {
                data = fileHandle.ToArray(),
            };
        }

        private static byte[] WritePayload<T>(T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return writer.ToArray();
        }
    }
}
