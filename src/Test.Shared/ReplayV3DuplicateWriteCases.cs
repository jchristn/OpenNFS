namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ReplaySuiteSupport;

    /// <summary>
    /// NFSv3 duplicate-write replay cases.
    /// </summary>
    internal static class ReplayV3DuplicateWriteCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
