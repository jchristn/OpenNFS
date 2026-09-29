namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Mounted-session regression cases that rewrite NFSv3 traffic between the client and a live in-process server to force
    /// short writes, write-verifier changes, restrictive FSINFO limits, and missing READDIRPLUS support.
    /// </summary>
    internal static class MountSessionFaultInjectionSupport
    {
        private const uint ReadProcedure = 6;
        private const uint WriteProcedure = 7;
        private const uint LookupProcedure = 3;
        private const uint ReaddirProcedure = 16;
        private const uint ReaddirPlusProcedure = 17;
        private const uint FsInfoProcedure = 19;
        private const uint CommitProcedure = 21;

        internal static async Task ExecuteShortWritesAreContinuedAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteCall = (procedure, payload) =>
                {
                    if (procedure != WriteProcedure)
                    {
                        return payload;
                    }

                    WRITE3args arguments = Decode(payload, WRITE3args.ReadFrom);
                    byte[] data = arguments.data ?? Array.Empty<byte>();
                    if (data.Length < 2)
                    {
                        return payload;
                    }

                    byte[] half = data.AsSpan(0, data.Length / 2).ToArray();
                    arguments.data = half;
                    arguments.count = new count3 { Value = new uint32 { Value = (uint)half.Length } };
                    return Encode(arguments.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(200_000, seed: 11);
            await session.Files.WriteAllBytesAsync("/short-writes.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] hostBytes = await File.ReadAllBytesAsync(server.GetHostPath("/short-writes.bin"), cancellationToken).ConfigureAwait(false);
            Require(hostBytes.AsSpan().SequenceEqual(payload), "Expected short WRITE replies to be continued until the whole payload was stored.");

            int writeCalls = executor.CallsFor(WriteProcedure).Count;
            int chunkCount = (payload.Length + (64 * 1024) - 1) / (64 * 1024);
            Require(writeCalls > chunkCount * 2, "Expected every chunk to need several WRITE calls after short writes, but observed only " + writeCalls + " WRITE calls.");

            using MemoryStream source = new MemoryStream(payload);
            await session.Files.WriteAsync("/short-writes-stream.bin", source, length: null, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            byte[] streamBytes = await File.ReadAllBytesAsync(server.GetHostPath("/short-writes-stream.bin"), cancellationToken).ConfigureAwait(false);
            Require(streamBytes.AsSpan().SequenceEqual(payload), "Expected stream writes to continue short writes as well.");
        }

        internal static async Task ExecuteZeroProgressWriteIsRejectedAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != WriteProcedure)
                    {
                        return reply;
                    }

                    WRITE3res result = Decode(reply, WRITE3res.ReadFrom);
                    if (result.resok is not null)
                    {
                        result.resok.count = new count3 { Value = new uint32 { Value = 0 } };
                    }

                    return Encode(result.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            OpenNfsClientProtocolException exception = await ExpectAsync<OpenNfsClientProtocolException>(
                () => session.Files.WriteAllBytesAsync("/zero-progress.bin", new byte[] { 1, 2, 3 }, OpenNfsWriteStability.FileSync, cancellationToken)).ConfigureAwait(false);
            Require(exception.Message.Contains("no progress", StringComparison.Ordinal), "Expected a zero-byte WRITE acknowledgement to be reported as a zero-progress protocol error.");
        }

        internal static async Task ExecuteUnstableWritesAreCommittedAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor();
            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(150_000, seed: 12);
            foreach (OpenNfsWriteStability stability in new[] { OpenNfsWriteStability.Unstable, OpenNfsWriteStability.DataSync, OpenNfsWriteStability.FileSync })
            {
                int commitsBefore = executor.CallsFor(CommitProcedure).Count;
                await session.Files.WriteAllBytesAsync("/commit-" + stability + ".bin", payload, stability, cancellationToken).ConfigureAwait(false);
                int commits = executor.CallsFor(CommitProcedure).Count - commitsBefore;

                if (stability == OpenNfsWriteStability.FileSync)
                {
                    Require(commits == 0, "Expected FILE_SYNC writes acknowledged as FILE_SYNC not to issue COMMIT, but observed " + commits + ".");
                }
                else
                {
                    Require(commits == 1, "Expected " + stability + " writes to be followed by exactly one COMMIT, but observed " + commits + ".");
                }

                commitsBefore = executor.CallsFor(CommitProcedure).Count;
                await session.Files.WriteAsync("/commit-stream-" + stability + ".bin", new MemoryStream(payload), length: null, stability, cancellationToken).ConfigureAwait(false);
                commits = executor.CallsFor(CommitProcedure).Count - commitsBefore;
                Require(
                    stability == OpenNfsWriteStability.FileSync ? commits == 0 : commits == 1,
                    "Expected stream writes with " + stability + " to follow the same COMMIT rules, but observed " + commits + " COMMIT call(s).");
            }
        }

        internal static async Task ExecuteVerifierChangeTriggersRewriteAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            int commitRewrites = 0;
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != CommitProcedure || Interlocked.Increment(ref commitRewrites) > 1)
                    {
                        return reply;
                    }

                    COMMIT3res result = Decode(reply, COMMIT3res.ReadFrom);
                    if (result.resok?.verf?.Value is byte[] verifier)
                    {
                        verifier[0] ^= 0xFF;
                    }

                    return Encode(result.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(100_000, seed: 13);
            await session.Files.WriteAllBytesAsync("/verifier.bin", payload, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<InterceptedNfsCall> calls = executor.Calls;
            int commitIndex = calls.ToList().FindIndex(static call => call.Procedure == CommitProcedure);
            Require(commitIndex >= 0, "Expected an UNSTABLE write to issue COMMIT.");
            List<WRITE3args> rewrites = calls.Skip(commitIndex + 1)
                .Where(static call => call.Procedure == WriteProcedure)
                .Select(static call => Decode(call.Payload, WRITE3args.ReadFrom))
                .ToList();
            Require(rewrites.Count >= 2, "Expected a verifier change at COMMIT to trigger a rewrite of the payload.");
            Require(rewrites.All(static write => write.stable == stable_how.FILE_SYNC), "Expected the rewrite after a verifier change to use FILE_SYNC stability.");
            byte[] hostBytes = await File.ReadAllBytesAsync(server.GetHostPath("/verifier.bin"), cancellationToken).ConfigureAwait(false);
            Require(hostBytes.AsSpan().SequenceEqual(payload), "Expected the rewritten file to contain the payload.");

            Interlocked.Exchange(ref commitRewrites, 0);
            OpenNfsClientIoException nonSeekableFailure = await ExpectAsync<OpenNfsClientIoException>(
                () => session.Files.WriteAsync("/verifier-stream.bin", new NonSeekableReadStream(payload, 4096), null, OpenNfsWriteStability.Unstable, cancellationToken)).ConfigureAwait(false);
            Require(nonSeekableFailure.Message.Contains("verifier changed", StringComparison.Ordinal), "Expected a clear verifier-change failure for a non-replayable stream.");

            Interlocked.Exchange(ref commitRewrites, 0);
            await session.Files.WriteAsync("/verifier-seekable.bin", new MemoryStream(payload), null, OpenNfsWriteStability.DataSync, cancellationToken).ConfigureAwait(false);
            byte[] seekableBytes = await File.ReadAllBytesAsync(server.GetHostPath("/verifier-seekable.bin"), cancellationToken).ConfigureAwait(false);
            Require(seekableBytes.AsSpan().SequenceEqual(payload), "Expected a seekable stream to be replayed after a verifier change.");
        }

        internal static async Task ExecuteWriteVerifierChangeBetweenWritesTriggersRewriteAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            int writeReplies = 0;
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != WriteProcedure || Interlocked.Increment(ref writeReplies) != 2)
                    {
                        return reply;
                    }

                    WRITE3res result = Decode(reply, WRITE3res.ReadFrom);
                    if (result.resok?.verf?.Value is byte[] verifier)
                    {
                        verifier[7] ^= 0x5A;
                    }

                    return Encode(result.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(200_000, seed: 14);
            await session.Files.WriteAllBytesAsync("/verifier-between.bin", payload, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);

            int fileSyncWrites = executor.CallsFor(WriteProcedure)
                .Select(static call => Decode(call.Payload, WRITE3args.ReadFrom))
                .Count(static write => write.stable == stable_how.FILE_SYNC);
            Require(fileSyncWrites >= 4, "Expected a verifier change between UNSTABLE writes to trigger a FILE_SYNC rewrite.");
            Require(
                (await File.ReadAllBytesAsync(server.GetHostPath("/verifier-between.bin"), cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(payload),
                "Expected the file contents to be correct after the rewrite.");
        }

        internal static async Task ExecutePersistentVerifierChangeFailsClearlyAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure == WriteProcedure)
                    {
                        WRITE3res writeResult = Decode(reply, WRITE3res.ReadFrom);
                        if (writeResult.resok is not null)
                        {
                            writeResult.resok.committed = stable_how.UNSTABLE;
                        }

                        return Encode(writeResult.WriteTo);
                    }

                    if (procedure == CommitProcedure)
                    {
                        COMMIT3res commitResult = Decode(reply, COMMIT3res.ReadFrom);
                        if (commitResult.resok?.verf is not null)
                        {
                            commitResult.resok.verf.Value = Guid.NewGuid().ToByteArray().AsSpan(0, 8).ToArray();
                        }

                        return Encode(commitResult.WriteTo);
                    }

                    return reply;
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            OpenNfsClientIoException exception = await ExpectAsync<OpenNfsClientIoException>(
                () => session.Files.WriteAllBytesAsync("/verifier-persistent.bin", CreatePayload(10_000, seed: 15), OpenNfsWriteStability.Unstable, cancellationToken)).ConfigureAwait(false);
            Require(exception.Message.Contains("durable", StringComparison.Ordinal), "Expected a persistent verifier change to fail with a clear durability message.");
            Require(executor.CallsFor(CommitProcedure).Count == 2, "Expected exactly one COMMIT for the original write and one for the FILE_SYNC rewrite.");
        }

        internal static async Task ExecuteTransferSizesFollowFsInfoAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != FsInfoProcedure)
                    {
                        return reply;
                    }

                    FSINFO3res result = Decode(reply, FSINFO3res.ReadFrom);
                    if (result.resok is not null)
                    {
                        result.resok.wtmax = new uint32 { Value = 4096 };
                        result.resok.wtpref = new uint32 { Value = 8192 };
                        result.resok.rtmax = new uint32 { Value = 1024 };
                        result.resok.rtpref = new uint32 { Value = 1024 };
                    }

                    return Encode(result.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(20_000, seed: 16);
            await session.Files.WriteAllBytesAsync("/small-transfers.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAsync("/small-transfers-stream.bin", new MemoryStream(payload), null, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] readBack = await session.Files.ReadAllBytesAsync("/small-transfers.bin", cancellationToken).ConfigureAwait(false);
            byte[] ranged = await session.Files.ReadAsync("/small-transfers-stream.bin", 0, payload.Length, cancellationToken).ConfigureAwait(false);
            await using (Stream stream = await session.Files.OpenReadAsync("/small-transfers.bin", cancellationToken).ConfigureAwait(false))
            {
                using MemoryStream copy = new MemoryStream();
                await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
                Require(copy.ToArray().AsSpan().SequenceEqual(payload), "Expected the read stream to honor the small FSINFO read size and still reproduce the file.");
            }

            Require(readBack.AsSpan().SequenceEqual(payload) && ranged.AsSpan().SequenceEqual(payload), "Expected small transfer sizes to preserve the payload.");
            Require(executor.CallsFor(FsInfoProcedure).Count == 1, "Expected FSINFO to be requested once and cached for the mounted session.");

            List<int> writeSizes = executor.CallsFor(WriteProcedure).Select(static call => (Decode(call.Payload, WRITE3args.ReadFrom).data ?? Array.Empty<byte>()).Length).ToList();
            List<uint> readCounts = executor.CallsFor(ReadProcedure).Select(static call => Decode(call.Payload, READ3args.ReadFrom).count?.Value?.Value ?? 0U).ToList();
            Require(writeSizes.Count >= 10 && writeSizes.Max() == 4096, "Expected WRITE chunks to be bounded by the advertised wtmax of 4096 bytes (max observed " + writeSizes.Max() + ").");
            Require(readCounts.Count >= 40 && readCounts.Max() <= 1024, "Expected READ counts to be bounded by the advertised rtmax of 1024 bytes (max observed " + readCounts.Max() + ").");
        }

        internal static async Task ExecuteTransferSizesFallBackWhenFsInfoFailsAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != FsInfoProcedure)
                    {
                        return reply;
                    }

                    return Encode(new FSINFO3res
                    {
                        status = nfsstat3.NFS3ERR_SERVERFAULT,
                        resfail = new FSINFO3resfail
                        {
                            obj_attributes = new post_op_attr { attributes_follow = false },
                        },
                    }.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            byte[] payload = CreatePayload(200_000, seed: 17);
            await session.Files.WriteAllBytesAsync("/fallback-transfers.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] readBack = await session.Files.ReadAllBytesAsync("/fallback-transfers.bin", cancellationToken).ConfigureAwait(false);
            Require(readBack.AsSpan().SequenceEqual(payload), "Expected the 64 KiB fallback transfer size to preserve the payload.");

            int maximumWrite = executor.CallsFor(WriteProcedure).Max(static call => (Decode(call.Payload, WRITE3args.ReadFrom).data ?? Array.Empty<byte>()).Length);
            uint maximumRead = executor.CallsFor(ReadProcedure).Max(static call => Decode(call.Payload, READ3args.ReadFrom).count?.Value?.Value ?? 0U);
            Require(maximumWrite == 64 * 1024 && maximumRead == 64 * 1024, "Expected a failed FSINFO to fall back to 64 KiB transfers (observed write " + maximumWrite + ", read " + maximumRead + ").");
        }

        internal static async Task ExecuteListWithAttributesFallsBackToReaddirAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            SeedDirectory(server, "/listing", 40);

            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != ReaddirPlusProcedure)
                    {
                        return reply;
                    }

                    return Encode(new READDIRPLUS3res
                    {
                        status = nfsstat3.NFS3ERR_NOTSUPP,
                        resfail = new READDIRPLUS3resfail
                        {
                            dir_attributes = new post_op_attr { attributes_follow = false },
                        },
                    }.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries = await session.Directories.ListWithAttributesAsync("/listing", cancellationToken).ConfigureAwait(false);
            Require(entries.Count == 41, "Expected the READDIR fallback to return all 41 entries but found " + entries.Count + ".");
            Require(entries.All(static entry => entry.Attributes is not null && entry.FileHandle.Length > 0), "Expected the READDIR fallback to supply attributes and handles for every entry.");
            Require(executor.CallsFor(ReaddirProcedure).Count >= 1, "Expected the fallback to issue READDIR.");
            Require(executor.CallsFor(LookupProcedure).Count >= 41, "Expected the fallback to LOOKUP every entry.");
            Require(entries.Single(static entry => entry.Name == "nested").Attributes!.FileType == OpenNfsV3FileType.Directory, "Expected the fallback to report directory types.");
        }

        internal static async Task ExecuteListWithAttributesCompletesMissingAttributesAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            SeedDirectory(server, "/listing", 25);

            int strippedReplies = 0;
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                RewriteReply = (procedure, call, reply) =>
                {
                    if (procedure != ReaddirPlusProcedure)
                    {
                        return reply;
                    }

                    READDIRPLUS3res result = Decode(reply, READDIRPLUS3res.ReadFrom);
                    entryplus3? entry = result.resok?.reply?.entries?.Value;
                    int index = 0;
                    while (entry is not null)
                    {
                        entry.name_attributes = new post_op_attr { attributes_follow = false };
                        if (index % 2 == 0)
                        {
                            entry.name_handle = new post_op_fh3 { handle_follows = false };
                        }

                        entry = entry.nextentry?.Value;
                        index++;
                    }

                    Interlocked.Increment(ref strippedReplies);
                    return Encode(result.WriteTo);
                },
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries = await session.Directories.ListWithAttributesAsync("/listing", cancellationToken).ConfigureAwait(false);
            Require(strippedReplies > 0, "Expected the READDIRPLUS replies to be rewritten.");
            Require(entries.Count == 26, "Expected all 26 entries but found " + entries.Count + ".");
            Require(entries.All(static entry => entry.Attributes is not null && entry.FileHandle.Length > 0), "Expected entries without post-op attributes or handles to be completed.");
            Require(entries.Where(static entry => entry.Name.StartsWith("file-", StringComparison.Ordinal)).All(static entry => entry.Attributes!.SizeBytes == 5), "Expected completed attributes to carry real file sizes.");
        }

        internal static async Task ExecuteMidFlightCancellationSurfacesOperationCanceledAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(server.GetHostPath("/slow.bin"), CreatePayload(1000, seed: 18), cancellationToken).ConfigureAwait(false);

            InterceptingRpcExecutor executor = new InterceptingRpcExecutor
            {
                BeforeSendAsync = (procedure, token) => procedure == ReadProcedure
                    ? Task.Delay(Timeout.Infinite, token)
                    : Task.CompletedTask,
            };

            await using OpenNfsClient client = await ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await ExpectAsync<OperationCanceledException>(() => session.Files.ReadAsync("/slow.bin", 0, 10, timeout.Token)).ConfigureAwait(false);

            using CancellationTokenSource streamTimeout = new CancellationTokenSource();
            await using Stream stream = await session.Files.OpenReadAsync("/slow.bin", streamTimeout.Token).ConfigureAwait(false);
            streamTimeout.CancelAfter(TimeSpan.FromMilliseconds(300));
            await ExpectAsync<OperationCanceledException>(async () => _ = await stream.ReadAsync(new byte[10], streamTimeout.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        internal static async Task ExecuteSetAttributesWithoutHostCapabilityAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(
                (builder, sourceRoot) => builder.UseFileSystem(new DictionaryNfsFileSystem(
                    new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                    {
                        [sourceRoot] = NfsPathKind.Directory,
                    },
                    new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase))),
                cancellationToken).ConfigureAwait(false);

            Require(!server.Application.Server.Capabilities.Supports(NfsCapabilityKind.AttributeMutation), "Expected an in-memory host without INfsAttributeMutation not to advertise the capability.");

            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            await using (session.ConfigureAwait(false))
            {
                await session.Files.WriteAllBytesAsync("/legacy.txt", Encoding.UTF8.GetBytes("0123456789"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync("/legacy.txt", cancellationToken).ConfigureAwait(false)) == "0123456789", "Expected create-if-missing writes to work without the SETATTR capability.");

                await session.Files.WriteAllBytesAsync("/legacy.txt", Encoding.UTF8.GetBytes("ABCDEFGHIJ"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync("/legacy.txt", cancellationToken).ConfigureAwait(false)) == "ABCDEFGHIJ", "Expected same-length overwrites not to need SETATTR.");

                await ExpectStatusAsync(
                    () => session.Files.WriteAllBytesAsync("/legacy.txt", Encoding.UTF8.GetBytes("abc"), OpenNfsWriteStability.FileSync, cancellationToken),
                    OpenNfsV3Status.NotSupported).ConfigureAwait(false);
                await ExpectStatusAsync(() => session.Metadata.SetLengthAsync("/legacy.txt", 1, cancellationToken), OpenNfsV3Status.NotSupported).ConfigureAwait(false);

                byte[] handle = await session.ResolvePathHandleOrThrowAsync("/legacy.txt", "test", cancellationToken).ConfigureAwait(false);
                OpenNfsV3SetAttributesResult modeResult = await client.Files.SetAttributesV3Async(handle, new OpenNfsV3SetAttributes(mode: 420), null, cancellationToken).ConfigureAwait(false);
                Require(modeResult.Status == OpenNfsV3Status.NotSupported && modeResult.Wcc is not null, "Expected hosts without the capability to keep rejecting mode changes with NFS3ERR_NOTSUPP and wcc data.");
            }
        }

        internal static async Task<OpenNfsClient> ConnectInterceptedAsync(
            EphemeralOpenNfsServer server,
            InterceptingRpcExecutor executor,
            CancellationToken cancellationToken)
        {
            OpenNfsClient client = InterceptingRpcExecutor.CreateClient(server.CreateClientBuilder().BuildSettings(), executor);
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return client;
        }

        internal static T Decode<T>(ReadOnlyMemory<byte> payload, Func<XdrReader, T> read)
        {
            XdrReader reader = new XdrReader(payload);
            T value = read(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        internal static ReadOnlyMemory<byte> Encode(Action<XdrWriter> write)
        {
            XdrWriter writer = new XdrWriter();
            write(writer);
            return writer.ToArray();
        }

        private static void SeedDirectory(EphemeralOpenNfsServer server, string exportRelativeDirectory, int fileCount)
        {
            string directory = server.GetHostPath(exportRelativeDirectory);
            Directory.CreateDirectory(Path.Combine(directory, "nested"));
            for (int index = 0; index < fileCount; index++)
            {
                File.WriteAllText(Path.Combine(directory, "file-" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".txt"), "hello");
            }
        }
    }
}
