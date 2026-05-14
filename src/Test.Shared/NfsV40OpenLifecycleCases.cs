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
    /// Open-close-reopen lifecycle NFSv4.0 suites.
    /// </summary>
    internal static class NfsV40OpenLifecycleCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NfsV40Suites",
                        caseId: "OpenCloseReopen",
                        displayName: "NFSv4.0 COMPOUND reopens a file successfully after CLOSE with the same open owner",
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
                                "suite-reopen-client-a",
                                new byte[] { 61, 62, 63, 64, 65, 66, 67, 68 },
                                "owner-reopen-a",
                                0x70010050,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = confirmedOpenStateContext.ClientId;
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

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
                            LockingServiceContext lockingServiceContext =
                                await CreateLockingServiceAsync(cancellationToken).ConfigureAwait(false);
                            Nfs40CompoundService service = lockingServiceContext.Service;
                            NfsFileHandle docsHandle = lockingServiceContext.DocsHandle;

                            ConfirmedOpenStateContext confirmedOpenStateContext = await CreateConfirmedOpenStateForClientAsync(
                                service,
                                docsHandle,
                                "suite-reopen-client-negative",
                                new byte[] { 71, 72, 73, 74, 75, 76, 77, 78 },
                                "owner-reopen-negative",
                                0x70010057,
                                cancellationToken).ConfigureAwait(false);
                            ulong clientId = confirmedOpenStateContext.ClientId;
                            stateid4 confirmedOpenStateId = confirmedOpenStateContext.ConfirmedOpenStateId;

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
            };
        }
    }
}
