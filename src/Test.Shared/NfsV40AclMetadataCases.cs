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
    /// ACL round-trip and negative ACL capability NFSv4.0 metadata suites.
    /// </summary>
    internal static class NfsV40AclMetadataCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}

