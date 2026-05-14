namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NfsV40SuiteSupport;

    /// <summary>
    /// Additional public-root, verify, and capability-gated NFSv4.0 COMPOUND cases.
    /// </summary>
    internal static class NfsV40CompoundAdditionalCoreCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "NfsV40Suites",
                    caseId: "CompoundAdditionalCoreOperationsPositiveAndNegative",
                    displayName: "NFSv4.0 COMPOUND serves explicit public-root, verify, and capability-gated core operation results",
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
                        NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports"),
                            cancellationToken).ConfigureAwait(false);

                        COMPOUND4res publicRootResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x70010009,
                                    "putpubfh-verify-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTPUBFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_VERIFY,
                                            opverify = new VERIFY4args
                                            {
                                                obj_attributes = CreateTypeAttributes(nfs_ftype4.NF4DIR),
                                            },
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_GETFH,
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        byte[]? publicHandleBytes = publicRootResult.resarray?[2].opgetfh?.resok4?.@object?.Value;
                        if (publicRootResult.status != nfsstat4.NFS4_OK
                            || publicRootResult.resarray is null
                            || publicRootResult.resarray.Length != 3
                            || publicRootResult.resarray[0].opputpubfh?.status != nfsstat4.NFS4_OK
                            || publicRootResult.resarray[1].opverify?.status != nfsstat4.NFS4_OK
                            || publicRootResult.resarray[2].opgetfh?.status != nfsstat4.NFS4_OK
                            || publicHandleBytes is null
                            || !publicHandleBytes.AsSpan().SequenceEqual(rootHandle.ToArray()))
                        {
                            throw new InvalidOperationException("Expected PUTPUBFH to expose the current public/root handle and allow a matching VERIFY sequence to succeed.");
                        }

                        COMPOUND4res nverifyDifferentResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000A,
                                    "nverify-positive",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTPUBFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_NVERIFY,
                                            opnverify = new NVERIFY4args
                                            {
                                                obj_attributes = CreateTypeAttributes(nfs_ftype4.NF4REG),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (nverifyDifferentResult.status != nfsstat4.NFS4_OK
                            || nverifyDifferentResult.resarray is null
                            || nverifyDifferentResult.resarray.Length != 2
                            || nverifyDifferentResult.resarray[1].opnverify?.status != nfsstat4.NFS4_OK)
                        {
                            throw new InvalidOperationException("Expected NVERIFY to succeed when the supplied attribute payload differs from the current object.");
                        }

                        COMPOUND4res verifyMismatchResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000B,
                                    "verify-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTPUBFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_VERIFY,
                                            opverify = new VERIFY4args
                                            {
                                                obj_attributes = CreateTypeAttributes(nfs_ftype4.NF4REG),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (verifyMismatchResult.status != nfsstat4.NFS4ERR_NOT_SAME
                            || verifyMismatchResult.resarray is null
                            || verifyMismatchResult.resarray.Length != 2
                            || verifyMismatchResult.resarray[1].opverify?.status != nfsstat4.NFS4ERR_NOT_SAME)
                        {
                            throw new InvalidOperationException("Expected VERIFY to return NFS4ERR_NOT_SAME when the supplied attribute payload does not match the current object.");
                        }

                        COMPOUND4res nverifySameResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000C,
                                    "nverify-negative",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTPUBFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_NVERIFY,
                                            opnverify = new NVERIFY4args
                                            {
                                                obj_attributes = CreateTypeAttributes(nfs_ftype4.NF4DIR),
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (nverifySameResult.status != nfsstat4.NFS4ERR_SAME
                            || nverifySameResult.resarray is null
                            || nverifySameResult.resarray.Length != 2
                            || nverifySameResult.resarray[1].opnverify?.status != nfsstat4.NFS4ERR_SAME)
                        {
                            throw new InvalidOperationException("Expected NVERIFY to return NFS4ERR_SAME when the supplied attribute payload matches the current object.");
                        }

                        COMPOUND4res openAttributeResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000D,
                                    "openattr-notsupp",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_PUTPUBFH,
                                        },
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_OPENATTR,
                                            opopenattr = new OPENATTR4args
                                            {
                                                createdir = false,
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (openAttributeResult.status != nfsstat4.NFS4ERR_NOTSUPP
                            || openAttributeResult.resarray is null
                            || openAttributeResult.resarray.Length != 2
                            || openAttributeResult.resarray[1].opopenattr?.status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("Expected OPENATTR to return an explicit NOTSUPP path when named attributes are not advertised.");
                        }

                        COMPOUND4res delegationPurgeResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000E,
                                    "delegpurge-notsupp",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_DELEGPURGE,
                                            opdelegpurge = new DELEGPURGE4args
                                            {
                                                clientid = new clientid4
                                                {
                                                    Value = 0UL,
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (delegationPurgeResult.status != nfsstat4.NFS4ERR_NOTSUPP
                            || delegationPurgeResult.resarray is null
                            || delegationPurgeResult.resarray.Length != 1
                            || delegationPurgeResult.resarray[0].opdelegpurge?.status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("Expected DELEGPURGE to bind to an explicit NOTSUPP capability-gated response on the current delegation surface.");
                        }

                        COMPOUND4res releaseLockOwnerResult = ReadCompoundReply(
                            await service.DispatchAsync(
                                CreateCompoundCall(
                                    0x7001000F,
                                    "release-lockowner-notsupp",
                                    0U,
                                    new[]
                                    {
                                        new nfs_argop4
                                        {
                                            argop = nfs_opnum4.OP_RELEASE_LOCKOWNER,
                                            oprelease_lockowner = new RELEASE_LOCKOWNER4args
                                            {
                                                lock_owner = new lock_owner4
                                                {
                                                    clientid = new clientid4
                                                    {
                                                        Value = 0UL,
                                                    },
                                                    owner = new byte[] { 0x01 },
                                                },
                                            },
                                        },
                                    }),
                                cancellationToken).ConfigureAwait(false));

                        if (releaseLockOwnerResult.status != nfsstat4.NFS4ERR_NOTSUPP
                            || releaseLockOwnerResult.resarray is null
                            || releaseLockOwnerResult.resarray.Length != 1
                            || releaseLockOwnerResult.resarray[0].oprelease_lockowner?.status != nfsstat4.NFS4ERR_NOTSUPP)
                        {
                            throw new InvalidOperationException("Expected RELEASE_LOCKOWNER to bind to an explicit NOTSUPP response instead of falling through to OP_ILLEGAL.");
                        }
                    }),
            };
        }
    }
}
