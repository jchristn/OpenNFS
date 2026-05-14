namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedMutationDecodeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientGroupedSuites",
                    caseId: "MutationApisDecodeTypedReplies",
                    displayName: "Grouped mutation APIs decode typed NFSv3 replies across success and failure variants",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        OpenNfsClient client = new OpenNfsClientBuilder().Build();

                        OpenNfsV3CreatePathResult createFileResult = client.Directories.ReadCreateFileV3Result(
                            EncodeAcceptedReply(
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
                                                data = new byte[] { 0x51, 0x52 },
                                            },
                                        },
                                        obj_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 12, 8001),
                                        dir_wcc = CreateWeakCacheConsistency(10, 12, 8100),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (!createFileResult.IsSuccess
                            || !createFileResult.ObjectFileHandle.Span.SequenceEqual(new byte[] { 0x51, 0x52 })
                            || createFileResult.ObjectAttributes?.FileType != OpenNfsV3FileType.RegularFile
                            || createFileResult.DirectoryWeakCacheConsistency?.After is null)
                        {
                            throw new InvalidOperationException("Expected grouped CREATE decoding to surface the created filehandle, attributes, and parent weak-cache-consistency data.");
                        }

                        OpenNfsV3CreatePathResult createDirectoryFailure = client.Directories.ReadCreateDirectoryV3Result(
                            EncodeAcceptedReply(
                                new MKDIR3res
                                {
                                    status = nfsstat3.NFS3ERR_EXIST,
                                    resfail = new MKDIR3resfail
                                    {
                                        dir_wcc = CreateWeakCacheConsistency(3, 3, 8200),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (createDirectoryFailure.IsSuccess
                            || createDirectoryFailure.Status != OpenNfsV3Status.AlreadyExists
                            || createDirectoryFailure.DirectoryWeakCacheConsistency?.Before is null)
                        {
                            throw new InvalidOperationException("Expected grouped MKDIR decoding to surface failure status and parent weak-cache-consistency data.");
                        }

                        OpenNfsV3DirectoryMutationResult removeFileResult = client.Directories.ReadRemoveFileV3Result(
                            EncodeAcceptedReply(
                                new REMOVE3res
                                {
                                    status = nfsstat3.NFS3_OK,
                                    resok = new REMOVE3resok
                                    {
                                        dir_wcc = CreateWeakCacheConsistency(12, 8, 8300),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (!removeFileResult.IsSuccess
                            || removeFileResult.DirectoryWeakCacheConsistency?.After is null)
                        {
                            throw new InvalidOperationException("Expected grouped REMOVE decoding to surface successful parent weak-cache-consistency data.");
                        }

                        OpenNfsV3DirectoryMutationResult removeDirectoryFailure = client.Directories.ReadRemoveDirectoryV3Result(
                            EncodeAcceptedReply(
                                new RMDIR3res
                                {
                                    status = nfsstat3.NFS3ERR_NOTEMPTY,
                                    resfail = new RMDIR3resfail
                                    {
                                        dir_wcc = CreateWeakCacheConsistency(4, 4, 8400),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (removeDirectoryFailure.IsSuccess
                            || removeDirectoryFailure.Status != OpenNfsV3Status.NotEmpty
                            || removeDirectoryFailure.DirectoryWeakCacheConsistency?.Before is null)
                        {
                            throw new InvalidOperationException("Expected grouped RMDIR decoding to surface failure status and parent weak-cache-consistency data.");
                        }

                        OpenNfsV3RenameResult renameResult = client.Directories.ReadRenameV3Result(
                            EncodeAcceptedReply(
                                new RENAME3res
                                {
                                    status = nfsstat3.NFS3_OK,
                                    resok = new RENAME3resok
                                    {
                                        fromdir_wcc = CreateWeakCacheConsistency(5, 4, 8500),
                                        todir_wcc = CreateWeakCacheConsistency(6, 7, 8501),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (!renameResult.IsSuccess
                            || renameResult.SourceDirectoryWeakCacheConsistency?.Before is null
                            || renameResult.DestinationDirectoryWeakCacheConsistency?.After is null)
                        {
                            throw new InvalidOperationException("Expected grouped RENAME decoding to surface weak-cache-consistency data for both parent directories.");
                        }

                        OpenNfsV3CreatePathResult symbolicLinkResult = client.Directories.ReadCreateSymbolicLinkV3Result(
                            EncodeAcceptedReply(
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
                                                data = new byte[] { 0x61, 0x62, 0x63 },
                                            },
                                        },
                                        obj_attributes = CreatePostOperationAttributes(ftype3.NF3LNK, 18, 8600),
                                        dir_wcc = CreateWeakCacheConsistency(7, 8, 8601),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (!symbolicLinkResult.IsSuccess
                            || symbolicLinkResult.ObjectAttributes?.FileType != OpenNfsV3FileType.SymbolicLink
                            || !symbolicLinkResult.ObjectFileHandle.Span.SequenceEqual(new byte[] { 0x61, 0x62, 0x63 }))
                        {
                            throw new InvalidOperationException("Expected grouped SYMLINK decoding to surface the created symbolic-link handle and attributes.");
                        }

                        OpenNfsV3LinkResult hardLinkFailure = client.Directories.ReadCreateHardLinkV3Result(
                            EncodeAcceptedReply(
                                new LINK3res
                                {
                                    status = nfsstat3.NFS3ERR_EXIST,
                                    resfail = new LINK3resfail
                                    {
                                        file_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 18, 8700),
                                        linkdir_wcc = CreateWeakCacheConsistency(8, 8, 8701),
                                    },
                                },
                                static (value, writer) => value.WriteTo(writer)));

                        if (hardLinkFailure.IsSuccess
                            || hardLinkFailure.Status != OpenNfsV3Status.AlreadyExists
                            || hardLinkFailure.FileAttributes?.FileType != OpenNfsV3FileType.RegularFile
                            || hardLinkFailure.LinkDirectoryWeakCacheConsistency?.Before is null)
                        {
                            throw new InvalidOperationException("Expected grouped LINK decoding to surface failure status, file attributes, and destination-directory weak-cache-consistency data.");
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
