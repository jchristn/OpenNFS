namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedMutationExecutionCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "FileAndDirectoryApisExecuteThroughScriptedRpcExecutor",
                    displayName: "Grouped file and directory APIs execute typed NFSv3 requests through the client retry pipeline",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                        {
                            RpcGenerated.call_body? callBody = request.CallEnvelope.Header.body?.cbody;
                            if (callBody is null)
                            {
                                throw new InvalidOperationException("Expected grouped client execution to emit an RPC call body.");
                            }

                            return callBody.proc switch
                            {
                                1 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new GETATTR3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new GETATTR3resok
                                            {
                                                obj_attributes = CreateAttributes(ftype3.NF3REG, 64, 7001),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                3 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new LOOKUP3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new LOOKUP3resok
                                            {
                                                @object = new nfs_fh3
                                                {
                                                    data = new byte[] { 0x10, 0x11 },
                                                },
                                                obj_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 512, 7101),
                                                dir_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 1024, 7100),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                16 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new READDIR3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new READDIR3resok
                                            {
                                                dir_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 1024, 7100),
                                                cookieverf = new cookieverf3
                                                {
                                                    Value = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                                                },
                                                reply = new dirlist3
                                                {
                                                    entries = CreateEntryList(CreateDirectoryEntrySeed(31UL, "notes", 501UL)),
                                                    eof = true,
                                                },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                7 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new WRITE3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new WRITE3resok
                                            {
                                                file_wcc = CreateWeakCacheConsistency(20, 24, 7201),
                                                count = new count3
                                                {
                                                    Value = new uint32
                                                    {
                                                        Value = 4,
                                                    },
                                                },
                                                committed = stable_how.FILE_SYNC,
                                                verf = new writeverf3
                                                {
                                                    Value = new byte[] { 7, 7, 7, 7, 7, 7, 7, 7 },
                                                },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                _ => throw new InvalidOperationException("Unexpected procedure number " + callBody.proc + " in grouped client execution test."),
                            };
                        });

                        OpenNfsClient client = new OpenNfsClient(
                            new OpenNfsClientSettings(
                                serverHost: "primary.example",
                                serverPort: 2049),
                            rpcExecutor,
                            transportPipeline: null);

                        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                        OpenNfsV3GetAttributesResult getattrResult = await client.Files.GetAttributesV3Async(
                            new byte[] { 0x01, 0x02 },
                            cancellationToken).ConfigureAwait(false);

                        OpenNfsV3LookupResult lookupResult = await client.Directories.LookupV3Async(
                            new byte[] { 0x03, 0x04 },
                            "child",
                            cancellationToken).ConfigureAwait(false);

                        OpenNfsV3ReadDirectoryResult readdirResult = await client.Directories.ReadDirectoryV3Async(
                            new byte[] { 0x05, 0x06 },
                            0,
                            new byte[8],
                            1024,
                            cancellationToken).ConfigureAwait(false);

                        OpenNfsV3WriteResult writeResult = await client.Files.WriteV3Async(
                            new byte[] { 0x07, 0x08 },
                            12,
                            OpenNfsWriteStability.FileSync,
                            new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                            cancellationToken).ConfigureAwait(false);

                        if (!getattrResult.IsSuccess
                            || getattrResult.Attributes?.SizeBytes != 64
                            || !lookupResult.ObjectFileHandle.Span.SequenceEqual(new byte[] { 0x10, 0x11 })
                            || readdirResult.Entries.Count != 1
                            || !string.Equals(readdirResult.Entries[0].Name, "notes", StringComparison.Ordinal)
                            || !writeResult.IsSuccess
                            || writeResult.Count != 4
                            || writeResult.CommittedStability != OpenNfsWriteStability.FileSync)
                        {
                            throw new InvalidOperationException("Expected grouped client execution to decode typed NFSv3 procedure results.");
                        }

                        if (rpcExecutor.Requests.Count != 4)
                        {
                            throw new InvalidOperationException("Expected grouped file and directory execution to issue one RPC call per requested operation.");
                        }

                        if (rpcExecutor.Requests[0].CallEnvelope.Header.body?.cbody?.proc != 1
                            || rpcExecutor.Requests[1].CallEnvelope.Header.body?.cbody?.proc != 3
                            || rpcExecutor.Requests[2].CallEnvelope.Header.body?.cbody?.proc != 16
                            || rpcExecutor.Requests[3].CallEnvelope.Header.body?.cbody?.proc != 7)
                        {
                            throw new InvalidOperationException("Expected grouped file and directory execution to target the expected NFSv3 procedure numbers.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MutationAndAdministrationApisExecuteThroughScriptedRpcExecutor",
                    displayName: "Grouped mutation and administration APIs execute through the client retry pipeline across positive and negative variants",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                        {
                            RpcGenerated.call_body? callBody = request.CallEnvelope.Header.body?.cbody;
                            if (callBody is null)
                            {
                                throw new InvalidOperationException("Expected grouped mutation execution to emit an RPC call body.");
                            }

                            if (callBody.proc == 0)
                            {
                                return Task.FromResult(
                                    RpcMessageFactory.CreateAcceptedReply(
                                        xid: request.CallEnvelope.Header.xid,
                                        status: RpcGenerated.accept_stat.SUCCESS,
                                        verifier: RpcAuthenticationCodec.CreateNone(),
                                        procedurePayload: Array.Empty<byte>()));
                            }

                            return callBody.proc switch
                            {
                                8 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new CREATE3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new CREATE3resok
                                            {
                                                obj = new post_op_fh3
                                                {
                                                    handle_follows = true,
                                                    handle = new nfs_fh3
                                                    {
                                                        data = new byte[] { 0x01, 0x02, 0x03 },
                                                    },
                                                },
                                                obj_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 24, 9001),
                                                dir_wcc = CreateWeakCacheConsistency(20, 24, 9000),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                12 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new REMOVE3res
                                        {
                                            status = nfsstat3.NFS3ERR_NOENT,
                                            resfail = new REMOVE3resfail
                                            {
                                                dir_wcc = CreateWeakCacheConsistency(24, 24, 9000),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                14 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new RENAME3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new RENAME3resok
                                            {
                                                fromdir_wcc = CreateWeakCacheConsistency(24, 16, 9000),
                                                todir_wcc = CreateWeakCacheConsistency(8, 16, 9002),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                10 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new SYMLINK3res
                                        {
                                            status = nfsstat3.NFS3_OK,
                                            resok = new SYMLINK3resok
                                            {
                                                obj = new post_op_fh3
                                                {
                                                    handle_follows = true,
                                                    handle = new nfs_fh3
                                                    {
                                                        data = new byte[] { 0x04, 0x05 },
                                                    },
                                                },
                                                obj_attributes = CreatePostOperationAttributes(ftype3.NF3LNK, 14, 9003),
                                                dir_wcc = CreateWeakCacheConsistency(16, 17, 9002),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                15 => Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new LINK3res
                                        {
                                            status = nfsstat3.NFS3ERR_EXIST,
                                            resfail = new LINK3resfail
                                            {
                                                file_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 24, 9001),
                                                linkdir_wcc = CreateWeakCacheConsistency(17, 17, 9002),
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer))),
                                _ => throw new InvalidOperationException("Unexpected procedure number " + callBody.proc + " in grouped mutation execution test."),
                            };
                        });

                        OpenNfsClient client = new OpenNfsClient(
                            new OpenNfsClientSettings(
                                serverHost: "primary.example",
                                serverPort: 2049),
                            rpcExecutor,
                            transportPipeline: null);

                        await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                        OpenNfsV3CreatePathResult createFileResult = await client.Directories.CreateFileV3Async(
                            new byte[] { 0x01, 0x10 },
                            "draft.txt",
                            failIfExists: true,
                            cancellationToken).ConfigureAwait(false);
                        OpenNfsV3DirectoryMutationResult removeFileResult = await client.Directories.RemoveFileV3Async(
                            new byte[] { 0x02, 0x10 },
                            "missing.txt",
                            cancellationToken).ConfigureAwait(false);
                        OpenNfsV3RenameResult renameResult = await client.Directories.RenameV3Async(
                            new byte[] { 0x03, 0x10 },
                            "old.txt",
                            new byte[] { 0x04, 0x10 },
                            "new.txt",
                            cancellationToken).ConfigureAwait(false);
                        OpenNfsV3CreatePathResult symbolicLinkResult = await client.Directories.CreateSymbolicLinkV3Async(
                            new byte[] { 0x05, 0x10 },
                            "latest",
                            "../releases/current",
                            cancellationToken).ConfigureAwait(false);
                        OpenNfsV3LinkResult hardLinkResult = await client.Directories.CreateHardLinkV3Async(
                            new byte[] { 0x06, 0x10 },
                            new byte[] { 0x07, 0x10 },
                            "draft-link.txt",
                            cancellationToken).ConfigureAwait(false);

                        await client.Administration.PingNfsV3Async(cancellationToken).ConfigureAwait(false);
                        await client.Administration.PingMountV3Async(cancellationToken).ConfigureAwait(false);
                        await client.Administration.PingNlmV4Async(cancellationToken).ConfigureAwait(false);

                        if (!createFileResult.IsSuccess
                            || !createFileResult.ObjectFileHandle.Span.SequenceEqual(new byte[] { 0x01, 0x02, 0x03 })
                            || removeFileResult.IsSuccess
                            || removeFileResult.Status != OpenNfsV3Status.NoEntry
                            || !renameResult.IsSuccess
                            || renameResult.DestinationDirectoryWeakCacheConsistency?.After is null
                            || !symbolicLinkResult.IsSuccess
                            || symbolicLinkResult.ObjectAttributes?.FileType != OpenNfsV3FileType.SymbolicLink
                            || hardLinkResult.IsSuccess
                            || hardLinkResult.Status != OpenNfsV3Status.AlreadyExists)
                        {
                            throw new InvalidOperationException("Expected grouped mutation execution to surface both successful and failed typed NFSv3 mutation results.");
                        }

                        if (rpcExecutor.Requests.Count != 8)
                        {
                            throw new InvalidOperationException("Expected grouped mutation and administration execution to issue one RPC call per requested operation.");
                        }

                        if (rpcExecutor.Requests[0].CallEnvelope.Header.body?.cbody?.proc != 8
                            || rpcExecutor.Requests[1].CallEnvelope.Header.body?.cbody?.proc != 12
                            || rpcExecutor.Requests[2].CallEnvelope.Header.body?.cbody?.proc != 14
                            || rpcExecutor.Requests[3].CallEnvelope.Header.body?.cbody?.proc != 10
                            || rpcExecutor.Requests[4].CallEnvelope.Header.body?.cbody?.proc != 15
                            || rpcExecutor.Requests[5].CallEnvelope.Header.body?.cbody?.prog != 100003
                            || rpcExecutor.Requests[6].CallEnvelope.Header.body?.cbody?.prog != 100005
                            || rpcExecutor.Requests[7].CallEnvelope.Header.body?.cbody?.prog != 100021)
                        {
                            throw new InvalidOperationException("Expected grouped mutation and administration execution to target the expected NFS, MOUNT, and NLM procedures.");
                        }

                        CREATE3args createArguments = CREATE3args.ReadFrom(new XdrReader(rpcExecutor.Requests[0].CallEnvelope.ProcedurePayload));
                        if (!string.Equals(createArguments.where?.name?.Value, "draft.txt", StringComparison.Ordinal)
                            || createArguments.how?.mode != createmode3.GUARDED)
                        {
                            throw new InvalidOperationException("Expected grouped CREATE execution to preserve the guarded-create payload shape on the wire.");
                        }
                    }),
            };
        }
    }
}
