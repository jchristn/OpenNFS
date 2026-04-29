namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the grouped public client convenience APIs.
    /// </summary>
    public static class ClientGroupedSuites
    {
        /// <summary>
        /// Creates the shared grouped-client suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClientGroupedSuites",
                displayName: "Client Grouped Surface Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "GroupedApisAreExposedOnClient",
                        displayName: "Client exposes grouped convenience API categories alongside low-level access",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder().Build();

                            if (client.Directories is null
                                || client.Files is null
                                || client.Exports is null
                                || client.Locks is null
                                || client.Sessions is null
                                || client.Administration is null)
                            {
                                throw new InvalidOperationException("Expected the client to expose all grouped convenience API categories.");
                            }

                            return Task.CompletedTask;

                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "ExportAndAdministrationApisTargetExpectedPrograms",
                        displayName: "Export and administration APIs target the expected ONC RPC programs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("exports.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan mountPlan = await client.Exports.PrepareMountV3Async("/srv/share", cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan exportPlan = await client.Exports.PrepareListExportsV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingPlan = await client.Administration.PreparePingNlmV4Async(cancellationToken).ConfigureAwait(false);

                            if (mountPlan.ProgramNumber != 100005
                                || mountPlan.VersionNumber != 3
                                || mountPlan.ProcedureNumber != 1
                                || !mountPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x0A,
                                    0x2F, 0x73, 0x72, 0x76, 0x2F, 0x73, 0x68, 0x61, 0x72, 0x65,
                                    0x00, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped export API to target the MOUNT v3 program and XDR-encode the export path.");
                            }

                            if (exportPlan.ProgramNumber != 100005
                                || exportPlan.VersionNumber != 3
                                || exportPlan.ProcedureNumber != 5
                                || exportPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped export API to target the MOUNT v3 EXPORT procedure.");
                            }

                            if (pingPlan.ProgramNumber != 100021
                                || pingPlan.VersionNumber != 4
                                || pingPlan.ProcedureNumber != 0
                                || pingPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped administration API to target the NLM v4 NULL procedure.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "DirectoryFileSessionAndLockApisUseExpectedDefaults",
                        displayName: "Directory, file, session, and lock APIs use the expected defaults and payload encoding",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("grouped.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan lookupPlan = await client.Directories.PrepareLookupV3Async(
                                new byte[] { 0xAA, 0xBB, 0xCC },
                                "log",
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readPlan = await client.Files.PrepareReadV3Async(
                                new byte[] { 0x01, 0x02 },
                                0x0102030405060708UL,
                                4096,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan accessPlan = await client.Files.PrepareAccessV3Async(
                                new byte[] { 0x05, 0x06 },
                                OpenNfsV3AccessMask.Read | OpenNfsV3AccessMask.Lookup,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readdirPlusPlan = await client.Directories.PrepareReadDirectoryPlusV3Async(
                                new byte[] { 0x0A, 0x0B, 0x0C, 0x0D },
                                0x1112131415161718UL,
                                new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                                2048,
                                4096,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan readLinkPlan = await client.Files.PrepareReadLinkV3Async(
                                new byte[] { 0x30, 0x31, 0x32 },
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan fsInfoPlan = await client.Files.PrepareFileSystemInfoV3Async(
                                new byte[] { 0x44 },
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsCompoundOperation[] operations =
                            {
                                new OpenNfsCompoundOperation(24, Array.Empty<byte>()),
                            };

                            OpenNfsCompoundPlan sessionPlan = await client.Sessions.PrepareAsync(operations, cancellationToken).ConfigureAwait(false);
                            OpenNfsCompoundPlan lockPlan = await client.Locks.PrepareV4Async(operations, cancellationToken).ConfigureAwait(false);

                            if (lookupPlan.ProgramNumber != 100003
                                || lookupPlan.VersionNumber != 3
                                || lookupPlan.ProcedureNumber != 3
                                || !lookupPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x03,
                                    0xAA, 0xBB, 0xCC, 0x00,
                                    0x00, 0x00, 0x00, 0x03,
                                    0x6C, 0x6F, 0x67, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped directory API to target NFSv3 LOOKUP and XDR-encode the handle/name pair.");
                            }

                            if (readPlan.ProgramNumber != 100003
                                || readPlan.VersionNumber != 3
                                || readPlan.ProcedureNumber != 6
                                || !readPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x02,
                                    0x01, 0x02, 0x00, 0x00,
                                    0x01, 0x02, 0x03, 0x04,
                                    0x05, 0x06, 0x07, 0x08,
                                    0x00, 0x00, 0x10, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 READ and XDR-encode the handle, offset, and count.");
                            }

                            if (accessPlan.ProgramNumber != 100003
                                || accessPlan.VersionNumber != 3
                                || accessPlan.ProcedureNumber != 4
                                || !accessPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x02,
                                    0x05, 0x06, 0x00, 0x00,
                                    0x00, 0x00, 0x00, 0x03,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 ACCESS and XDR-encode the requested access mask.");
                            }

                            if (readdirPlusPlan.ProgramNumber != 100003
                                || readdirPlusPlan.VersionNumber != 3
                                || readdirPlusPlan.ProcedureNumber != 17
                                || !readdirPlusPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x04,
                                    0x0A, 0x0B, 0x0C, 0x0D,
                                    0x11, 0x12, 0x13, 0x14,
                                    0x15, 0x16, 0x17, 0x18,
                                    0x01, 0x02, 0x03, 0x04,
                                    0x05, 0x06, 0x07, 0x08,
                                    0x00, 0x00, 0x08, 0x00,
                                    0x00, 0x00, 0x10, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped directory API to target NFSv3 READDIRPLUS and XDR-encode the cookie, verifier, and count fields.");
                            }

                            if (readLinkPlan.ProgramNumber != 100003
                                || readLinkPlan.VersionNumber != 3
                                || readLinkPlan.ProcedureNumber != 5
                                || !readLinkPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x03,
                                    0x30, 0x31, 0x32, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 READLINK and XDR-encode the symbolic-link filehandle.");
                            }

                            if (fsInfoPlan.ProgramNumber != 100003
                                || fsInfoPlan.VersionNumber != 3
                                || fsInfoPlan.ProcedureNumber != 19
                                || !fsInfoPlan.ProcedurePayload.Span.SequenceEqual(new byte[]
                                {
                                    0x00, 0x00, 0x00, 0x01,
                                    0x44, 0x00, 0x00, 0x00,
                                }))
                            {
                                throw new InvalidOperationException("Expected the grouped file API to target NFSv3 FSINFO and XDR-encode the filesystem root handle.");
                            }

                            if (sessionPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs41
                                || !string.Equals(sessionPlan.Tag, "session", StringComparison.Ordinal)
                                || sessionPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly)
                            {
                                throw new InvalidOperationException("Expected the grouped session API to default to NFSv4.1 and the session tag while preserving TCP-only COMPOUND transport.");
                            }

                            if (lockPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                || !string.Equals(lockPlan.Tag, "lock", StringComparison.Ordinal)
                                || lockPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly)
                            {
                                throw new InvalidOperationException("Expected the grouped lock API to default to NFSv4.0 and the lock tag while preserving TCP-only COMPOUND transport.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "FileAndDirectoryApisDecodeTypedReplies",
                        displayName: "Grouped file and directory APIs decode typed NFSv3 replies without requiring an open client",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder().Build();

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
                                                    (11UL, "alpha", 101UL),
                                                    (12UL, "beta", 102UL)),
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
                                                    (21UL, "docs", 201UL, ftype3.NF3DIR, 4096UL, new byte[] { 0x99, 0x88 })),
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

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MutationAndAdministrationApisUseExpectedProgramsAndPayloadShapes",
                        displayName: "Grouped mutation and administration APIs target the expected programs and payload shapes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("mutations.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan createFilePlan = await client.Directories.PrepareCreateFileV3Async(
                                new byte[] { 0x21, 0x22 },
                                "draft.txt",
                                failIfExists: true,
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan createDirectoryPlan = await client.Directories.PrepareCreateDirectoryV3Async(
                                new byte[] { 0x31, 0x32 },
                                "docs",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan removeFilePlan = await client.Directories.PrepareRemoveFileV3Async(
                                new byte[] { 0x41, 0x42 },
                                "draft.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan removeDirectoryPlan = await client.Directories.PrepareRemoveDirectoryV3Async(
                                new byte[] { 0x51, 0x52 },
                                "docs",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan renamePlan = await client.Directories.PrepareRenameV3Async(
                                new byte[] { 0x61, 0x62 },
                                "old.txt",
                                new byte[] { 0x71, 0x72 },
                                "new.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan symbolicLinkPlan = await client.Directories.PrepareCreateSymbolicLinkV3Async(
                                new byte[] { 0x81, 0x82 },
                                "latest",
                                "../releases/current",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan hardLinkPlan = await client.Directories.PrepareCreateHardLinkV3Async(
                                new byte[] { 0x91, 0x92 },
                                new byte[] { 0xA1, 0xA2 },
                                "draft-link.txt",
                                cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingNfsPlan = await client.Administration.PreparePingNfsV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingMountPlan = await client.Administration.PreparePingMountV3Async(cancellationToken).ConfigureAwait(false);
                            OpenNfsV3ProcedurePlan pingNlmPlan = await client.Administration.PreparePingNlmV4Async(cancellationToken).ConfigureAwait(false);

                            CREATE3args createFileArguments = CREATE3args.ReadFrom(new XdrReader(createFilePlan.ProcedurePayload));
                            MKDIR3args createDirectoryArguments = MKDIR3args.ReadFrom(new XdrReader(createDirectoryPlan.ProcedurePayload));
                            REMOVE3args removeFileArguments = REMOVE3args.ReadFrom(new XdrReader(removeFilePlan.ProcedurePayload));
                            RMDIR3args removeDirectoryArguments = RMDIR3args.ReadFrom(new XdrReader(removeDirectoryPlan.ProcedurePayload));
                            RENAME3args renameArguments = RENAME3args.ReadFrom(new XdrReader(renamePlan.ProcedurePayload));
                            SYMLINK3args symbolicLinkArguments = SYMLINK3args.ReadFrom(new XdrReader(symbolicLinkPlan.ProcedurePayload));
                            LINK3args hardLinkArguments = LINK3args.ReadFrom(new XdrReader(hardLinkPlan.ProcedurePayload));

                            if (createFilePlan.ProgramNumber != 100003
                                || createFilePlan.VersionNumber != 3
                                || createFilePlan.ProcedureNumber != 8
                                || createFileArguments.where?.dir?.data is null
                                || !createFileArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x21, 0x22 })
                                || !string.Equals(createFileArguments.where.name?.Value, "draft.txt", StringComparison.Ordinal)
                                || createFileArguments.how?.mode != createmode3.GUARDED
                                || createFileArguments.how.obj_attributes is null)
                            {
                                throw new InvalidOperationException("Expected the grouped CREATE API to target NFSPROC3_CREATE with a guarded create payload.");
                            }

                            if (createDirectoryPlan.ProcedureNumber != 9
                                || createDirectoryArguments.where?.dir?.data is null
                                || !createDirectoryArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x31, 0x32 })
                                || !string.Equals(createDirectoryArguments.where.name?.Value, "docs", StringComparison.Ordinal)
                                || createDirectoryArguments.attributes is null)
                            {
                                throw new InvalidOperationException("Expected the grouped MKDIR API to target NFSPROC3_MKDIR with the parent handle and directory name.");
                            }

                            if (removeFilePlan.ProcedureNumber != 12
                                || removeFileArguments.@object?.dir?.data is null
                                || !removeFileArguments.@object.dir.data.AsSpan().SequenceEqual(new byte[] { 0x41, 0x42 })
                                || !string.Equals(removeFileArguments.@object.name?.Value, "draft.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped REMOVE API to target NFSPROC3_REMOVE with a diropargs payload.");
                            }

                            if (removeDirectoryPlan.ProcedureNumber != 13
                                || removeDirectoryArguments.@object?.dir?.data is null
                                || !removeDirectoryArguments.@object.dir.data.AsSpan().SequenceEqual(new byte[] { 0x51, 0x52 })
                                || !string.Equals(removeDirectoryArguments.@object.name?.Value, "docs", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped RMDIR API to target NFSPROC3_RMDIR with a diropargs payload.");
                            }

                            if (renamePlan.ProcedureNumber != 14
                                || renameArguments.from?.dir?.data is null
                                || renameArguments.to?.dir?.data is null
                                || !renameArguments.from.dir.data.AsSpan().SequenceEqual(new byte[] { 0x61, 0x62 })
                                || !renameArguments.to.dir.data.AsSpan().SequenceEqual(new byte[] { 0x71, 0x72 })
                                || !string.Equals(renameArguments.from.name?.Value, "old.txt", StringComparison.Ordinal)
                                || !string.Equals(renameArguments.to.name?.Value, "new.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped RENAME API to target NFSPROC3_RENAME with distinct source and destination diropargs payloads.");
                            }

                            if (symbolicLinkPlan.ProcedureNumber != 10
                                || symbolicLinkArguments.where?.dir?.data is null
                                || !symbolicLinkArguments.where.dir.data.AsSpan().SequenceEqual(new byte[] { 0x81, 0x82 })
                                || !string.Equals(symbolicLinkArguments.where.name?.Value, "latest", StringComparison.Ordinal)
                                || symbolicLinkArguments.symlink?.symlink_attributes is null
                                || !string.Equals(symbolicLinkArguments.symlink.symlink_data?.Value, "../releases/current", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped SYMLINK API to target NFSPROC3_SYMLINK with the parent handle, entry name, and link target.");
                            }

                            if (hardLinkPlan.ProcedureNumber != 15
                                || hardLinkArguments.file?.data is null
                                || hardLinkArguments.link?.dir?.data is null
                                || !hardLinkArguments.file.data.AsSpan().SequenceEqual(new byte[] { 0x91, 0x92 })
                                || !hardLinkArguments.link.dir.data.AsSpan().SequenceEqual(new byte[] { 0xA1, 0xA2 })
                                || !string.Equals(hardLinkArguments.link.name?.Value, "draft-link.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped LINK API to target NFSPROC3_LINK with the source filehandle and destination diropargs payload.");
                            }

                            if (pingNfsPlan.ProgramNumber != 100003
                                || pingNfsPlan.VersionNumber != 3
                                || pingNfsPlan.ProcedureNumber != 0
                                || pingNfsPlan.ProcedurePayload.Length != 0
                                || pingMountPlan.ProgramNumber != 100005
                                || pingMountPlan.VersionNumber != 3
                                || pingMountPlan.ProcedureNumber != 0
                                || pingMountPlan.ProcedurePayload.Length != 0
                                || pingNlmPlan.ProgramNumber != 100021
                                || pingNlmPlan.VersionNumber != 4
                                || pingNlmPlan.ProcedureNumber != 0
                                || pingNlmPlan.ProcedurePayload.Length != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped administration APIs to target the correct NULL procedures without payload bytes.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MutationAndAdministrationApisRejectInvalidArgumentsAndUnexpectedVoidPayloads",
                        displayName: "Grouped mutation and administration APIs reject invalid arguments and unexpected NULL reply payloads",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("mutations.example", 2049)
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            try
                            {
                                await client.Directories.PrepareCreateFileV3Async(
                                    Array.Empty<byte>(),
                                    "draft.txt",
                                    failIfExists: true,
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped CREATE planning to reject an empty parent directory filehandle.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "directoryHandle", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped CREATE planning failures to identify the invalid directoryHandle parameter.");
                                }
                            }

                            try
                            {
                                await client.Directories.PrepareRenameV3Async(
                                    new byte[] { 0x11 },
                                    "old.txt",
                                    new byte[] { 0x12 },
                                    " ",
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped RENAME planning to reject a whitespace-only destination entry name.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "entryName", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped RENAME planning failures to identify the invalid entryName parameter.");
                                }
                            }

                            try
                            {
                                await client.Directories.PrepareCreateSymbolicLinkV3Async(
                                    new byte[] { 0x21 },
                                    "latest",
                                    " ",
                                    cancellationToken).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected grouped SYMLINK planning to reject a whitespace-only target path.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!string.Equals(exception.ParamName, "targetPath", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped SYMLINK planning failures to identify the invalid targetPath parameter.");
                                }
                            }

                            client.Administration.ReadPingNfsV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                            client.Administration.ReadPingMountV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                            client.Administration.ReadPingNlmV4Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));

                            try
                            {
                                client.Administration.ReadPingNfsV3Result(EncodeAcceptedSuccessReply(new byte[] { 0x00, 0x00, 0x00, 0x01 }));
                                throw new InvalidOperationException("Expected grouped NULL reply validation to reject unexpected payload bytes.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("must not carry a procedure result payload", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NULL reply validation failures to explain the unexpected payload bytes.");
                                }
                            }

                            try
                            {
                                client.Administration.ReadPingMountV3Result(
                                    RpcMessageCodec.Encode(
                                        RpcMessageFactory.CreateRejectedReply(
                                            xid: 0x01020304,
                                            status: RpcGenerated.reject_stat.AUTH_ERROR,
                                            authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED)));
                                throw new InvalidOperationException("Expected grouped NULL reply validation to reject denied RPC replies.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("AUTH_ERROR", StringComparison.Ordinal)
                                    || !exception.Message.Contains("AUTH_BADCRED", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NULL reply decoding failures to surface both the rejected status and the authentication failure code.");
                                }
                            }

                        }),

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
                                                        entries = CreateEntryList((31UL, "notes", 501UL)),
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

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MountApisDecodeTypedReplies",
                        displayName: "Grouped export APIs decode typed MOUNT v3 replies without requiring an open client",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder().Build();

                            OpenNfsMountV3Result mountResult = client.Exports.ReadMountV3Result(
                                EncodeAcceptedReply(
                                    new mountres3
                                    {
                                        fhs_status = mountstat3.MNT3_OK,
                                        mountinfo = new mountres3_ok
                                        {
                                            fhandle = new fhandle3
                                            {
                                                Value = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
                                            },
                                            auth_flavors = new[] { 0, 1, 6 },
                                        },
                                    },
                                    static (value, writer) => value.WriteTo(writer)));

                            if (!mountResult.IsSuccess
                                || mountResult.Status != OpenNfsMountV3Status.Ok
                                || !mountResult.RootFileHandle.Span.SequenceEqual(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD })
                                || mountResult.SupportedAuthenticationFlavors.Count != 3
                                || mountResult.SupportedAuthenticationFlavors[0] != OpenNfsRpcAuthenticationFlavor.AuthNone
                                || mountResult.SupportedAuthenticationFlavors[1] != OpenNfsRpcAuthenticationFlavor.AuthSys
                                || mountResult.SupportedAuthenticationFlavors[2] != OpenNfsRpcAuthenticationFlavor.RpcSecGss)
                            {
                                throw new InvalidOperationException("Expected the grouped export API to decode a successful MOUNT v3 MNT reply into a typed result model.");
                            }

                            OpenNfsMountV3Result deniedMountResult = client.Exports.ReadMountV3Result(
                                EncodeAcceptedReply(
                                    new mountres3
                                    {
                                        fhs_status = mountstat3.MNT3ERR_ACCES,
                                    },
                                    static (value, writer) => value.WriteTo(writer)));

                            if (deniedMountResult.IsSuccess
                                || deniedMountResult.Status != OpenNfsMountV3Status.AccessDenied
                                || deniedMountResult.RootFileHandle.Length != 0
                                || deniedMountResult.SupportedAuthenticationFlavors.Count != 0)
                            {
                                throw new InvalidOperationException("Expected non-successful MOUNT v3 MNT replies to surface status without success-only payload data.");
                            }

                            IReadOnlyList<OpenNfsExportV3Entry> exports = client.Exports.ReadListExportsV3Result(
                                EncodeAcceptedReply(
                                    new exports
                                    {
                                        Value = new exportnode
                                        {
                                            ex_dir = new dirpath
                                            {
                                                Value = "/srv/share",
                                            },
                                            ex_groups = new groups
                                            {
                                                Value = new groupnode
                                                {
                                                    gr_name = new name
                                                    {
                                                        Value = "ops",
                                                    },
                                                    gr_next = new groups
                                                    {
                                                        Value = new groupnode
                                                        {
                                                            gr_name = new name
                                                            {
                                                                Value = "qa",
                                                            },
                                                            gr_next = new groups(),
                                                        },
                                                    },
                                                },
                                            },
                                            ex_next = new exports
                                            {
                                                Value = new exportnode
                                                {
                                                    ex_dir = new dirpath
                                                    {
                                                        Value = "/srv/public",
                                                    },
                                                    ex_groups = new groups(),
                                                    ex_next = new exports(),
                                                },
                                            },
                                        },
                                    },
                                    static (value, writer) => value.WriteTo(writer)));

                            if (exports.Count != 2
                                || !string.Equals(exports[0].ExportPath, "/srv/share", StringComparison.Ordinal)
                                || exports[0].AuthorizedClientGroups.Count != 2
                                || !string.Equals(exports[0].AuthorizedClientGroups[0], "ops", StringComparison.Ordinal)
                                || !string.Equals(exports[0].AuthorizedClientGroups[1], "qa", StringComparison.Ordinal)
                                || !string.Equals(exports[1].ExportPath, "/srv/public", StringComparison.Ordinal)
                                || exports[1].AuthorizedClientGroups.Count != 0)
                            {
                                throw new InvalidOperationException("Expected the grouped export API to decode typed MOUNT v3 EXPORT entries.");
                            }

                            IReadOnlyList<OpenNfsMountedExportV3Entry> mountedExports = client.Exports.ReadListMountsV3Result(
                                EncodeAcceptedReply(
                                    new mountlist
                                    {
                                        Value = new mountbody
                                        {
                                            ml_hostname = new name
                                            {
                                                Value = "client-a",
                                            },
                                            ml_directory = new dirpath
                                            {
                                                Value = "/srv/share",
                                            },
                                            ml_next = new mountlist
                                            {
                                                Value = new mountbody
                                                {
                                                    ml_hostname = new name
                                                    {
                                                        Value = "client-b",
                                                    },
                                                    ml_directory = new dirpath
                                                    {
                                                        Value = "/srv/public",
                                                    },
                                                    ml_next = new mountlist(),
                                                },
                                            },
                                        },
                                    },
                                    static (value, writer) => value.WriteTo(writer)));

                            if (mountedExports.Count != 2
                                || !string.Equals(mountedExports[0].HostName, "client-a", StringComparison.Ordinal)
                                || !string.Equals(mountedExports[0].ExportPath, "/srv/share", StringComparison.Ordinal)
                                || !string.Equals(mountedExports[1].HostName, "client-b", StringComparison.Ordinal)
                                || !string.Equals(mountedExports[1].ExportPath, "/srv/public", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the grouped export API to decode typed MOUNT v3 DUMP entries.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MountApisRejectRpcFailuresAndUnexpectedVoidPayloads",
                        displayName: "Grouped export reply decoders reject RPC failures and unexpected UMNT payload bytes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder().Build();

                            client.Exports.ReadUnmountV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));
                            client.Exports.ReadUnmountAllV3Result(EncodeAcceptedSuccessReply(Array.Empty<byte>()));

                            try
                            {
                                client.Exports.ReadUnmountV3Result(EncodeAcceptedSuccessReply(new byte[] { 0x00, 0x00, 0x00, 0x01 }));
                                throw new InvalidOperationException("Expected UMNT reply validation to reject unexpected procedure payload bytes.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("must not carry a procedure result payload", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the UMNT reply validation failure to explain the unexpected payload bytes.");
                                }
                            }

                            try
                            {
                                client.Exports.ReadListExportsV3Result(
                                    RpcMessageCodec.Encode(
                                        RpcMessageFactory.CreateRejectedReply(
                                            xid: 0x01020304,
                                            status: RpcGenerated.reject_stat.AUTH_ERROR,
                                            authenticationStatus: RpcGenerated.auth_stat.AUTH_BADCRED)));
                                throw new InvalidOperationException("Expected grouped MOUNT reply decoding to reject denied RPC replies.");
                            }
                            catch (InvalidDataException exception)
                            {
                                if (!exception.Message.Contains("AUTH_ERROR", StringComparison.Ordinal)
                                    || !exception.Message.Contains("AUTH_BADCRED", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected denied RPC replies to surface both the rejected status and authentication failure code.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MountApisExecuteThroughScriptedRpcExecutor",
                        displayName: "Grouped export APIs execute MOUNT v3 requests through the client retry pipeline",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            int executionCount = 0;
                            ScriptedRpcExecutor rpcExecutor = new ScriptedRpcExecutor((request, attempt, token) =>
                            {
                                executionCount++;

                                if (executionCount == 1)
                                {
                                    throw new IOException("Simulated transient transport failure.");
                                }

                                return Task.FromResult(
                                    CreateAcceptedReplyEnvelope(
                                        request.CallEnvelope.Header.xid,
                                        new mountres3
                                        {
                                            fhs_status = mountstat3.MNT3_OK,
                                            mountinfo = new mountres3_ok
                                            {
                                                fhandle = new fhandle3
                                                {
                                                    Value = new byte[] { 0x10, 0x20, 0x30, 0x40 },
                                                },
                                                auth_flavors = new[] { 0, 1 },
                                            },
                                        },
                                        static (value, writer) => value.WriteTo(writer)));
                            });

                            OpenNfsClient client = new OpenNfsClient(
                                new OpenNfsClientSettings(
                                    serverHost: "primary.example",
                                    serverPort: 2049,
                                    alternateEndpoints: new[]
                                    {
                                        new OpenNfsEndpoint("failover.example", 3049),
                                    },
                                    endpointSelectionMode: OpenNfsEndpointSelectionMode.SequentialFailover,
                                    retryPolicy: new OpenNfsRetryPolicy(
                                        maximumAttempts: 2,
                                        initialDelay: TimeSpan.FromMilliseconds(1),
                                        maximumDelay: TimeSpan.FromMilliseconds(1),
                                        useExponentialBackoff: false)),
                                rpcExecutor,
                                transportPipeline: null);

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsMountV3Result result = await client.Exports.MountV3Async("/srv/share", cancellationToken).ConfigureAwait(false);

                            if (!result.IsSuccess
                                || !result.RootFileHandle.Span.SequenceEqual(new byte[] { 0x10, 0x20, 0x30, 0x40 })
                                || result.SupportedAuthenticationFlavors.Count != 2
                                || result.SupportedAuthenticationFlavors[0] != OpenNfsRpcAuthenticationFlavor.AuthNone
                                || result.SupportedAuthenticationFlavors[1] != OpenNfsRpcAuthenticationFlavor.AuthSys)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to decode the successful reply after retry.");
                            }

                            if (rpcExecutor.Attempts.Count != 2
                                || !string.Equals(rpcExecutor.Attempts[0].Endpoint.Host, "primary.example", StringComparison.Ordinal)
                                || rpcExecutor.Attempts[0].Endpoint.Port != 2049
                                || !string.Equals(rpcExecutor.Attempts[1].Endpoint.Host, "failover.example", StringComparison.Ordinal)
                                || rpcExecutor.Attempts[1].Endpoint.Port != 3049)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to advance through the configured client endpoints during retry.");
                            }

                            if (rpcExecutor.Requests.Count != 2)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to issue one RPC call per transport attempt.");
                            }

                            RpcGenerated.call_body? callBody = rpcExecutor.Requests[0].CallEnvelope.Header.body?.cbody;
                            if (callBody is null
                                || callBody.prog != 100005
                                || callBody.vers != 3
                                || callBody.proc != 1
                                || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to target the MOUNT v3 program and issue AUTH_SYS credentials by default.");
                            }

                            XdrReader payloadReader = new XdrReader(rpcExecutor.Requests[0].CallEnvelope.ProcedurePayload);
                            string exportPath = payloadReader.ReadString();
                            payloadReader.EnsureFullyConsumed();

                            if (!string.Equals(exportPath, "/srv/share", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected grouped MOUNT execution to XDR-encode the requested export path.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MountApisExecuteOverLoopbackTcp",
                        displayName: "Grouped export APIs execute against the default TCP RPC executor",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                            listener.Start();

                            try
                            {
                                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                                Task serverTask = Task.Run(
                                    async () =>
                                    {
                                        using TcpClient acceptedClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                                        using NetworkStream stream = acceptedClient.GetStream();
                                        RpcTcpTransport transport = new RpcTcpTransport(
                                            stream,
                                            new RpcTransportOptions(
                                                timeouts: new RpcTransportTimeouts(
                                                    readTimeout: TimeSpan.FromSeconds(5),
                                                    writeTimeout: TimeSpan.FromSeconds(5))));

                                        RpcMessageEnvelope request = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                        RpcGenerated.call_body? callBody = request.Header.body?.cbody;
                                        if (callBody is null
                                            || callBody.prog != 100005
                                            || callBody.vers != 3
                                            || callBody.proc != 5
                                            || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                                        {
                                            throw new InvalidOperationException("Expected the default grouped export execution path to issue a TCP MOUNT v3 EXPORT call.");
                                        }

                                        await transport.SendAsync(
                                            CreateAcceptedReplyEnvelope(
                                                request.Header.xid,
                                                new exports
                                                {
                                                    Value = new exportnode
                                                    {
                                                        ex_dir = new dirpath
                                                        {
                                                            Value = "/srv/share",
                                                        },
                                                        ex_groups = new groups(),
                                                        ex_next = new exports(),
                                                    },
                                                },
                                                static (value, writer) => value.WriteTo(writer)),
                                            cancellationToken).ConfigureAwait(false);
                                    },
                                    cancellationToken);

                                OpenNfsClient client = new OpenNfsClientBuilder()
                                    .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                                    .Build();

                                await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                                IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);

                                if (exports.Count != 1
                                    || !string.Equals(exports[0].ExportPath, "/srv/share", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the default TCP grouped export execution path to decode the loopback server reply.");
                                }

                                await serverTask.ConfigureAwait(false);
                            }
                            finally
                            {
                                listener.Stop();
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientGroupedSuites",
                        caseId: "MountApisFallBackToUdpForV3Policy",
                        displayName: "Grouped export APIs fall back to UDP for v3-era transport policy when TCP fails",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            using UdpClient udpServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                            int port = ((IPEndPoint)udpServer.Client.LocalEndPoint!).Port;

                            Task serverTask = Task.Run(
                                async () =>
                                {
                                    UdpReceiveResult receivedDatagram = await udpServer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                    RpcMessageEnvelope request = RpcMessageCodec.Decode(receivedDatagram.Buffer);
                                    RpcGenerated.call_body? callBody = request.Header.body?.cbody;
                                    if (callBody is null
                                        || callBody.prog != 100005
                                        || callBody.vers != 3
                                        || callBody.proc != 5
                                        || callBody.cred?.flavor != RpcGenerated.auth_flavor.AUTH_SYS)
                                    {
                                        throw new InvalidOperationException("Expected UDP fallback to preserve the MOUNT v3 EXPORT call envelope.");
                                    }

                                    byte[] replyBytes = RpcMessageCodec.Encode(
                                        CreateAcceptedReplyEnvelope(
                                            request.Header.xid,
                                            new exports
                                            {
                                                Value = new exportnode
                                                {
                                                    ex_dir = new dirpath
                                                    {
                                                        Value = "/srv/udp",
                                                    },
                                                    ex_groups = new groups(),
                                                    ex_next = new exports(),
                                                },
                                            },
                                            static (value, writer) => value.WriteTo(writer)));

                                    _ = await udpServer.SendAsync(
                                        replyBytes,
                                        replyBytes.Length,
                                        receivedDatagram.RemoteEndPoint).ConfigureAwait(false);
                                },
                                cancellationToken);

                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithConnectionTimeout(TimeSpan.FromMilliseconds(200))
                                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                                .Build();

                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);

                            if (exports.Count != 1
                                || !string.Equals(exports[0].ExportPath, "/srv/udp", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected grouped client execution to decode the UDP fallback reply.");
                            }

                            await serverTask.ConfigureAwait(false);
                        }),
                });
        }

        private static RpcMessageEnvelope CreateAcceptedReplyEnvelope<T>(uint xid, T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: RpcGenerated.accept_stat.SUCCESS,
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: writer.ToArray());
        }

        private static byte[] EncodeAcceptedReply<T>(T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return EncodeAcceptedSuccessReply(writer.ToArray());
        }

        private static byte[] EncodeAcceptedSuccessReply(byte[] procedurePayload)
        {
            return RpcMessageCodec.Encode(
                RpcMessageFactory.CreateAcceptedReply(
                    xid: 0x10203040,
                    status: RpcGenerated.accept_stat.SUCCESS,
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: procedurePayload));
        }

        private static fattr3 CreateAttributes(ftype3 fileType, ulong size, ulong fileId)
        {
            return new fattr3
            {
                type = fileType,
                mode = new mode3
                {
                    Value = new uint32
                    {
                        Value = 420,
                    },
                },
                nlink = new uint32
                {
                    Value = 1,
                },
                uid = new uid3
                {
                    Value = new uint32
                    {
                        Value = 1000,
                    },
                },
                gid = new gid3
                {
                    Value = new uint32
                    {
                        Value = 1000,
                    },
                },
                size = CreateSize(size),
                used = CreateSize(size),
                rdev = new specdata3
                {
                    specdata1 = new uint32
                    {
                        Value = 0,
                    },
                    specdata2 = new uint32
                    {
                        Value = 0,
                    },
                },
                fsid = new uint64
                {
                    Value = 77,
                },
                fileid = new fileid3
                {
                    Value = new uint64
                    {
                        Value = fileId,
                    },
                },
                atime = CreateTime(10, 1),
                mtime = CreateTime(20, 2),
                ctime = CreateTime(30, 3),
            };
        }

        private static post_op_attr CreatePostOperationAttributes(ftype3 fileType, ulong size, ulong fileId)
        {
            return new post_op_attr
            {
                attributes_follow = true,
                attributes = CreateAttributes(fileType, size, fileId),
            };
        }

        private static pre_op_attr CreatePreOperationAttributes(ulong size)
        {
            return new pre_op_attr
            {
                attributes_follow = true,
                attributes = new wcc_attr
                {
                    size = CreateSize(size),
                    mtime = CreateTime(40, 4),
                    ctime = CreateTime(50, 5),
                },
            };
        }

        private static wcc_data CreateWeakCacheConsistency(ulong beforeSize, ulong afterSize, ulong afterFileId)
        {
            return new wcc_data
            {
                before = CreatePreOperationAttributes(beforeSize),
                after = CreatePostOperationAttributes(ftype3.NF3REG, afterSize, afterFileId),
            };
        }

        private static size3 CreateSize(ulong value)
        {
            return new size3
            {
                Value = new uint64
                {
                    Value = value,
                },
            };
        }

        private static nfstime3 CreateTime(uint seconds, uint nanoseconds)
        {
            return new nfstime3
            {
                seconds = new uint32
                {
                    Value = seconds,
                },
                nseconds = new uint32
                {
                    Value = nanoseconds,
                },
            };
        }

        private static entry3list CreateEntryList(params (ulong FileId, string Name, ulong Cookie)[] entries)
        {
            entry3list head = new entry3list();

            for (int index = entries.Length - 1; index >= 0; index--)
            {
                (ulong fileId, string name, ulong cookie) = entries[index];
                head = new entry3list
                {
                    Value = new entry3
                    {
                        fileid = new fileid3
                        {
                            Value = new uint64
                            {
                                Value = fileId,
                            },
                        },
                        name = new filename3
                        {
                            Value = name,
                        },
                        cookie = new cookie3
                        {
                            Value = new uint64
                            {
                                Value = cookie,
                            },
                        },
                        nextentry = head,
                    },
                };
            }

            return head;
        }

        private static entryplus3list CreateEntryPlusList(
            params (ulong FileId, string Name, ulong Cookie, ftype3 FileType, ulong Size, byte[]? Handle)[] entries)
        {
            entryplus3list head = new entryplus3list();

            for (int index = entries.Length - 1; index >= 0; index--)
            {
                (ulong fileId, string name, ulong cookie, ftype3 fileType, ulong size, byte[]? handle) = entries[index];
                head = new entryplus3list
                {
                    Value = new entryplus3
                    {
                        fileid = new fileid3
                        {
                            Value = new uint64
                            {
                                Value = fileId,
                            },
                        },
                        name = new filename3
                        {
                            Value = name,
                        },
                        cookie = new cookie3
                        {
                            Value = new uint64
                            {
                                Value = cookie,
                            },
                        },
                        name_attributes = CreatePostOperationAttributes(fileType, size, fileId),
                        name_handle = new post_op_fh3
                        {
                            handle_follows = handle is not null,
                            handle = handle is null
                                ? null
                                : new nfs_fh3
                                {
                                    data = handle,
                                },
                        },
                        nextentry = head,
                    },
                };
            }

            return head;
        }
    }
}
