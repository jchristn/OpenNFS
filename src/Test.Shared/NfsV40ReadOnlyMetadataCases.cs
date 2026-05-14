namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV40SuiteSupport;

    /// <summary>
    /// Read-only NFSv4.0 metadata and namespace inspection suites.
    /// </summary>
    internal static class NfsV40ReadOnlyMetadataCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundReadOnlyOperationsPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful read-only lookup, readdir, read, and readlink flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                                    [@"C:\exports\docs\shortcut"] = NfsPathKind.SymbolicLink,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                                },
                                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\shortcut"] = "notes.txt",
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle shortcutHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\shortcut"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope readdirReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010004,
                                    "readdir-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTROOTFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LOOKUP,
                                            oplookup = CreateLookupArguments("docs"),
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READDIR,
                                            opreaddir = CreateReadDirectoryArguments(0UL, new byte[8], 4096U),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res readdirResult = ReadCompoundReply(readdirReply);
                            entry4? firstReadDirectoryEntry = readdirResult.resarray?[2].opreaddir?.resok4?.reply?.entries;
                            if (readdirResult.status != nfsstat4.NFS4_OK
                                || readdirResult.resarray is null
                                || readdirResult.resarray.Length != 3
                                || firstReadDirectoryEntry is null
                                || !string.Equals(ReadUtf8(firstReadDirectoryEntry.name?.Value), "notes.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READDIR to return the docs directory entries through the COMPOUND engine.");
                            }

                            RpcMessageEnvelope readReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010005,
                                    "read-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = notesHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READ,
                                            opread = new READ4args
                                            {
                                                stateid = CreateAnonymousStateId(),
                                                offset = new offset4
                                                {
                                                    Value = 0UL,
                                                },
                                                count = new count4
                                                {
                                                    Value = 5U,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res readResult = ReadCompoundReply(readReply);
                            byte[]? readBytes = readResult.resarray?[1].opread?.resok4?.data;
                            if (readResult.status != nfsstat4.NFS4_OK
                                || readResult.resarray is null
                                || readResult.resarray.Length != 2
                                || readBytes is null
                                || Encoding.UTF8.GetString(readBytes) != "hello")
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READ to return file bytes through the COMPOUND engine.");
                            }

                            RpcMessageEnvelope readLinkReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010006,
                                    "readlink-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = shortcutHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READLINK,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res readLinkResult = ReadCompoundReply(readLinkReply);
                            if (readLinkResult.status != nfsstat4.NFS4_OK
                                || readLinkResult.resarray is null
                                || readLinkResult.resarray.Length != 2
                                || !string.Equals(Encoding.UTF8.GetString(readLinkResult.resarray[1].opreadlink?.resok4?.link?.Value ?? Array.Empty<byte>()), "notes.txt", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READLINK to return the stored symbolic-link target.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundReadOnlyOperationsNegative",
                        displayName: "NFSv4.0 COMPOUND preserves negative read-only lookup, read, readlink, and readdir outcomes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope missingLookupReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010007,
                                    "lookup-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTROOTFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LOOKUP,
                                            oplookup = CreateLookupArguments("missing.txt"),
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res missingLookupResult = ReadCompoundReply(missingLookupReply);
                            if (missingLookupResult.status != nfsstat4.NFS4ERR_NOENT
                                || missingLookupResult.resarray is null
                                || missingLookupResult.resarray.Length != 2
                                || missingLookupResult.resarray[1].oplookup?.status != nfsstat4.NFS4ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 LOOKUP to short-circuit with NFS4ERR_NOENT for missing children.");
                            }

                            RpcMessageEnvelope readDirectoryAsFileReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010008,
                                    "read-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTROOTFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READ,
                                            opread = new READ4args
                                            {
                                                stateid = CreateAnonymousStateId(),
                                                offset = new offset4
                                                {
                                                    Value = 0UL,
                                                },
                                                count = new count4
                                                {
                                                    Value = 16U,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res readDirectoryAsFileResult = ReadCompoundReply(readDirectoryAsFileReply);
                            if (readDirectoryAsFileResult.status != nfsstat4.NFS4ERR_ISDIR
                                || readDirectoryAsFileResult.resarray is null
                                || readDirectoryAsFileResult.resarray.Length != 2
                                || readDirectoryAsFileResult.resarray[1].opread?.status != nfsstat4.NFS4ERR_ISDIR)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READ against a directory to report NFS4ERR_ISDIR.");
                            }

                            RpcMessageEnvelope invalidReadLinkReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010009,
                                    "readlink-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = notesHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READLINK,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res invalidReadLinkResult = ReadCompoundReply(invalidReadLinkReply);
                            if (invalidReadLinkResult.status != nfsstat4.NFS4ERR_INVAL
                                || invalidReadLinkResult.resarray is null
                                || invalidReadLinkResult.resarray.Length != 2
                                || invalidReadLinkResult.resarray[1].opreadlink?.status != nfsstat4.NFS4ERR_INVAL)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READLINK against a regular file to report NFS4ERR_INVAL.");
                            }

                            RpcMessageEnvelope badCookieReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000A,
                                    "readdir-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTROOTFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_READDIR,
                                            opreaddir = CreateReadDirectoryArguments(1UL, new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, 4096U),
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res badCookieResult = ReadCompoundReply(badCookieReply);
                            if (badCookieResult.status != nfsstat4.NFS4ERR_BAD_COOKIE
                                || badCookieResult.resarray is null
                                || badCookieResult.resarray.Length != 2
                                || badCookieResult.resarray[1].opreaddir?.status != nfsstat4.NFS4ERR_BAD_COOKIE)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 READDIR with a mismatched cookie verifier to report NFS4ERR_BAD_COOKIE.");
                            }
                        }),
            };
        }
    }
}

