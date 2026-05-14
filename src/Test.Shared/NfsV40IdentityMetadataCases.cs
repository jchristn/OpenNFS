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
    /// SECINFO and identity-attribute NFSv4.0 metadata suites.
    /// </summary>
    internal static class NfsV40IdentityMetadataCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}

