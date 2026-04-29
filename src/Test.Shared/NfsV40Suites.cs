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

    /// <summary>
    /// Touchstone suites covering the first NFSv4.0 COMPOUND execution surface.
    /// </summary>
    public static class NfsV40Suites
    {
        /// <summary>
        /// Creates the shared NFSv4.0 suite catalog.
        /// </summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NfsV40Suites",
                displayName: "NFSv4.0 Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundCurrentAndSavedFileHandleFlow",
                        displayName: "NFSv4.0 COMPOUND preserves current and saved filehandles across core operations",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\notes.txt"] = NfsPathKind.File,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\notes.txt"] = Encoding.ASCII.GetBytes("hello"),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/notes.txt", @"C:\exports\notes.txt"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope reply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010001,
                                    "flow",
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
                                                    Value = fileHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SAVEFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_TYPE,
                                                    (int)Nfs40Constants.FATTR4_SIZE,
                                                    (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTROOTFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_ACCESS,
                                            opaccess = new ACCESS4args
                                            {
                                                access = (uint)(
                                                    Nfs40Constants.ACCESS4_READ
                                                    | Nfs40Constants.ACCESS4_LOOKUP),
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_RESTOREFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res compoundResult = ReadCompoundReply(reply);
                            if (compoundResult.status != nfsstat4.NFS4_OK
                                || compoundResult.resarray is null
                                || compoundResult.resarray.Length != 8
                                || !string.Equals(ReadUtf8(compoundResult.tag), "flow", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the initial NFSv4.0 COMPOUND flow to complete successfully and preserve the tag plus every requested result.");
                            }

                            GETATTR4res getAttrResult = compoundResult.resarray[2].opgetattr
                                ?? throw new InvalidOperationException("Expected COMPOUND result index 2 to contain a GETATTR reply.");
                            if (getAttrResult.status != nfsstat4.NFS4_OK
                                || getAttrResult.resok4?.obj_attributes?.attrmask is null
                                || getAttrResult.resok4.obj_attributes.attr_vals?.Value is null)
                            {
                                throw new InvalidOperationException("Expected GETATTR to return typed attributes for the current filehandle.");
                            }

                            XdrReader attributeReader = new XdrReader(getAttrResult.resok4.obj_attributes.attr_vals.Value);
                            fattr4_type attributeType = fattr4_type.ReadFrom(attributeReader);
                            fattr4_size attributeSize = fattr4_size.ReadFrom(attributeReader);
                            fattr4_filehandle attributeFileHandle = fattr4_filehandle.ReadFrom(attributeReader);
                            attributeReader.EnsureFullyConsumed();

                            if (attributeType.Value != nfs_ftype4.NF4REG
                                || attributeSize.Value != 5UL
                                || attributeFileHandle.Value?.Value is null
                                || !attributeFileHandle.Value.Value.AsSpan().SequenceEqual(fileHandle.ToArray()))
                            {
                                throw new InvalidOperationException("Expected GETATTR to surface the file type, byte length, and current filehandle.");
                            }

                            ACCESS4res accessResult = compoundResult.resarray[4].opaccess
                                ?? throw new InvalidOperationException("Expected COMPOUND result index 4 to contain an ACCESS reply.");
                            if (accessResult.status != nfsstat4.NFS4_OK
                                || accessResult.resok4 is null
                                || accessResult.resok4.supported != (uint)(
                                    Nfs40Constants.ACCESS4_READ
                                    | Nfs40Constants.ACCESS4_LOOKUP
                                    | Nfs40Constants.ACCESS4_MODIFY
                                    | Nfs40Constants.ACCESS4_EXTEND
                                    | Nfs40Constants.ACCESS4_DELETE)
                                || accessResult.resok4.access != (uint)(
                                    Nfs40Constants.ACCESS4_READ
                                    | Nfs40Constants.ACCESS4_LOOKUP))
                            {
                                throw new InvalidOperationException("Expected ACCESS on the root directory to preserve the current filehandle and mask the requested bits against directory capabilities.");
                            }

                            GETFH4res rootGetFileHandle = compoundResult.resarray[5].opgetfh
                                ?? throw new InvalidOperationException("Expected COMPOUND result index 5 to contain a GETFH reply.");
                            GETFH4res restoredGetFileHandle = compoundResult.resarray[7].opgetfh
                                ?? throw new InvalidOperationException("Expected COMPOUND result index 7 to contain a GETFH reply.");

                            if (rootGetFileHandle.status != nfsstat4.NFS4_OK
                                || rootGetFileHandle.resok4?.@object?.Value is null
                                || !rootGetFileHandle.resok4.@object.Value.AsSpan().SequenceEqual(rootHandle.ToArray())
                                || restoredGetFileHandle.status != nfsstat4.NFS4_OK
                                || restoredGetFileHandle.resok4?.@object?.Value is null
                                || !restoredGetFileHandle.resok4.@object.Value.AsSpan().SequenceEqual(fileHandle.ToArray()))
                            {
                                throw new InvalidOperationException("Expected GETFH to surface the current root handle after PUTROOTFH and the original file handle after RESTOREFH.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundErrorShortCircuit",
                        displayName: "NFSv4.0 COMPOUND short-circuits after the first failing operation",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);

                            RpcMessageEnvelope restoreFailureReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010002,
                                    "restore-fail",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_RESTOREFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res restoreFailureResult = ReadCompoundReply(restoreFailureReply);
                            if (restoreFailureResult.status != nfsstat4.NFS4ERR_RESTOREFH
                                || restoreFailureResult.resarray is null
                                || restoreFailureResult.resarray.Length != 1
                                || restoreFailureResult.resarray[0].oprestorefh?.status != nfsstat4.NFS4ERR_RESTOREFH)
                            {
                                throw new InvalidOperationException("Expected RESTOREFH without a saved filehandle to fail the COMPOUND and short-circuit the following GETFH.");
                            }

                            RpcMessageEnvelope illegalOperationReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010003,
                                    "illegal-op",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = (nfs_opnum4)999,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res illegalOperationResult = ReadCompoundReply(illegalOperationReply);
                            if (illegalOperationResult.status != nfsstat4.NFS4ERR_OP_ILLEGAL
                                || illegalOperationResult.resarray is null
                                || illegalOperationResult.resarray.Length != 1
                                || illegalOperationResult.resarray[0].resop != nfs_opnum4.OP_ILLEGAL
                                || illegalOperationResult.resarray[0].opillegal?.status != nfsstat4.NFS4ERR_OP_ILLEGAL)
                            {
                                throw new InvalidOperationException("Expected an unsupported NFSv4.0 operation number to surface OP_ILLEGAL and short-circuit the rest of the COMPOUND.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundSecurityInfoAndIdentityAttributesPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful SECINFO and identity-attribute replies",
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
                                .UseIdMapper(new TestNfsIdMapper("owner@example.test", "group@example.test"))
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope securityInfoReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000B,
                                    "secinfo-positive",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(docsHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SECINFO,
                                            opsecinfo = new SECINFO4args
                                            {
                                                name = CreatePathComponent("notes.txt"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res securityInfoResult = ReadCompoundReply(securityInfoReply);
                            secinfo4[] advertisedFlavors = securityInfoResult.resarray?[1].opsecinfo?.resok4?.Value
                                ?? throw new InvalidOperationException("Expected SECINFO to return advertised security flavors.");
                            if (securityInfoResult.status != nfsstat4.NFS4_OK
                                || advertisedFlavors.Length != 2
                                || advertisedFlavors[0].flavor != (uint)auth_flavor.AUTH_NONE
                                || advertisedFlavors[1].flavor != (uint)auth_flavor.AUTH_SYS)
                            {
                                throw new InvalidOperationException("Expected SECINFO to advertise AUTH_NONE and AUTH_SYS for the discovered child.");
                            }

                            RpcMessageEnvelope getattrReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000C,
                                    "identity-attrs-positive",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_TYPE,
                                                    (int)Nfs40Constants.FATTR4_OWNER,
                                                    (int)Nfs40Constants.FATTR4_OWNER_GROUP),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res getattrResult = ReadCompoundReply(getattrReply);
                            GETATTR4resok getattrResok = getattrResult.resarray?[1].opgetattr?.resok4
                                ?? throw new InvalidOperationException("Expected GETATTR to return an attribute payload.");
                            XdrReader attributeReader = new XdrReader(getattrResok.obj_attributes?.attr_vals?.Value ?? Array.Empty<byte>());
                            fattr4_type attributeType = fattr4_type.ReadFrom(attributeReader);
                            fattr4_owner attributeOwner = fattr4_owner.ReadFrom(attributeReader);
                            fattr4_owner_group attributeOwnerGroup = fattr4_owner_group.ReadFrom(attributeReader);
                            attributeReader.EnsureFullyConsumed();

                            if (getattrResult.status != nfsstat4.NFS4_OK
                                || attributeType.Value != nfs_ftype4.NF4REG
                                || !string.Equals(
                                    Encoding.UTF8.GetString(attributeOwner.Value?.Value?.Value ?? Array.Empty<byte>()),
                                    "owner@example.test",
                                    StringComparison.Ordinal)
                                || !string.Equals(
                                    Encoding.UTF8.GetString(attributeOwnerGroup.Value?.Value?.Value ?? Array.Empty<byte>()),
                                    "group@example.test",
                                    StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected GETATTR to encode owner and owner-group identity strings when an id mapper is configured.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundSecurityInfoAndIdentityAttributesNegative",
                        displayName: "NFSv4.0 COMPOUND preserves negative SECINFO discovery and unsupported identity-attribute results",
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
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope missingSecurityInfoReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000D,
                                    "secinfo-negative",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(docsHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SECINFO,
                                            opsecinfo = new SECINFO4args
                                            {
                                                name = CreatePathComponent("missing.txt"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res missingSecurityInfoResult = ReadCompoundReply(missingSecurityInfoReply);
                            if (missingSecurityInfoResult.status != nfsstat4.NFS4ERR_NOENT
                                || missingSecurityInfoResult.resarray is null
                                || missingSecurityInfoResult.resarray.Length != 2
                                || missingSecurityInfoResult.resarray[1].opsecinfo?.status != nfsstat4.NFS4ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected SECINFO for a missing entry to short-circuit with NFS4ERR_NOENT.");
                            }

                            RpcMessageEnvelope unsupportedAttributesReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000E,
                                    "identity-attrs-negative",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_OWNER,
                                                    (int)Nfs40Constants.FATTR4_OWNER_GROUP),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res unsupportedAttributesResult = ReadCompoundReply(unsupportedAttributesReply);
                            if (unsupportedAttributesResult.status != nfsstat4.NFS4ERR_ATTRNOTSUPP
                                || unsupportedAttributesResult.resarray is null
                                || unsupportedAttributesResult.resarray.Length != 2
                                || unsupportedAttributesResult.resarray[1].opgetattr?.status != nfsstat4.NFS4ERR_ATTRNOTSUPP)
                            {
                                throw new InvalidOperationException("Expected GETATTR owner/owner_group requests without an id mapper to fail with NFS4ERR_ATTRNOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "AclGetSetRoundTripPositive",
                        displayName: "NFSv4.0 COMPOUND round-trips ACL support and ACL replacement over GETATTR and SETATTR",
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

                            TestNfsAcls acls = new TestNfsAcls(
                                initialEntries: new Dictionary<string, IReadOnlyList<NfsAclEntry>>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = new[]
                                    {
                                        new NfsAclEntry(
                                            NfsAclEntryType.Allow,
                                            NfsAclEntryFlags.None,
                                            NfsAclPermissionMask.ReadData | NfsAclPermissionMask.ReadAcl,
                                            "EVERYONE@"),
                                    },
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .UseAcls(acls)
                                .AddExport("/", @"C:\exports")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope initialAclReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000F,
                                    "acl-get-positive",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_ACL,
                                                    (int)Nfs40Constants.FATTR4_ACLSUPPORT),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res initialAclResult = ReadCompoundReply(initialAclReply);
                            GETATTR4resok initialAclResok = initialAclResult.resarray?[1].opgetattr?.resok4
                                ?? throw new InvalidOperationException("Expected GETATTR to return an ACL attribute payload.");
                            XdrReader initialAclReader = new XdrReader(initialAclResok.obj_attributes?.attr_vals?.Value ?? Array.Empty<byte>());
                            fattr4_acl initialAcl = fattr4_acl.ReadFrom(initialAclReader);
                            fattr4_aclsupport initialAclSupport = fattr4_aclsupport.ReadFrom(initialAclReader);
                            initialAclReader.EnsureFullyConsumed();

                            if (initialAclResult.status != nfsstat4.NFS4_OK
                                || (NfsAclSupport)initialAclSupport.Value != (NfsAclSupport.AllowAcl | NfsAclSupport.DenyAcl)
                                || initialAcl.Value is null
                                || initialAcl.Value.Length != 1
                                || !string.Equals(
                                    Encoding.UTF8.GetString(initialAcl.Value[0].who?.Value?.Value ?? Array.Empty<byte>()),
                                    "EVERYONE@",
                                    StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected GETATTR ACL support to return the initial ACL entry and advertised ACL support flags.");
                            }

                            RpcMessageEnvelope setAclReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010010,
                                    "acl-set-positive",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETATTR,
                                            opsetattr = new SETATTR4args
                                            {
                                                stateid = CreateAnonymousStateId(),
                                                obj_attributes = CreateAclAttributes(
                                                    new[]
                                                    {
                                                        new NfsAclEntry(
                                                            NfsAclEntryType.Allow,
                                                            NfsAclEntryFlags.None,
                                                            NfsAclPermissionMask.ReadData
                                                                | NfsAclPermissionMask.WriteData
                                                                | NfsAclPermissionMask.ReadAcl,
                                                            "interop-user@example.test"),
                                                        new NfsAclEntry(
                                                            NfsAclEntryType.Deny,
                                                            NfsAclEntryFlags.None,
                                                            NfsAclPermissionMask.Delete,
                                                            "EVERYONE@"),
                                                    }),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res setAclResult = ReadCompoundReply(setAclReply);
                            SETATTR4res? setAttrOperation = setAclResult.resarray is not null && setAclResult.resarray.Length > 1
                                ? setAclResult.resarray[1].opsetattr
                                : null;
                            if (setAclResult.status != nfsstat4.NFS4_OK
                                || setAclResult.resarray is null
                                || setAclResult.resarray.Length != 2
                                || setAttrOperation?.status != nfsstat4.NFS4_OK
                                || setAttrOperation.attrsset?.Value is null
                                || setAttrOperation.attrsset.Value.Length < 1
                                || (setAttrOperation.attrsset.Value[0] & (1U << (int)Nfs40Constants.FATTR4_ACL)) == 0U)
                            {
                                throw new InvalidOperationException("Expected SETATTR to succeed and report the ACL bit in attrsset.");
                            }

                            RpcMessageEnvelope rereadAclReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010011,
                                    "acl-reread-positive",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_ACL),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res rereadAclResult = ReadCompoundReply(rereadAclReply);
                            GETATTR4resok rereadAclResok = rereadAclResult.resarray?[1].opgetattr?.resok4
                                ?? throw new InvalidOperationException("Expected GETATTR to return the updated ACL attribute payload.");
                            XdrReader rereadAclReader = new XdrReader(rereadAclResok.obj_attributes?.attr_vals?.Value ?? Array.Empty<byte>());
                            fattr4_acl updatedAcl = fattr4_acl.ReadFrom(rereadAclReader);
                            rereadAclReader.EnsureFullyConsumed();

                            if (rereadAclResult.status != nfsstat4.NFS4_OK
                                || updatedAcl.Value is null
                                || updatedAcl.Value.Length != 2
                                || !string.Equals(
                                    Encoding.UTF8.GetString(updatedAcl.Value[0].who?.Value?.Value ?? Array.Empty<byte>()),
                                    "interop-user@example.test",
                                    StringComparison.Ordinal)
                                || updatedAcl.Value[1].type?.Value != (uint)NfsAclEntryType.Deny)
                            {
                                throw new InvalidOperationException("Expected GETATTR after SETATTR to return the replacement ACL entry set.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "AclGetSetRoundTripNegative",
                        displayName: "NFSv4.0 COMPOUND preserves ATTRNOTSUPP for ACL GETATTR and SETATTR when ACL capability is absent",
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

                            RpcMessageEnvelope getAclReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010012,
                                    "acl-get-negative",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_ACL,
                                                    (int)Nfs40Constants.FATTR4_ACLSUPPORT),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res getAclResult = ReadCompoundReply(getAclReply);
                            if (getAclResult.status != nfsstat4.NFS4ERR_ATTRNOTSUPP
                                || getAclResult.resarray is null
                                || getAclResult.resarray.Length != 2
                                || getAclResult.resarray[1].opgetattr?.status != nfsstat4.NFS4ERR_ATTRNOTSUPP)
                            {
                                throw new InvalidOperationException("Expected GETATTR ACL requests without ACL capability to fail with NFS4ERR_ATTRNOTSUPP.");
                            }

                            RpcMessageEnvelope setAclReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010013,
                                    "acl-set-negative",
                                    0U,
                                    new[]
                                    {
                                        CreatePutFileHandleArgop(notesHandle),
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SETATTR,
                                            opsetattr = new SETATTR4args
                                            {
                                                stateid = CreateAnonymousStateId(),
                                                obj_attributes = CreateAclAttributes(
                                                    new[]
                                                    {
                                                        new NfsAclEntry(
                                                            NfsAclEntryType.Allow,
                                                            NfsAclEntryFlags.None,
                                                            NfsAclPermissionMask.ReadData,
                                                            "EVERYONE@"),
                                                    }),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res setAclResult = ReadCompoundReply(setAclReply);
                            if (setAclResult.status != nfsstat4.NFS4ERR_ATTRNOTSUPP
                                || setAclResult.resarray is null
                                || setAclResult.resarray.Length != 2
                                || setAclResult.resarray[1].opsetattr?.status != nfsstat4.NFS4ERR_ATTRNOTSUPP)
                            {
                                throw new InvalidOperationException("Expected SETATTR ACL requests without ACL capability to fail with NFS4ERR_ATTRNOTSUPP.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundNamespaceAndMutationOperationsPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful LOOKUPP, CREATE, LINK, RENAME, and REMOVE flows",
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
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope lookuppReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000B,
                                    "lookupp-positive",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LOOKUPP,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_TYPE,
                                                    (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res lookuppResult = ReadCompoundReply(lookuppReply);
                            if (lookuppResult.status != nfsstat4.NFS4_OK
                                || lookuppResult.resarray is null
                                || lookuppResult.resarray.Length != 4
                                || lookuppResult.resarray[1].oplookupp?.status != nfsstat4.NFS4_OK
                                || !lookuppResult.resarray[2].opgetfh!.resok4!.@object!.Value!.AsSpan().SequenceEqual(rootHandle.ToArray()))
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 LOOKUPP to move from the child filehandle to the parent directory handle.");
                            }

                            RpcMessageEnvelope createDirectoryReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000C,
                                    "create-dir-positive",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_CREATE,
                                            opcreate = new CREATE4args
                                            {
                                                objtype = new createtype4
                                                {
                                                    type = nfs_ftype4.NF4DIR,
                                                },
                                                objname = CreatePathComponent("newdir"),
                                                createattrs = CreateEmptyAttributes(),
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_TYPE,
                                                    (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res createDirectoryResult = ReadCompoundReply(createDirectoryReply);
                            if (createDirectoryResult.status != nfsstat4.NFS4_OK
                                || createDirectoryResult.resarray is null
                                || createDirectoryResult.resarray.Length != 4
                                || createDirectoryResult.resarray[1].opcreate?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 CREATE to build a directory and keep the created object as the current filehandle.");
                            }

                            RpcMessageEnvelope createSymbolicLinkReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000D,
                                    "create-symlink-positive",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_CREATE,
                                            opcreate = new CREATE4args
                                            {
                                                objtype = new createtype4
                                                {
                                                    type = nfs_ftype4.NF4LNK,
                                                    linkdata = new linktext4
                                                    {
                                                        Value = Encoding.UTF8.GetBytes("notes.txt"),
                                                    },
                                                },
                                                objname = CreatePathComponent("shortcut"),
                                                createattrs = CreateEmptyAttributes(),
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETATTR,
                                            opgetattr = new GETATTR4args
                                            {
                                                attr_request = Nfs40AttributeEncoder.CreateBitmap(
                                                    (int)Nfs40Constants.FATTR4_TYPE,
                                                    (int)Nfs40Constants.FATTR4_FILEHANDLE),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res createSymbolicLinkResult = ReadCompoundReply(createSymbolicLinkReply);
                            if (createSymbolicLinkResult.status != nfsstat4.NFS4_OK
                                || createSymbolicLinkResult.resarray is null
                                || createSymbolicLinkResult.resarray.Length != 4
                                || createSymbolicLinkResult.resarray[1].opcreate?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 CREATE to build a symbolic link and keep the created link as the current filehandle.");
                            }

                            RpcMessageEnvelope linkReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000E,
                                    "link-positive",
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
                                            argop = nfs_opnum4.OP_SAVEFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LINK,
                                            oplink = new LINK4args
                                            {
                                                newname = CreatePathComponent("notes-link.txt"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res linkResult = ReadCompoundReply(linkReply);
                            if (linkResult.status != nfsstat4.NFS4_OK
                                || linkResult.resarray is null
                                || linkResult.resarray.Length != 4
                                || linkResult.resarray[3].oplink?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 LINK to create a new directory entry for the saved filehandle.");
                            }

                            RpcMessageEnvelope renameReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000F,
                                    "rename-positive",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SAVEFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_RENAME,
                                            oprename = new RENAME4args
                                            {
                                                oldname = CreatePathComponent("newdir"),
                                                newname = CreatePathComponent("renamed-dir"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res renameResult = ReadCompoundReply(renameReply);
                            if (renameResult.status != nfsstat4.NFS4_OK
                                || renameResult.resarray is null
                                || renameResult.resarray.Length != 4
                                || renameResult.resarray[3].oprename?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 RENAME to move the saved-directory entry into the target directory.");
                            }

                            RpcMessageEnvelope removeReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010010,
                                    "remove-positive",
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
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_REMOVE,
                                            opremove = new REMOVE4args
                                            {
                                                target = CreatePathComponent("renamed-dir"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res removeResult = ReadCompoundReply(removeReply);
                            if (removeResult.status != nfsstat4.NFS4_OK
                                || removeResult.resarray is null
                                || removeResult.resarray.Length != 2
                                || removeResult.resarray[1].opremove?.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 REMOVE to delete the renamed directory entry.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundNamespaceAndMutationOperationsNegative",
                        displayName: "NFSv4.0 COMPOUND preserves negative LOOKUPP, CREATE, LINK, RENAME, and REMOVE outcomes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                                    [@"C:\other"] = NfsPathKind.Directory,
                                },
                                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/", @"C:\exports")
                                .AddExport("/other", @"C:\other")
                                .Build();

                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle otherRootHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/other", @"C:\other"),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope rootLookupParentReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010011,
                                    "lookupp-negative",
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
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LOOKUPP,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res rootLookupParentResult = ReadCompoundReply(rootLookupParentReply);
                            if (rootLookupParentResult.status != nfsstat4.NFS4ERR_NOENT
                                || rootLookupParentResult.resarray is null
                                || rootLookupParentResult.resarray.Length != 2
                                || rootLookupParentResult.resarray[1].oplookupp?.status != nfsstat4.NFS4ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 LOOKUPP on the export root to report NFS4ERR_NOENT.");
                            }

                            RpcMessageEnvelope badCreateReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010012,
                                    "create-negative",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_CREATE,
                                            opcreate = new CREATE4args
                                            {
                                                objtype = new createtype4
                                                {
                                                    type = nfs_ftype4.NF4REG,
                                                },
                                                objname = CreatePathComponent("new-file"),
                                                createattrs = CreateEmptyAttributes(),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res badCreateResult = ReadCompoundReply(badCreateReply);
                            if (badCreateResult.status != nfsstat4.NFS4ERR_BADTYPE
                                || badCreateResult.resarray is null
                                || badCreateResult.resarray.Length != 2
                                || badCreateResult.resarray[1].opcreate?.status != nfsstat4.NFS4ERR_BADTYPE)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 CREATE for a regular file object to report NFS4ERR_BADTYPE.");
                            }

                            RpcMessageEnvelope missingSaveHandleLinkReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010013,
                                    "link-negative",
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
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_LINK,
                                            oplink = new LINK4args
                                            {
                                                newname = CreatePathComponent("notes-link.txt"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res missingSaveHandleLinkResult = ReadCompoundReply(missingSaveHandleLinkReply);
                            if (missingSaveHandleLinkResult.status != nfsstat4.NFS4ERR_NOFILEHANDLE
                                || missingSaveHandleLinkResult.resarray is null
                                || missingSaveHandleLinkResult.resarray.Length != 2
                                || missingSaveHandleLinkResult.resarray[1].oplink?.status != nfsstat4.NFS4ERR_NOFILEHANDLE)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 LINK without SAVEFH to report NFS4ERR_NOFILEHANDLE.");
                            }

                            RpcMessageEnvelope crossExportRenameReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010014,
                                    "rename-negative",
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
                                                    Value = docsHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_SAVEFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTFH,
                                            opputfh = new PUTFH4args
                                            {
                                                @object = new nfs_fh4
                                                {
                                                    Value = otherRootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_RENAME,
                                            oprename = new RENAME4args
                                            {
                                                oldname = CreatePathComponent("notes.txt"),
                                                newname = CreatePathComponent("moved.txt"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res crossExportRenameResult = ReadCompoundReply(crossExportRenameReply);
                            if (crossExportRenameResult.status != nfsstat4.NFS4ERR_XDEV
                                || crossExportRenameResult.resarray is null
                                || crossExportRenameResult.resarray.Length != 4
                                || crossExportRenameResult.resarray[3].oprename?.status != nfsstat4.NFS4ERR_XDEV)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 RENAME across different exports to report NFS4ERR_XDEV.");
                            }

                            RpcMessageEnvelope missingRemoveReply = await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010015,
                                    "remove-negative",
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
                                                    Value = rootHandle.ToArray(),
                                                },
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_REMOVE,
                                            opremove = new REMOVE4args
                                            {
                                                target = CreatePathComponent("missing"),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res missingRemoveResult = ReadCompoundReply(missingRemoveReply);
                            if (missingRemoveResult.status != nfsstat4.NFS4ERR_NOENT
                                || missingRemoveResult.resarray is null
                                || missingRemoveResult.resarray.Length != 2
                                || missingRemoveResult.resarray[1].opremove?.status != nfsstat4.NFS4ERR_NOENT)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 REMOVE for a missing entry to report NFS4ERR_NOENT.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundStatefulOperationsPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful SETCLIENTID, OPEN, RENEW, DOWNGRADE, and CLOSE flows",
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
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            byte[] clientVerifier = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                            COMPOUND4res setClientIdResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010016,
                                        "setclientid-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID,
                                                opsetclientid = CreateSetClientIdArguments("suite-client-a", clientVerifier),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid plus confirmation verifier.");
                            ulong clientId = setClientIdResok.clientid?.Value
                                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid.");
                            byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier.");
                            if (setClientIdResult.status != nfsstat4.NFS4_OK
                                || confirmationVerifier.Length != 8)
                            {
                                throw new InvalidOperationException("Expected successful SETCLIENTID to return an eight-byte confirmation verifier.");
                            }

                            COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010017,
                                        "setclientid-confirm-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed for the issued clientid.");
                            }

                            COMPOUND4res openResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010018,
                                        "open-positive",
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
                                                        Value = docsHandle.ToArray(),
                                                    },
                                                },
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "owner-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_WRITE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            OPEN4resok openResok = openResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected OPEN to return OPEN4resok.");
                            GETFH4resok openGetFileHandle = openResult.resarray?[2].opgetfh?.resok4
                                ?? throw new InvalidOperationException("Expected OPEN compound to return GETFH success data.");
                            if (openResult.status != nfsstat4.NFS4_OK
                                || openResok.stateid is null
                                || (openResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) == 0U
                                || openGetFileHandle.@object?.Value is null
                                || !openGetFileHandle.@object.Value.AsSpan().SequenceEqual(notesHandle.ToArray()))
                            {
                                throw new InvalidOperationException("Expected OPEN to return a confirm-required stateid and switch the current filehandle to the opened file.");
                            }

                            stateid4 openStateId = openResok.stateid;
                            COMPOUND4res openConfirmResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010019,
                                        "open-confirm-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                                opopen_confirm = new OPEN_CONFIRM4args
                                                {
                                                    open_stateid = openStateId,
                                                    seqid = new seqid4
                                                    {
                                                        Value = 2U,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 confirmedStateId = openConfirmResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                                ?? throw new InvalidOperationException("Expected OPEN_CONFIRM to return the updated stateid.");
                            if (openConfirmResult.status != nfsstat4.NFS4_OK || confirmedStateId.seqid != 2U)
                            {
                                throw new InvalidOperationException("Expected OPEN_CONFIRM to advance the stateid sequence.");
                            }

                            COMPOUND4res renewResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001A,
                                        "renew-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_RENEW,
                                                oprenew = new RENEW4args
                                                {
                                                    clientid = new clientid4
                                                    {
                                                        Value = clientId,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (renewResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected RENEW to refresh the confirmed client lease.");
                            }

                            COMPOUND4res downgradeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001B,
                                        "open-downgrade-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_DOWNGRADE,
                                                opopen_downgrade = new OPEN_DOWNGRADE4args
                                                {
                                                    open_stateid = confirmedStateId,
                                                    seqid = new seqid4
                                                    {
                                                        Value = 3U,
                                                    },
                                                    share_access = (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    share_deny = (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 downgradedStateId = downgradeResult.resarray?[0].opopen_downgrade?.resok4?.open_stateid
                                ?? throw new InvalidOperationException("Expected OPEN_DOWNGRADE to return the updated stateid.");
                            if (downgradeResult.status != nfsstat4.NFS4_OK || downgradedStateId.seqid != 3U)
                            {
                                throw new InvalidOperationException("Expected OPEN_DOWNGRADE to advance the stateid sequence.");
                            }

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001C,
                                        "close-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 4U,
                                                    },
                                                    open_stateid = downgradedStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (closeResult.status != nfsstat4.NFS4_OK
                                || closeResult.resarray?[0].opclose?.open_stateid?.seqid != 4U)
                            {
                                throw new InvalidOperationException("Expected CLOSE to advance and return the terminal stateid.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundStatefulOperationsNegative",
                        displayName: "NFSv4.0 COMPOUND preserves negative SETCLIENTID, OPEN, and CLOSE outcomes",
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
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res setClientIdAResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001D,
                                        "setclientid-negative-a",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID,
                                                opsetclientid = CreateSetClientIdArguments("suite-client-a", new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 }),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            SETCLIENTID4resok setClientIdAResok = setClientIdAResult.resarray?[0].opsetclientid?.resok4
                                ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to succeed for the negative-path setup.");
                            ulong clientIdA = setClientIdAResok.clientid?.Value
                                ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to return a clientid.");
                            byte[] confirmationVerifierA = setClientIdAResok.setclientid_confirm?.Value
                                ?? throw new InvalidOperationException("Expected the first SETCLIENTID call to return a confirmation verifier.");

                            COMPOUND4res staleOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001E,
                                        "open-negative-stale-client",
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
                                                        Value = docsHandle.ToArray(),
                                                    },
                                                },
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientIdA,
                                                    "owner-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (staleOpenResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                            {
                                throw new InvalidOperationException("Expected OPEN before SETCLIENTID_CONFIRM to report NFS4ERR_STALE_CLIENTID.");
                            }

                            COMPOUND4res wrongConfirmResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001001F,
                                        "setclientid-confirm-negative",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdA, new byte[8]),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (wrongConfirmResult.status != nfsstat4.NFS4ERR_STALE_CLIENTID)
                            {
                                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM with the wrong verifier to report NFS4ERR_STALE_CLIENTID.");
                            }

                            if (ReadCompoundReply(
                                    await service.DispatchAsync(
                                        CreateCompoundCall(
                                            0x70010020,
                                            "setclientid-confirm-correct",
                                            0U,
                                            new[]
                                            {
                                                new nfs_argop4
                                                {
                                                    argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                    opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdA, confirmationVerifierA),
                                                },
                                            }),
                                        cancellationToken).ConfigureAwait(false)).status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the correct SETCLIENTID_CONFIRM retry to succeed.");
                            }

                            COMPOUND4res openAResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010021,
                                        "open-negative-owner-a",
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
                                                        Value = docsHandle.ToArray(),
                                                    },
                                                },
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientIdA,
                                                    "owner-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_WRITE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 openStateIdA = openAResult.resarray?[1].opopen?.resok4?.stateid
                                ?? throw new InvalidOperationException("Expected the first confirmed client OPEN to succeed before testing share denial.");

                            COMPOUND4res confirmOpenAResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010022,
                                        "open-confirm-negative-owner-a",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                                opopen_confirm = new OPEN_CONFIRM4args
                                                {
                                                    open_stateid = openStateIdA,
                                                    seqid = new seqid4
                                                    {
                                                        Value = 2U,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 confirmedStateIdA = confirmOpenAResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                                ?? throw new InvalidOperationException("Expected the first client's OPEN_CONFIRM to succeed before testing share denial.");

                            COMPOUND4res setClientIdBResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010023,
                                        "setclientid-negative-b",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_SETCLIENTID,
                                                opsetclientid = CreateSetClientIdArguments("suite-client-b", new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 }),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            SETCLIENTID4resok setClientIdBResok = setClientIdBResult.resarray?[0].opsetclientid?.resok4
                                ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to succeed for the share-denied setup.");
                            ulong clientIdB = setClientIdBResok.clientid?.Value
                                ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to return a clientid.");
                            byte[] confirmationVerifierB = setClientIdBResok.setclientid_confirm?.Value
                                ?? throw new InvalidOperationException("Expected the second SETCLIENTID call to return a confirmation verifier.");

                            if (ReadCompoundReply(
                                    await service.DispatchAsync(
                                        CreateCompoundCall(
                                            0x70010024,
                                            "setclientid-confirm-negative-b",
                                            0U,
                                            new[]
                                            {
                                                new nfs_argop4
                                                {
                                                    argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                                    opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientIdB, confirmationVerifierB),
                                                },
                                            }),
                                        cancellationToken).ConfigureAwait(false)).status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the second client's SETCLIENTID_CONFIRM call to succeed.");
                            }

                            COMPOUND4res shareDeniedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010025,
                                        "open-negative-share-denied",
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
                                                        Value = docsHandle.ToArray(),
                                                    },
                                                },
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientIdB,
                                                    "owner-b",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_WRITE,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (shareDeniedResult.status != nfsstat4.NFS4ERR_SHARE_DENIED)
                            {
                                throw new InvalidOperationException("Expected a conflicting second OPEN to report NFS4ERR_SHARE_DENIED.");
                            }

                            COMPOUND4res badStateIdCloseResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010026,
                                        "close-negative-bad-stateid",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 3U,
                                                    },
                                                    open_stateid = new stateid4
                                                    {
                                                        seqid = confirmedStateIdA.seqid,
                                                        other = new byte[12],
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (badStateIdCloseResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected CLOSE with an unknown stateid to report NFS4ERR_BAD_STATEID.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundLockOperationsPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful LOCK, relock, LOCKU, and CLOSE flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (OpenNfsServer server, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientId, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-a",
                                new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 },
                                "owner-lock-a",
                                0x70010027,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002B,
                                        "lock-first-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateId,
                                                    3U,
                                                    clientId,
                                                    "lock-owner-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive LOCK to return a lock stateid.");

                            COMPOUND4res relockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002C,
                                        "lock-relock-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockExistingArguments(
                                                    firstLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 secondLockStateId = relockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive relock to return an updated lock stateid.");

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002D,
                                        "lock-unlock-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    secondLockStateId,
                                                    3U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 unlockedStateId = unlockResult.resarray?[1].oplocku?.lock_stateid
                                ?? throw new InvalidOperationException("Expected positive LOCKU to return an updated lock stateid.");

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001002E,
                                        "lock-close-positive",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 4U,
                                                    },
                                                    open_stateid = confirmedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || firstLockStateId.seqid != 1U
                                || relockResult.status != nfsstat4.NFS4_OK
                                || secondLockStateId.seqid != 2U
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || unlockedStateId.seqid != 3U
                                || closeResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected successful NFSv4.0 locking flows to preserve state sequencing across LOCK, relock, LOCKU, and CLOSE.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundLockOperationsNegative",
                        displayName: "NFSv4.0 COMPOUND preserves denied lock, lock-held close, and bad-state unlock outcomes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientIdA, stateid4 confirmedOpenStateIdA) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-negative-a",
                                new byte[] { 41, 42, 43, 44, 45, 46, 47, 48 },
                                "owner-lock-negative-a",
                                0x70010030,
                                cancellationToken).ConfigureAwait(false);
                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010034,
                                        "lock-first-negative-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdA,
                                                    3U,
                                                    clientIdA,
                                                    "lock-owner-negative-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 lockStateIdA = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the negative locking setup LOCK to return a lock stateid.");

                            (ulong clientIdB, stateid4 confirmedOpenStateIdB) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-lock-client-negative-b",
                                new byte[] { 51, 52, 53, 54, 55, 56, 57, 58 },
                                "owner-lock-negative-b",
                                0x70010040,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res lockTestDeniedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010044,
                                        "lockt-negative-conflict",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientIdB,
                                                    "lock-owner-negative-b",
                                                    nfs_lock_type4.READ_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res lockDeniedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010045,
                                        "lock-negative-conflict",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdB,
                                                    3U,
                                                    clientIdB,
                                                    "lock-owner-negative-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res closeWhileLockedResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010046,
                                        "lock-close-negative-held",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = new seqid4
                                                    {
                                                        Value = 4U,
                                                    },
                                                    open_stateid = confirmedOpenStateIdA,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010047,
                                        "locku-negative-bad-stateid",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    new stateid4
                                                    {
                                                        seqid = lockStateIdA.seqid,
                                                        other = new byte[12],
                                                    },
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            LOCK4denied lockTestDenied = lockTestDeniedResult.resarray?[1].oplockt?.denied
                                ?? throw new InvalidOperationException("Expected denied LOCKT to include conflict details.");
                            LOCK4denied lockDenied = lockDeniedResult.resarray?[1].oplock?.denied
                                ?? throw new InvalidOperationException("Expected denied LOCK to include conflict details.");

                            if (lockTestDeniedResult.status != nfsstat4.NFS4ERR_DENIED
                                || lockDeniedResult.status != nfsstat4.NFS4ERR_DENIED
                                || closeWhileLockedResult.status != nfsstat4.NFS4ERR_LOCKS_HELD
                                || badUnlockResult.status != nfsstat4.NFS4ERR_BAD_STATEID
                                || lockTestDenied.owner?.clientid?.Value != clientIdA
                                || lockDenied.owner?.clientid?.Value != clientIdA)
                            {
                                throw new InvalidOperationException("Expected negative NFSv4.0 locking flows to preserve denied conflict, lock-held close, and bad-state unlock results.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundWriteAndCommitPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful WRITE, COMMIT, and read-back flows with a confirmed open stateid",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (_, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-write-client-a",
                                new byte[] { 59, 60, 61, 62, 63, 64, 65, 66 },
                                "owner-write-a",
                                0x70010048,
                                cancellationToken).ConfigureAwait(false);

                            byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-v40");
                            COMPOUND4res writeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001004C,
                                        "write-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_WRITE,
                                                opwrite = CreateWriteArguments(
                                                    confirmedOpenStateId,
                                                    0UL,
                                                    stable_how4.FILE_SYNC4,
                                                    updatedBytes),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            COMMIT4res commitResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001004D,
                                        "commit-positive",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_COMMIT,
                                                opcommit = CreateCommitArguments(0UL, (uint)updatedBytes.Length),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false)).resarray?[1].opcommit
                                ?? throw new InvalidOperationException("Expected COMMIT to return a COMMIT4res payload.");
                            COMPOUND4res readResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001004E,
                                        "write-readback",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
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
                                                        Value = 64U,
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            WRITE4resok writeResok = writeResult.resarray?[1].opwrite?.resok4
                                ?? throw new InvalidOperationException("Expected WRITE to return a WRITE4resok payload.");
                            COMMIT4resok commitResok = commitResult.resok4
                                ?? throw new InvalidOperationException("Expected COMMIT to return a COMMIT4resok payload.");
                            READ4resok readResok = readResult.resarray?[1].opread?.resok4
                                ?? throw new InvalidOperationException("Expected READ to return a READ4resok payload after WRITE and COMMIT.");

                            if (writeResult.status != nfsstat4.NFS4_OK
                                || writeResok.count?.Value != (uint)updatedBytes.Length
                                || writeResok.committed != stable_how4.FILE_SYNC4
                                || writeResok.writeverf?.Value is null
                                || writeResok.writeverf.Value.Length != 8
                                || commitResult.status != nfsstat4.NFS4_OK
                                || commitResok.writeverf?.Value is null
                                || !commitResok.writeverf.Value.AsSpan().SequenceEqual(writeResok.writeverf.Value)
                                || readResult.status != nfsstat4.NFS4_OK
                                || readResok.data is null
                                || !readResok.data.AsSpan().SequenceEqual(updatedBytes))
                            {
                                throw new InvalidOperationException("Expected raw NFSv4.0 WRITE and COMMIT execution to preserve byte-count acknowledgement, stable write semantics, verifier reuse, and read-back content.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundWriteAndCommitNegative",
                        displayName: "NFSv4.0 COMPOUND preserves BAD_STATEID and directory COMMIT failures for write flows",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (_, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-write-client-negative-a",
                                new byte[] { 67, 68, 69, 70, 71, 72, 73, 74 },
                                "owner-write-negative-a",
                                0x70010060,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res badStateWriteResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010064,
                                        "write-negative-bad-stateid",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_WRITE,
                                                opwrite = CreateWriteArguments(
                                                    new stateid4
                                                    {
                                                        seqid = confirmedOpenStateId.seqid,
                                                        other = new byte[12],
                                                    },
                                                    0UL,
                                                    stable_how4.FILE_SYNC4,
                                                    Encoding.UTF8.GetBytes("x")),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            COMPOUND4res directoryCommitResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010065,
                                        "commit-negative-directory",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_COMMIT,
                                                opcommit = CreateCommitArguments(0UL, 1U),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (badStateWriteResult.status != nfsstat4.NFS4ERR_BAD_STATEID
                                || directoryCommitResult.status != nfsstat4.NFS4ERR_ISDIR)
                            {
                                throw new InvalidOperationException("Expected raw NFSv4.0 WRITE and COMMIT negative flows to preserve BAD_STATEID and directory-commit failures.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "OpenCloseReopen",
                        displayName: "NFSv4.0 COMPOUND reopens a file successfully after CLOSE with the same open owner",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientId, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reopen-client-a",
                                new byte[] { 61, 62, 63, 64, 65, 66, 67, 68 },
                                "owner-reopen-a",
                                0x70010050,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010054,
                                        "close-before-reopen",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(3U),
                                                    open_stateid = confirmedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res reopenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010055,
                                        "reopen-after-close",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "owner-reopen-a",
                                                    4U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            OPEN4resok reopenResok = reopenResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected the reopened OPEN flow to return OPEN4resok.");
                            GETFH4resok reopenGetFileHandle = reopenResult.resarray?[2].opgetfh?.resok4
                                ?? throw new InvalidOperationException("Expected the reopened OPEN flow to return GETFH success data.");

                            COMPOUND4res reopenConfirmResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010056,
                                        "reopen-confirm",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                                opopen_confirm = new OPEN_CONFIRM4args
                                                {
                                                    open_stateid = reopenResok.stateid,
                                                    seqid = CreateSequenceId(5U),
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (closeResult.status != nfsstat4.NFS4_OK
                                || closeResult.resarray?[0].opclose?.open_stateid?.seqid != 3U
                                || reopenResult.status != nfsstat4.NFS4_OK
                                || (reopenResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) == 0U
                                || reopenGetFileHandle.@object?.Value is null
                                || !reopenGetFileHandle.@object.Value.AsSpan().SequenceEqual(notesHandle.ToArray())
                                || reopenConfirmResult.status != nfsstat4.NFS4_OK
                                || reopenConfirmResult.resarray?[0].opopen_confirm?.resok4?.open_stateid?.seqid != 2U)
                            {
                                throw new InvalidOperationException("Expected CLOSE followed by reopen to preserve the open-owner seqid stream and return a fresh confirm-required stateid.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "OpenCloseReopenNegative",
                        displayName: "NFSv4.0 COMPOUND rejects stale open-owner sequencing and stale stateids after CLOSE",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, _) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientId, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reopen-client-negative",
                                new byte[] { 71, 72, 73, 74, 75, 76, 77, 78 },
                                "owner-reopen-negative",
                                0x70010057,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001005B,
                                        "close-before-reopen-negative",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(3U),
                                                    open_stateid = confirmedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res staleSequenceReopen = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001005C,
                                        "reopen-bad-seqid",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "owner-reopen-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res staleCloseResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x7001005D,
                                        "close-stale-stateid",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(3U),
                                                    open_stateid = confirmedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (closeResult.status != nfsstat4.NFS4_OK
                                || staleSequenceReopen.status != nfsstat4.NFS4ERR_BAD_SEQID
                                || staleCloseResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected stale reopen sequencing and stale close stateids to be rejected after CLOSE.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LockConflictAndUnlock",
                        displayName: "NFSv4.0 COMPOUND grants a waiting conflicting lock after the prior owner unlocks",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientIdA, stateid4 confirmedOpenStateIdA) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-client-a",
                                new byte[] { 81, 82, 83, 84, 85, 86, 87, 88 },
                                "owner-conflict-a",
                                0x70010060,
                                cancellationToken).ConfigureAwait(false);
                            (ulong clientIdB, stateid4 confirmedOpenStateIdB) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-client-b",
                                new byte[] { 91, 92, 93, 94, 95, 96, 97, 98 },
                                "owner-conflict-b",
                                0x70010070,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010080,
                                        "conflict-lock-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdA,
                                                    3U,
                                                    clientIdA,
                                                    "lock-conflict-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the initial conflicting lock setup to return a stateid.");

                            COMPOUND4res deniedLockTestResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010081,
                                        "conflict-lockt-b",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientIdB,
                                                    "lock-conflict-b",
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010082,
                                        "conflict-unlock-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    firstLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res grantedLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x70010083,
                                        "conflict-lock-b-after-unlock",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdB,
                                                    3U,
                                                    clientIdB,
                                                    "lock-conflict-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || deniedLockTestResult.status != nfsstat4.NFS4ERR_DENIED
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || grantedLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the second owner to observe a denied conflict before unlock and a granted lock afterwards.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "LockConflictAndUnlockNegative",
                        displayName: "NFSv4.0 COMPOUND preserves denied conflicts and bad unlock stateids before the conflicting owner is released",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);

                            (ulong clientIdA, stateid4 confirmedOpenStateIdA) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-negative-client-a",
                                new byte[] { 101, 102, 103, 104, 105, 106, 107, 108 },
                                "owner-conflict-negative-a",
                                0x70010084,
                                cancellationToken).ConfigureAwait(false);
                            (ulong clientIdB, stateid4 confirmedOpenStateIdB) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-conflict-negative-client-b",
                                new byte[] { 111, 112, 113, 114, 115, 116, 117, 118 },
                                "owner-conflict-negative-b",
                                0x70010090,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A0,
                                        "conflict-negative-lock-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdA,
                                                    3U,
                                                    clientIdA,
                                                    "lock-conflict-negative-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 firstLockStateId = firstLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected the negative conflict setup to return a lock stateid.");

                            COMPOUND4res deniedLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A1,
                                        "conflict-negative-lock-b",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateIdB,
                                                    3U,
                                                    clientIdB,
                                                    "lock-conflict-negative-b",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badUnlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100A2,
                                        "conflict-negative-locku",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    new stateid4
                                                    {
                                                        seqid = firstLockStateId.seqid,
                                                        other = new byte[12],
                                                    },
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (firstLockResult.status != nfsstat4.NFS4_OK
                                || deniedLockResult.status != nfsstat4.NFS4ERR_DENIED
                                || badUnlockResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected conflicting locks to remain denied until unlock and bogus unlock stateids to fail.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "ReclaimAfterLeaseRecovery",
                        displayName: "NFSv4.0 COMPOUND reclaims open and lock state successfully during the simulated recovery grace period",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero));
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(
                                    cancellationToken,
                                    clock,
                                    leaseWindow: TimeSpan.FromMinutes(5),
                                    gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                            (ulong clientId, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reclaim-client",
                                new byte[] { 121, 122, 123, 124, 125, 126, 127, 128 },
                                "owner-reclaim-a",
                                0x700100B0,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B4,
                                        "reclaim-lock-before-recovery",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateId,
                                                    3U,
                                                    clientId,
                                                    "lock-reclaim-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (firstLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the initial lock setup to succeed before simulated recovery.");
                            }

                            service.SimulateRecovery();
                            if (!service.IsGracePeriodActive())
                            {
                                throw new InvalidOperationException("Expected simulated recovery to start the NFSv4.0 grace period.");
                            }

                            COMPOUND4res reclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B5,
                                        "reclaim-open",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "owner-reclaim-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            OPEN4resok reclaimOpenResok = reclaimOpenResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected reclaim OPEN to return OPEN4resok.");
                            stateid4 reclaimedOpenStateId = reclaimOpenResok.stateid
                                ?? throw new InvalidOperationException("Expected reclaim OPEN to return a reclaimed open stateid.");

                            COMPOUND4res reclaimLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B6,
                                        "reclaim-lock",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    reclaimedOpenStateId,
                                                    2U,
                                                    clientId,
                                                    "lock-reclaim-a",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL,
                                                    reclaim: true),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            stateid4 reclaimedLockStateId = reclaimLockResult.resarray?[1].oplock?.resok4?.lock_stateid
                                ?? throw new InvalidOperationException("Expected reclaim LOCK to return a reclaimed lock stateid.");

                            COMPOUND4res unlockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B7,
                                        "reclaim-unlock",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKU,
                                                oplocku = CreateUnlockArguments(
                                                    reclaimedLockStateId,
                                                    2U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res closeResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100B8,
                                        "reclaim-close",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_CLOSE,
                                                opclose = new CLOSE4args
                                                {
                                                    seqid = CreateSequenceId(3U),
                                                    open_stateid = reclaimedOpenStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (reclaimOpenResult.status != nfsstat4.NFS4_OK
                                || (reclaimOpenResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) != 0U
                                || reclaimLockResult.status != nfsstat4.NFS4_OK
                                || unlockResult.status != nfsstat4.NFS4_OK
                                || closeResult.status != nfsstat4.NFS4_OK
                                || closeResult.resarray?[0].opclose?.open_stateid?.seqid != 2U)
                            {
                                throw new InvalidOperationException("Expected reclaim OPEN and LOCK to succeed during grace without requiring OPEN_CONFIRM, and to allow subsequent unlock and close.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "ReclaimAfterLeaseRecoveryNegative",
                        displayName: "NFSv4.0 COMPOUND surfaces GRACE, RECLAIM_BAD, and NO_GRACE results around simulated lease recovery",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 13, 0, 0, TimeSpan.Zero));
                            (_, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle) =
                                await CreateLockingServiceAsync(
                                    cancellationToken,
                                    clock,
                                    leaseWindow: TimeSpan.FromMinutes(5),
                                    gracePeriodDuration: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                            (ulong clientId, stateid4 confirmedOpenStateId) = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reclaim-negative-client",
                                new byte[] { 131, 132, 133, 134, 135, 136, 137, 138 },
                                "owner-reclaim-negative",
                                0x700100C0,
                                cancellationToken).ConfigureAwait(false);

                            COMPOUND4res firstLockResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C4,
                                        "reclaim-negative-lock-before-recovery",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCK,
                                                oplock = CreateLockFromOpenArguments(
                                                    confirmedOpenStateId,
                                                    3U,
                                                    clientId,
                                                    "lock-reclaim-negative",
                                                    1U,
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            if (firstLockResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException("Expected the negative reclaim setup lock to succeed before simulated recovery.");
                            }

                            service.SimulateRecovery();

                            COMPOUND4res graceOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C5,
                                        "grace-open-denied",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "owner-reclaim-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res graceLockTestResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C6,
                                        "grace-lockt-denied",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_LOCKT,
                                                oplockt = CreateLockTestArguments(
                                                    clientId,
                                                    "lock-reclaim-negative",
                                                    nfs_lock_type4.WRITE_LT,
                                                    0UL,
                                                    5UL),
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res badReclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C7,
                                        "reclaim-open-bad-owner",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "wrong-owner",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            clock.Advance(TimeSpan.FromMinutes(6));
                            COMPOUND4res lateReclaimOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100C8,
                                        "reclaim-open-late",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateReclaimOpenArguments(
                                                    clientId,
                                                    "owner-reclaim-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE),
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETFH,
                                            },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (graceOpenResult.status != nfsstat4.NFS4ERR_GRACE
                                || graceLockTestResult.status != nfsstat4.NFS4ERR_GRACE
                                || badReclaimOpenResult.status != nfsstat4.NFS4ERR_RECLAIM_BAD
                                || lateReclaimOpenResult.status != nfsstat4.NFS4ERR_NO_GRACE
                                || service.IsGracePeriodActive())
                            {
                                throw new InvalidOperationException("Expected simulated recovery to deny non-reclaim operations during grace, reject bad reclaim owners, and reject late reclaim after grace expires.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "DelegationRecallRoundTripPositive",
                        displayName: "NFSv4.0 grants, recalls, and returns delegations before completing conflicting opens",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsDelegations delegations = new TestNfsDelegations(
                                new Dictionary<string, OpenNFS.Server.Delegations.NfsDelegationKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\exports\docs\notes.txt"] = OpenNFS.Server.Delegations.NfsDelegationKind.Read,
                                });
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
                                .UseDelegations(delegations)
                                .AddExport("/", @"C:\exports")
                                .Build();
                            Nfs40CompoundService service = new Nfs40CompoundService(server);
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            (ulong clientAId, byte[] clientAConfirm) = await CreateClientSessionAsync(
                                service,
                                "delegation-client-a",
                                new byte[] { 11, 12, 13, 14, 15, 16, 17, 18 },
                                0x700100D0U,
                                cancellationToken).ConfigureAwait(false);
                            COMPOUND4res openAResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D2,
                                        "delegation-open-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientAId,
                                                    "delegation-owner-a",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));
                            OPEN4resok openAResok = openAResult.resarray?[1].opopen?.resok4
                                ?? throw new InvalidOperationException("Expected OPEN A to return a success arm.");
                            stateid4 openAStateId = openAResok.stateid
                                ?? throw new InvalidOperationException("Expected OPEN A to return a stateid.");
                            stateid4 delegationStateId = openAResok.delegation?.read?.stateid
                                ?? throw new InvalidOperationException("Expected OPEN A to return a read delegation stateid.");

                            COMPOUND4res openAConfirmResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D3,
                                        "delegation-open-confirm-a",
                                        0U,
                                        new[]
                                        {
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                                opopen_confirm = new OPEN_CONFIRM4args
                                                {
                                                    open_stateid = openAStateId,
                                                    seqid = CreateSequenceId(2U),
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            (ulong clientBId, byte[] clientBConfirm) = await CreateClientSessionAsync(
                                service,
                                "delegation-client-b",
                                new byte[] { 21, 22, 23, 24, 25, 26, 27, 28 },
                                0x700100D4U,
                                cancellationToken).ConfigureAwait(false);
                            COMPOUND4res delayedOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D6,
                                        "delegation-open-b-delayed",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientBId,
                                                    "delegation-owner-b",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res delegReturnResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D7,
                                        "delegation-return-a",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_DELEGRETURN,
                                                opdelegreturn = new DELEGRETURN4args
                                                {
                                                    deleg_stateid = delegationStateId,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res retryOpenResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100D8,
                                        "delegation-open-b-retry",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientBId,
                                                    "delegation-owner-b",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (clientAId == 0UL
                                || clientAConfirm.Length != 8
                                || openAResult.status != nfsstat4.NFS4_OK
                                || openAResok.delegation?.delegation_type != open_delegation_type4.OPEN_DELEGATE_READ
                                || openAConfirmResult.status != nfsstat4.NFS4_OK
                                || clientBId == 0UL
                                || clientBConfirm.Length != 8
                                || delayedOpenResult.status != nfsstat4.NFS4ERR_DELAY
                                || !delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                || delegReturnResult.status != nfsstat4.NFS4_OK
                                || !delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                || retryOpenResult.status != nfsstat4.NFS4_OK)
                            {
                                throw new InvalidOperationException(
                                    "Expected NFSv4.0 delegation grant, recall, and DELEGRETURN retry behavior to succeed on the raw COMPOUND path."
                                    + " Observed:"
                                    + " openAStatus=" + openAResult.status
                                    + ", openADelegationType=" + (openAResok.delegation?.delegation_type?.ToString() ?? "<null>")
                                    + ", openAConfirmStatus=" + openAConfirmResult.status
                                    + ", delayedOpenStatus=" + delayedOpenResult.status
                                    + ", wasRecalled=" + delegations.WasRecalled(@"C:\exports\docs\notes.txt")
                                    + ", recalls=" + delegations.DescribeRecalls()
                                    + ", delegReturnStatus=" + delegReturnResult.status
                                    + ", wasReturned=" + delegations.WasReturned(@"C:\exports\docs\notes.txt")
                                    + ", returns=" + delegations.DescribeReturns()
                                    + ", retryOpenStatus=" + retryOpenResult.status
                                    + ".");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "DelegationRecallRoundTripNegative",
                        displayName: "NFSv4.0 keeps delegations unadvertised when the host capability is absent and rejects invalid returns",
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
                            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                                cancellationToken).ConfigureAwait(false);
                            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                                cancellationToken).ConfigureAwait(false);

                            (ulong clientId, _) = await CreateClientSessionAsync(
                                service,
                                "delegation-client-negative",
                                new byte[] { 31, 32, 33, 34, 35, 36, 37, 38 },
                                0x700100E0U,
                                cancellationToken).ConfigureAwait(false);
                            COMPOUND4res openResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E2,
                                        "delegation-open-negative",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(docsHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_OPEN,
                                                opopen = CreateOpenExistingArguments(
                                                    clientId,
                                                    "delegation-owner-negative",
                                                    1U,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_READ,
                                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                                    "notes.txt"),
                                            },
                                            new nfs_argop4 { argop = nfs_opnum4.OP_GETFH },
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_GETATTR,
                                                opgetattr = new GETATTR4args
                                                {
                                                    attr_request = CreateReadDirectoryArguments(0UL, new byte[8], 4096U).attr_request,
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            COMPOUND4res invalidReturnResult = ReadCompoundReply(
                                await service.DispatchAsync(
                                    CreateCompoundCall(
                                        0x700100E3,
                                        "delegation-return-negative",
                                        0U,
                                        new[]
                                        {
                                            CreatePutFileHandleArgop(notesHandle),
                                            new nfs_argop4
                                            {
                                                argop = nfs_opnum4.OP_DELEGRETURN,
                                                opdelegreturn = new DELEGRETURN4args
                                                {
                                                    deleg_stateid = new stateid4
                                                    {
                                                        seqid = 1U,
                                                        other = new byte[12],
                                                    },
                                                },
                                            },
                                        }),
                                    cancellationToken).ConfigureAwait(false));

                            if (openResult.status != nfsstat4.NFS4_OK
                                || openResult.resarray?[1].opopen?.resok4?.delegation?.delegation_type != open_delegation_type4.OPEN_DELEGATE_NONE
                                || invalidReturnResult.status != nfsstat4.NFS4ERR_BAD_STATEID)
                            {
                                throw new InvalidOperationException("Expected NFSv4.0 OPEN to avoid advertising delegations when disabled and DELEGRETURN to reject an unknown delegation stateid.");
                            }
                        }),
                });
        }

        private static async Task<(OpenNfsServer Server, Nfs40CompoundService Service, NfsFileHandle DocsHandle, NfsFileHandle NotesHandle)> CreateLockingServiceAsync(
            CancellationToken cancellationToken,
            MutableClock? clock = null,
            TimeSpan? leaseWindow = null,
            TimeSpan? gracePeriodDuration = null)
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                });

            await fileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    Encoding.UTF8.GetBytes("hello-v4"),
                    NfsWriteStability.FileSync,
                    cancellationToken)).ConfigureAwait(false);

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .Build();

            Nfs40CompoundService service = new Nfs40CompoundService(
                server,
                leaseWindow,
                gracePeriodDuration,
                clock is null ? null : clock.UtcNow);
            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                cancellationToken).ConfigureAwait(false);

            return (server, service, docsHandle, notesHandle);
        }

        private static async Task<(ulong ClientId, byte[] ConfirmationVerifier)> CreateClientSessionAsync(
            Nfs40CompoundService service,
            string clientIdentifier,
            byte[] clientVerifier,
            uint xidBase,
            CancellationToken cancellationToken)
        {
            COMPOUND4res setClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase,
                        clientIdentifier + "-setclientid",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID,
                                opsetclientid = CreateSetClientIdArguments(clientIdentifier, clientVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid and confirmation verifier.");
            ulong clientId = setClientIdResok.clientid?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid.");
            byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier.");

            if (setClientIdResult.status != nfsstat4.NFS4_OK || confirmationVerifier.Length != 8)
            {
                throw new InvalidOperationException("Expected SETCLIENTID to succeed and return an eight-byte confirmation verifier.");
            }

            COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 1U,
                        clientIdentifier + "-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed.");
            }

            return (clientId, confirmationVerifier);
        }

        private static async Task<(ulong ClientId, stateid4 ConfirmedOpenStateId)> CreateConfirmedOpenStateForClientAsync(
            Nfs40CompoundService service,
            NfsFileHandle docsHandle,
            string clientIdentifier,
            byte[] clientVerifier,
            string openOwner,
            uint xidBase,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(docsHandle);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientIdentifier);
            ArgumentNullException.ThrowIfNull(clientVerifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(openOwner);

            COMPOUND4res setClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase,
                        clientIdentifier + "-setclientid",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID,
                                opsetclientid = CreateSetClientIdArguments(clientIdentifier, clientVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            SETCLIENTID4resok setClientIdResok = setClientIdResult.resarray?[0].opsetclientid?.resok4
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid and confirmation verifier for the lock setup flow.");
            ulong clientId = setClientIdResok.clientid?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a clientid for the lock setup flow.");
            byte[] confirmationVerifier = setClientIdResok.setclientid_confirm?.Value
                ?? throw new InvalidOperationException("Expected SETCLIENTID to return a confirmation verifier for the lock setup flow.");

            if (setClientIdResult.status != nfsstat4.NFS4_OK || confirmationVerifier.Length != 8)
            {
                throw new InvalidOperationException("Expected successful lock setup SETCLIENTID to return an eight-byte confirmation verifier.");
            }

            COMPOUND4res confirmClientIdResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 1U,
                        clientIdentifier + "-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                                opsetclientid_confirm = CreateSetClientIdConfirmArguments(clientId, confirmationVerifier),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            if (confirmClientIdResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected SETCLIENTID_CONFIRM to succeed for the lock setup flow.");
            }

            COMPOUND4res openResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 2U,
                        clientIdentifier + "-open",
                        0U,
                        new[]
                        {
                            CreatePutFileHandleArgop(docsHandle),
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_OPEN,
                                opopen = CreateOpenExistingArguments(
                                    clientId,
                                    openOwner,
                                    1U,
                                    (uint)Nfs40Constants.OPEN4_SHARE_ACCESS_BOTH,
                                    (uint)Nfs40Constants.OPEN4_SHARE_DENY_NONE,
                                    "notes.txt"),
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            stateid4 openStateId = openResult.resarray?[1].opopen?.resok4?.stateid
                ?? throw new InvalidOperationException("Expected OPEN to return a stateid for the lock setup flow.");

            if (openResult.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException("Expected OPEN to succeed for the lock setup flow.");
            }

            COMPOUND4res openConfirmResult = ReadCompoundReply(
                await service.DispatchAsync(
                    CreateCompoundCall(
                        xidBase + 3U,
                        clientIdentifier + "-open-confirm",
                        0U,
                        new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_OPEN_CONFIRM,
                                opopen_confirm = new OPEN_CONFIRM4args
                                {
                                    open_stateid = openStateId,
                                    seqid = CreateSequenceId(2U),
                                },
                            },
                        }),
                    cancellationToken).ConfigureAwait(false));
            stateid4 confirmedStateId = openConfirmResult.resarray?[0].opopen_confirm?.resok4?.open_stateid
                ?? throw new InvalidOperationException("Expected OPEN_CONFIRM to return the confirmed stateid for the lock setup flow.");

            if (openConfirmResult.status != nfsstat4.NFS4_OK || confirmedStateId.seqid != 2U)
            {
                throw new InvalidOperationException("Expected OPEN_CONFIRM to advance the stateid sequence during the lock setup flow.");
            }

            return (clientId, confirmedStateId);
        }

        private static nfs_argop4 CreatePutFileHandleArgop(NfsFileHandle fileHandle)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);

            return new nfs_argop4
            {
                argop = nfs_opnum4.OP_PUTFH,
                opputfh = new PUTFH4args
                {
                    @object = new nfs_fh4
                    {
                        Value = fileHandle.ToArray(),
                    },
                },
            };
        }

        private static LOCKT4args CreateLockTestArguments(
            ulong clientId,
            string lockOwner,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
        {
            return new LOCKT4args
            {
                locktype = lockType,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                owner = CreateLockOwner(clientId, lockOwner),
            };
        }

        private static LOCK4args CreateLockFromOpenArguments(
            stateid4 openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length,
            bool reclaim = false)
        {
            ArgumentNullException.ThrowIfNull(openStateId);

            return new LOCK4args
            {
                locktype = lockType,
                reclaim = reclaim,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                locker = new locker4
                {
                    new_lock_owner = true,
                    open_owner = new open_to_lock_owner4
                    {
                        open_seqid = CreateSequenceId(openSequenceId),
                        open_stateid = openStateId,
                        lock_seqid = CreateSequenceId(lockSequenceId),
                        lock_owner = CreateLockOwner(clientId, lockOwner),
                    },
                },
            };
        }

        private static LOCK4args CreateLockExistingArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length,
            bool reclaim = false)
        {
            ArgumentNullException.ThrowIfNull(lockStateId);

            return new LOCK4args
            {
                locktype = lockType,
                reclaim = reclaim,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
                locker = new locker4
                {
                    new_lock_owner = false,
                    lock_owner = new exist_lock_owner4
                    {
                        lock_stateid = lockStateId,
                        lock_seqid = CreateSequenceId(lockSequenceId),
                    },
                },
            };
        }

        private static LOCKU4args CreateUnlockArguments(
            stateid4 lockStateId,
            uint lockSequenceId,
            nfs_lock_type4 lockType,
            ulong offset,
            ulong length)
        {
            ArgumentNullException.ThrowIfNull(lockStateId);

            return new LOCKU4args
            {
                locktype = lockType,
                seqid = CreateSequenceId(lockSequenceId),
                lock_stateid = lockStateId,
                offset = new offset4 { Value = offset },
                length = new length4 { Value = length },
            };
        }

        private static seqid4 CreateSequenceId(uint value)
        {
            return new seqid4
            {
                Value = value,
            };
        }

        private static lock_owner4 CreateLockOwner(ulong clientId, string lockOwner)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(lockOwner);

            return new lock_owner4
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                owner = Encoding.UTF8.GetBytes(lockOwner),
            };
        }

        private static OPEN4args CreateReclaimOpenArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny)
        {
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId),
                share_access = shareAccess,
                share_deny = shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(openOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_PREVIOUS,
                    delegate_type = open_delegation_type4.OPEN_DELEGATE_NONE,
                },
            };
        }

        private static RpcMessageEnvelope CreateCompoundCall(
            uint xid,
            string tag,
            uint minorVersion,
            nfs_argop4[] operations)
        {
            XdrWriter writer = new XdrWriter();
            new COMPOUND4args
            {
                tag = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes(tag),
                    },
                },
                minorversion = minorVersion,
                argarray = operations,
            }.WriteTo(writer);

            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: writer.ToArray());
        }

        private static COMPOUND4res ReadCompoundReply(RpcMessageEnvelope reply)
        {
            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv4.0 COMPOUND result.");
            }

            return Nfs40CompoundPayloadCodec.ReadPayload(reply.ProcedurePayload, COMPOUND4res.ReadFrom);
        }

        private static string ReadUtf8(utf8str_cs? value)
        {
            byte[] bytes = value?.Value?.Value ?? Array.Empty<byte>();
            return Encoding.UTF8.GetString(bytes);
        }

        private static stateid4 CreateAnonymousStateId()
        {
            return new stateid4
            {
                seqid = 0,
                other = new byte[12],
            };
        }

        private static fattr4 CreateAclAttributes(IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            XdrWriter writer = new XdrWriter();
            nfsace4[] mappedEntries = new nfsace4[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                NfsAclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL entry collections cannot contain null entries.");
                mappedEntries[index] = new nfsace4
                {
                    type = new acetype4
                    {
                        Value = (uint)entry.EntryType,
                    },
                    flag = new aceflag4
                    {
                        Value = (uint)entry.EntryFlags,
                    },
                    access_mask = new acemask4
                    {
                        Value = (uint)entry.Permissions,
                    },
                    who = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(entry.Who),
                        },
                    },
                };
            }

            new fattr4_acl
            {
                Value = mappedEntries,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_ACL),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        private static LOOKUP4args CreateLookupArguments(string entryName)
        {
            return new LOOKUP4args
            {
                objname = CreatePathComponent(entryName),
            };
        }

        private static SETCLIENTID4args CreateSetClientIdArguments(string clientIdentifier, byte[] clientVerifier)
        {
            return new SETCLIENTID4args
            {
                client = new nfs_client_id4
                {
                    verifier = new verifier4
                    {
                        Value = clientVerifier,
                    },
                    id = Encoding.UTF8.GetBytes(clientIdentifier),
                },
                callback = new cb_client4
                {
                    cb_program = 0U,
                    cb_location = new clientaddr4
                    {
                        r_netid = string.Empty,
                        r_addr = string.Empty,
                    },
                },
                callback_ident = 0U,
            };
        }

        private static SETCLIENTID_CONFIRM4args CreateSetClientIdConfirmArguments(ulong clientId, byte[] confirmationVerifier)
        {
            return new SETCLIENTID_CONFIRM4args
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                setclientid_confirm = new verifier4
                {
                    Value = confirmationVerifier,
                },
            };
        }

        private static OPEN4args CreateOpenExistingArguments(
            ulong clientId,
            string openOwner,
            uint sequenceId,
            uint shareAccess,
            uint shareDeny,
            string entryName)
        {
            return new OPEN4args
            {
                seqid = new seqid4
                {
                    Value = sequenceId,
                },
                share_access = shareAccess,
                share_deny = shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = Encoding.UTF8.GetBytes(openOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_NULL,
                    file = CreatePathComponent(entryName),
                },
            };
        }

        private static component4 CreatePathComponent(string entryName)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes(entryName),
                    },
                },
            };
        }

        private static fattr4 CreateEmptyAttributes()
        {
            return new fattr4
            {
                attrmask = new bitmap4
                {
                    Value = Array.Empty<uint>(),
                },
                attr_vals = new attrlist4
                {
                    Value = Array.Empty<byte>(),
                },
            };
        }

        private static READDIR4args CreateReadDirectoryArguments(ulong cookie, byte[] cookieVerifier, uint maxCount)
        {
            return new READDIR4args
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                cookieverf = new verifier4
                {
                    Value = cookieVerifier,
                },
                dircount = new count4
                {
                    Value = maxCount,
                },
                maxcount = new count4
                {
                    Value = maxCount,
                },
                attr_request = new bitmap4
                {
                    Value = new[]
                    {
                        (1U << (int)Nfs40Constants.FATTR4_TYPE)
                        | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                        | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                        | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                    },
                },
            };
        }

        private static WRITE4args CreateWriteArguments(
            stateid4 stateId,
            ulong offset,
            stable_how4 stability,
            byte[] data)
        {
            ArgumentNullException.ThrowIfNull(stateId);
            ArgumentNullException.ThrowIfNull(data);

            return new WRITE4args
            {
                stateid = stateId,
                offset = new offset4
                {
                    Value = offset,
                },
                stable = stability,
                data = data,
            };
        }

        private static COMMIT4args CreateCommitArguments(ulong offset, uint count)
        {
            return new COMMIT4args
            {
                offset = new offset4
                {
                    Value = offset,
                },
                count = new count4
                {
                    Value = count,
                },
            };
        }
    }
}
