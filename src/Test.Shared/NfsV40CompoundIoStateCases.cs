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
    /// Compound write, commit, and stateid-validation NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40CompoundIoStateCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "CompoundWriteAndCommitPositive",
                        displayName: "NFSv4.0 COMPOUND serves successful WRITE, COMMIT, and read-back flows with a confirmed open stateid",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            LockingServiceContext lockingServiceContext =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-write-client-a",
                                new byte[] { 59, 60, 61, 62, 63, 64, 65, 66 },
                                "owner-write-a",
                                0x70010048,
                                cancellationToken).ConfigureAwait(false);
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

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
                            LockingServiceContext lockingServiceContext =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;
                            NfsFileHandle notesHandle = lockingServiceContext.NotesHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-write-client-negative-a",
                                new byte[] { 67, 68, 69, 70, 71, 72, 73, 74 },
                                "owner-write-negative-a",
                                0x70010060,
                                cancellationToken).ConfigureAwait(false);
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

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
            };
        }
    }
}
