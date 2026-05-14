namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV40SuiteSupport;

    /// <summary>
    /// NFSv4.0 COMPOUND filehandle and short-circuit semantics cases.
    /// </summary>
    internal static class NfsV40CompoundFileHandleCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
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
            };
        }
    }
}
