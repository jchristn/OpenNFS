namespace Test.Shared
{
    using System;
    using System.IO;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using static Test.Shared.ClientGroupedSuiteSupport;

    internal static class ClientGroupedFoundationDecodeSupport
    {
        internal static void VerifyTypedReplyDecoding()
        {
            OpenNfsClient client = new OpenNfsClientBuilder().Build();
            VerifyAttributeAndAccessDecoding(client);
            VerifyLookupAndReadDecoding(client);
            VerifyDirectoryEnumerationDecoding(client);
            VerifyFileSystemMetadataDecoding(client);
            VerifyWriteCommitAndRpcFailureDecoding(client);
        }

        private static void VerifyAttributeAndAccessDecoding(OpenNfsClient client)
        {
            OpenNfsV3GetAttributesResult getattrResult = client.Files.ReadGetAttributesV3Result(
                EncodeAcceptedReply(
                    new GETATTR3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new GETATTR3resok
                        {
                            obj_attributes = CreateAttributes(ftype3.NF3REG, 42, 1001),
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!getattrResult.IsSuccess
                || getattrResult.Attributes is null
                || getattrResult.Attributes.FileType != OpenNfsV3FileType.RegularFile
                || getattrResult.Attributes.SizeBytes != 42
                || getattrResult.Attributes.FileId != 1001)
            {
                throw new InvalidOperationException("Expected grouped GETATTR decoding to surface typed NFSv3 attributes.");
            }

            OpenNfsV3AccessResult accessResult = client.Files.ReadAccessV3Result(
                EncodeAcceptedReply(
                    new ACCESS3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new ACCESS3resok
                        {
                            obj_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 42, 1001),
                            access = new uint32
                            {
                                Value = (uint)(Nfs3Constants.ACCESS3_READ | Nfs3Constants.ACCESS3_LOOKUP),
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!accessResult.IsSuccess
                || accessResult.ObjectAttributes is null
                || accessResult.AccessMask != (OpenNfsV3AccessMask.Read | OpenNfsV3AccessMask.Lookup))
            {
                throw new InvalidOperationException("Expected grouped ACCESS decoding to surface typed access flags and post-operation attributes.");
            }
        }

        private static void VerifyLookupAndReadDecoding(OpenNfsClient client)
        {
            OpenNfsV3LookupResult lookupResult = client.Directories.ReadLookupV3Result(
                EncodeAcceptedReply(
                    new LOOKUP3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new LOOKUP3resok
                        {
                            @object = new nfs_fh3
                            {
                                data = new byte[] { 0xAA, 0xBB, 0xCC },
                            },
                            obj_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 512, 2001),
                            dir_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 1024, 2000),
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!lookupResult.IsSuccess
                || !lookupResult.ObjectFileHandle.Span.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC })
                || lookupResult.ObjectAttributes is null
                || lookupResult.ObjectAttributes.FileType != OpenNfsV3FileType.Directory)
            {
                throw new InvalidOperationException("Expected grouped LOOKUP decoding to surface the child filehandle and attributes.");
            }

            OpenNfsV3ReadResult readResult = client.Files.ReadReadV3Result(
                EncodeAcceptedReply(
                    new READ3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new READ3resok
                        {
                            file_attributes = CreatePostOperationAttributes(ftype3.NF3REG, 4096, 3001),
                            count = new count3
                            {
                                Value = new uint32
                                {
                                    Value = 3,
                                },
                            },
                            eof = false,
                            data = new byte[] { 0x10, 0x20, 0x30 },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!readResult.IsSuccess
                || readResult.FileAttributes is null
                || readResult.Count != 3
                || readResult.EndOfFile
                || !readResult.Data.Span.SequenceEqual(new byte[] { 0x10, 0x20, 0x30 }))
            {
                throw new InvalidOperationException("Expected grouped READ decoding to surface typed file data and attributes.");
            }

            OpenNfsV3ReadLinkResult readLinkResult = client.Files.ReadReadLinkV3Result(
                EncodeAcceptedReply(
                    new READLINK3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new READLINK3resok
                        {
                            symlink_attributes = CreatePostOperationAttributes(ftype3.NF3LNK, 12, 4001),
                            data = new nfspath3
                            {
                                Value = "../target",
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!readLinkResult.IsSuccess
                || readLinkResult.SymbolicLinkAttributes is null
                || !string.Equals(readLinkResult.TargetPath, "../target", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected grouped READLINK decoding to surface the link target path.");
            }
        }

        private static void VerifyDirectoryEnumerationDecoding(OpenNfsClient client)
        {
            OpenNfsV3ReadDirectoryResult readdirResult = client.Directories.ReadReadDirectoryV3Result(
                EncodeAcceptedReply(
                    new READDIR3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new READDIR3resok
                        {
                            dir_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 2048, 5000),
                            cookieverf = new cookieverf3
                            {
                                Value = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                            },
                            reply = new dirlist3
                            {
                                entries = CreateEntryList(
                                    CreateDirectoryEntrySeed(11UL, "alpha", 101UL),
                                    CreateDirectoryEntrySeed(12UL, "beta", 102UL)),
                                eof = true,
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!readdirResult.IsSuccess
                || readdirResult.DirectoryAttributes is null
                || !readdirResult.CookieVerifier.Span.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
                || readdirResult.Entries.Count != 2
                || !string.Equals(readdirResult.Entries[1].Name, "beta", StringComparison.Ordinal)
                || !readdirResult.EndOfFile)
            {
                throw new InvalidOperationException("Expected grouped READDIR decoding to surface typed directory entries and the cookie verifier.");
            }

            OpenNfsV3ReadDirectoryPlusResult readdirPlusResult = client.Directories.ReadReadDirectoryPlusV3Result(
                EncodeAcceptedReply(
                    new READDIRPLUS3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new READDIRPLUS3resok
                        {
                            dir_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 2048, 5000),
                            cookieverf = new cookieverf3
                            {
                                Value = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 },
                            },
                            reply = new dirlistplus3
                            {
                                entries = CreateEntryPlusList(
                                    CreateDirectoryEntryPlusSeed(21UL, "docs", 201UL, ftype3.NF3DIR, 4096UL, new byte[] { 0x99, 0x88 })),
                                eof = false,
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            OpenNfsV3Attributes? firstReadDirectoryPlusAttributes =
                readdirPlusResult.Entries.Count == 0 ? null : readdirPlusResult.Entries[0].Attributes;

            if (!readdirPlusResult.IsSuccess
                || readdirPlusResult.Entries.Count != 1
                || firstReadDirectoryPlusAttributes is null
                || firstReadDirectoryPlusAttributes.FileType != OpenNfsV3FileType.Directory
                || !readdirPlusResult.Entries[0].FileHandle.Span.SequenceEqual(new byte[] { 0x99, 0x88 })
                || readdirPlusResult.EndOfFile)
            {
                throw new InvalidOperationException("Expected grouped READDIRPLUS decoding to surface typed child attributes and handles.");
            }
        }

        private static void VerifyFileSystemMetadataDecoding(OpenNfsClient client)
        {
            OpenNfsV3FileSystemStatusResult fsStatResult = client.Files.ReadFileSystemStatusV3Result(
                EncodeAcceptedReply(
                    new FSSTAT3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new FSSTAT3resok
                        {
                            obj_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 2048, 5000),
                            tbytes = CreateSize(1_000_000),
                            fbytes = CreateSize(750_000),
                            abytes = CreateSize(700_000),
                            tfiles = CreateSize(1000),
                            ffiles = CreateSize(900),
                            afiles = CreateSize(850),
                            invarsec = new uint32
                            {
                                Value = 30,
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!fsStatResult.IsSuccess
                || fsStatResult.TotalBytes != 1_000_000
                || fsStatResult.AvailableFiles != 850
                || fsStatResult.InvariantSeconds != 30)
            {
                throw new InvalidOperationException("Expected grouped FSSTAT decoding to surface typed filesystem capacity values.");
            }

            OpenNfsV3FileSystemInfoResult fsInfoResult = client.Files.ReadFileSystemInfoV3Result(
                EncodeAcceptedReply(
                    new FSINFO3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new FSINFO3resok
                        {
                            obj_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 2048, 5000),
                            rtmax = new uint32 { Value = 65536 },
                            rtpref = new uint32 { Value = 32768 },
                            rtmult = new uint32 { Value = 4096 },
                            wtmax = new uint32 { Value = 65536 },
                            wtpref = new uint32 { Value = 32768 },
                            wtmult = new uint32 { Value = 4096 },
                            dtpref = new uint32 { Value = 8192 },
                            maxfilesize = CreateSize(1_099_511_627_776UL),
                            time_delta = CreateTime(2, 50),
                            properties = new uint32
                            {
                                Value = (uint)(Nfs3Constants.FSF3_LINK | Nfs3Constants.FSF3_SYMLINK),
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!fsInfoResult.IsSuccess
                || fsInfoResult.ReadMaxBytes != 65536
                || fsInfoResult.MaxFileSizeBytes != 1_099_511_627_776UL
                || fsInfoResult.TimeDelta is null
                || fsInfoResult.TimeDelta.Seconds != 2
                || fsInfoResult.Properties != (OpenNfsV3FileSystemProperties.SupportsHardLinks | OpenNfsV3FileSystemProperties.SupportsSymbolicLinks))
            {
                throw new InvalidOperationException("Expected grouped FSINFO decoding to surface typed transfer and capability information.");
            }

            OpenNfsV3PathConfigurationResult pathConfResult = client.Files.ReadPathConfigurationV3Result(
                EncodeAcceptedReply(
                    new PATHCONF3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new PATHCONF3resok
                        {
                            obj_attributes = CreatePostOperationAttributes(ftype3.NF3DIR, 2048, 5000),
                            linkmax = new uint32 { Value = 256 },
                            name_max = new uint32 { Value = 255 },
                            no_trunc = true,
                            chown_restricted = true,
                            case_insensitive = false,
                            case_preserving = true,
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!pathConfResult.IsSuccess
                || pathConfResult.MaximumLinks != 256
                || pathConfResult.MaximumNameLength != 255
                || !pathConfResult.NoTruncation
                || !pathConfResult.ChownRestricted
                || pathConfResult.CaseInsensitive
                || !pathConfResult.CasePreserving)
            {
                throw new InvalidOperationException("Expected grouped PATHCONF decoding to surface typed path-configuration metadata.");
            }
        }

        private static void VerifyWriteCommitAndRpcFailureDecoding(OpenNfsClient client)
        {
            OpenNfsV3WriteResult writeResult = client.Files.ReadWriteV3Result(
                EncodeAcceptedReply(
                    new WRITE3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new WRITE3resok
                        {
                            file_wcc = CreateWeakCacheConsistency(10, 13, 6001),
                            count = new count3
                            {
                                Value = new uint32
                                {
                                    Value = 3,
                                },
                            },
                            committed = stable_how.FILE_SYNC,
                            verf = new writeverf3
                            {
                                Value = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 },
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!writeResult.IsSuccess
                || writeResult.FileWeakCacheConsistency?.Before is null
                || writeResult.FileWeakCacheConsistency.After is null
                || writeResult.Count != 3
                || writeResult.CommittedStability != OpenNfsWriteStability.FileSync
                || !writeResult.Verifier.Span.SequenceEqual(new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 }))
            {
                throw new InvalidOperationException("Expected grouped WRITE decoding to surface weak-cache-consistency data, committed stability, and the write verifier.");
            }

            OpenNfsV3CommitResult commitResult = client.Files.ReadCommitV3Result(
                EncodeAcceptedReply(
                    new COMMIT3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new COMMIT3resok
                        {
                            file_wcc = CreateWeakCacheConsistency(13, 13, 6001),
                            verf = new writeverf3
                            {
                                Value = new byte[] { 2, 3, 4, 5, 6, 7, 8, 9 },
                            },
                        },
                    },
                    static (value, writer) => value.WriteTo(writer)));

            if (!commitResult.IsSuccess
                || commitResult.FileWeakCacheConsistency?.After is null
                || !commitResult.Verifier.Span.SequenceEqual(new byte[] { 2, 3, 4, 5, 6, 7, 8, 9 }))
            {
                throw new InvalidOperationException("Expected grouped COMMIT decoding to surface weak-cache-consistency data and the write verifier.");
            }

            try
            {
                client.Files.ReadGetAttributesV3Result(
                    RpcMessageCodec.Encode(
                        RpcMessageFactory.CreateRejectedReply(
                            xid: 0x01020304,
                            status: RpcGenerated.reject_stat.AUTH_ERROR,
                            authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED)));
                throw new InvalidOperationException("Expected grouped NFSv3 decode helpers to reject denied RPC replies.");
            }
            catch (InvalidDataException exception)
            {
                if (!exception.Message.Contains("AUTH_ERROR", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected grouped NFSv3 decode failures to explain the rejected RPC status.");
                }
            }
        }
    }
}
