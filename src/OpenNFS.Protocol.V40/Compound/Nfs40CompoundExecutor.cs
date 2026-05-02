namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs40CompoundExecutor
    {
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;
        private readonly Nfs40WriteStateTracker _writeState;

        internal Nfs40CompoundExecutor(OpenNfsServer server)
            : this(server, leaseWindow: null, gracePeriodDuration: null, utcNow: null)
        {
        }

        internal Nfs40CompoundExecutor(
            OpenNfsServer server,
            TimeSpan? leaseWindow,
            TimeSpan? gracePeriodDuration,
            Func<DateTimeOffset>? utcNow)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
            _stateManager = new Nfs40StateManager(leaseWindow, gracePeriodDuration, utcNow);
            _writeState = Nfs40WriteStateTracker.ForServer(server);
        }

        internal bool IsGracePeriodActive()
        {
            return _stateManager.IsGracePeriodActive();
        }

        internal void SimulateRecovery()
        {
            _stateManager.SimulateRecovery();
        }

        private async Task ReleaseExpiredLocksAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Nfs40StateManager.Nfs40ExpiredLockCleanup> expiredLockCleanups =
                _stateManager.DrainExpiredLockCleanups();
            if (expiredLockCleanups.Count == 0 || _server.Capabilities.Locking is null)
            {
                return;
            }

            for (int index = 0; index < expiredLockCleanups.Count; index++)
            {
                Nfs40StateManager.Nfs40ExpiredLockCleanup cleanup = expiredLockCleanups[index];
                NfsLockRequest unlockRequest = CreateHostLockRequest(
                    NfsLockOperation.Unlock,
                    cleanup.Target,
                    cleanup.ClientId,
                    cleanup.OwnerBytes,
                    cleanup.Offset,
                    cleanup.Length,
                    cleanup.Exclusive,
                    block: false,
                    reclaim: false,
                    cancellationToken);
                _ = await _server.Capabilities.Locking.ProcessLockAsync(unlockRequest).ConfigureAwait(false);
            }
        }

        internal async Task<COMPOUND4res> ExecuteAsync(COMPOUND4args arguments, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            cancellationToken.ThrowIfCancellationRequested();

            await ReleaseExpiredLocksAsync(cancellationToken).ConfigureAwait(false);

            if (arguments.minorversion != 0U)
            {
                return new COMPOUND4res
                {
                    status = nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH,
                    tag = CreateResponseTag(arguments.tag),
                    resarray = Array.Empty<nfs_resop4>(),
                };
            }

            nfs_argop4[] operations = arguments.argarray ?? Array.Empty<nfs_argop4>();
            List<nfs_resop4> results = new List<nfs_resop4>(operations.Length);
            Nfs40CompoundState state = new Nfs40CompoundState();
            nfsstat4 overallStatus = nfsstat4.NFS4_OK;

            for (int index = 0; index < operations.Length; index++)
            {
                Nfs40CompoundOperationResult operationResult =
                    await ExecuteOperationAsync(operations[index], state, cancellationToken).ConfigureAwait(false);
                await ReleaseExpiredLocksAsync(cancellationToken).ConfigureAwait(false);
                results.Add(operationResult.ResponseOperation);

                if (operationResult.Status != nfsstat4.NFS4_OK)
                {
                    overallStatus = operationResult.Status;
                    break;
                }
            }

            return new COMPOUND4res
            {
                status = overallStatus,
                tag = CreateResponseTag(arguments.tag),
                resarray = results.ToArray(),
            };
        }

        private static uint CreateAccessMask(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.Directory => (uint)(
                    Nfs40Constants.ACCESS4_READ
                    | Nfs40Constants.ACCESS4_LOOKUP
                    | Nfs40Constants.ACCESS4_MODIFY
                    | Nfs40Constants.ACCESS4_EXTEND
                    | Nfs40Constants.ACCESS4_DELETE),
                NfsPathKind.File => (uint)(
                    Nfs40Constants.ACCESS4_READ
                    | Nfs40Constants.ACCESS4_MODIFY
                    | Nfs40Constants.ACCESS4_EXTEND
                    | Nfs40Constants.ACCESS4_DELETE
                    | Nfs40Constants.ACCESS4_EXECUTE),
                NfsPathKind.SymbolicLink => (uint)Nfs40Constants.ACCESS4_READ,
                _ => 0U,
            };
        }

        private static utf8str_cs CreateResponseTag(utf8str_cs? requestTag)
        {
            byte[] tagBytes = requestTag?.Value?.Value?.AsSpan().ToArray() ?? Array.Empty<byte>();
            return new utf8str_cs
            {
                Value = new utf8string
                {
                    Value = tagBytes,
                },
            };
        }

        private static Nfs40CompoundOperationResult CreateAccessResult(nfsstat4 status, ACCESS4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_ACCESS,
                    opaccess = new ACCESS4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateCreateResult(nfsstat4 status, CREATE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CREATE,
                    opcreate = new CREATE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateCommitResult(
            nfsstat4 status,
            COMMIT4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_COMMIT,
                    opcommit = new COMMIT4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateDelegationPurgeResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_DELEGPURGE,
                    opdelegpurge = new DELEGPURGE4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateCloseResult(nfsstat4 status, stateid4? stateId = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLOSE,
                    opclose = new CLOSE4res
                    {
                        status = status,
                        open_stateid = status == nfsstat4.NFS4_OK ? stateId : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateNotVerifyResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_NVERIFY,
                    opnverify = new NVERIFY4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateOpenAttributeResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPENATTR,
                    opopenattr = new OPENATTR4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateGetAttrResult(nfsstat4 status, fattr4? attributes = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_GETATTR,
                    opgetattr = new GETATTR4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK
                            ? new GETATTR4resok
                            {
                                obj_attributes = attributes,
                            }
                            : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateGetFileHandleResult(
            nfsstat4 status,
            Nfs40CompoundResolvedHandle? resolvedHandle = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_GETFH,
                    opgetfh = new GETFH4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK && resolvedHandle is not null
                            ? new GETFH4resok
                            {
                                @object = new nfs_fh4
                                {
                                    Value = resolvedHandle.FileHandle.ToArray(),
                                },
                            }
                            : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateSetAttrResult(nfsstat4 status, bitmap4? attributesSet = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETATTR,
                    opsetattr = new SETATTR4res
                    {
                        status = status,
                        attrsset = attributesSet ?? Nfs40MutationSupport.CreateEmptyAttributeSet(),
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateIllegalOperationResult()
        {
            return new Nfs40CompoundOperationResult(
                nfsstat4.NFS4ERR_OP_ILLEGAL,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_ILLEGAL,
                    opillegal = new ILLEGAL4res
                    {
                        status = nfsstat4.NFS4ERR_OP_ILLEGAL,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLinkResult(nfsstat4 status, LINK4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LINK,
                    oplink = new LINK4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLockResult(
            nfsstat4 status,
            LOCK4resok? successPayload = null,
            LOCK4denied? deniedPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCK,
                    oplock = new LOCK4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK ? successPayload : null,
                        denied = status == nfsstat4.NFS4ERR_DENIED ? deniedPayload : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLockTestResult(
            nfsstat4 status,
            LOCK4denied? deniedPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCKT,
                    oplockt = new LOCKT4res
                    {
                        status = status,
                        denied = status == nfsstat4.NFS4ERR_DENIED ? deniedPayload : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLockUnlockResult(
            nfsstat4 status,
            stateid4? stateId = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCKU,
                    oplocku = new LOCKU4res
                    {
                        status = status,
                        lock_stateid = status == nfsstat4.NFS4_OK ? stateId : null,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateOpenConfirmResult(
            nfsstat4 status,
            OPEN_CONFIRM4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN_CONFIRM,
                    opopen_confirm = new OPEN_CONFIRM4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateOpenDowngradeResult(
            nfsstat4 status,
            OPEN_DOWNGRADE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN_DOWNGRADE,
                    opopen_downgrade = new OPEN_DOWNGRADE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateOpenResult(nfsstat4 status, OPEN4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateDelegationReturnResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_DELEGRETURN,
                    opdelegreturn = new DELEGRETURN4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLookupParentResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUPP,
                    oplookupp = new LOOKUPP4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateLookupResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreatePutFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreatePutPublicFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTPUBFH,
                    opputpubfh = new PUTPUBFH4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreatePutRootFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTROOTFH,
                    opputrootfh = new PUTROOTFH4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateReleaseLockOwnerResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RELEASE_LOCKOWNER,
                    oprelease_lockowner = new RELEASE_LOCKOWNER4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateReadDirectoryResult(
            nfsstat4 status,
            READDIR4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READDIR,
                    opreaddir = new READDIR4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateReadLinkResult(
            nfsstat4 status,
            READLINK4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READLINK,
                    opreadlink = new READLINK4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateReadResult(nfsstat4 status, READ4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ,
                    opread = new READ4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateRenewResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RENEW,
                    oprenew = new RENEW4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateRemoveResult(nfsstat4 status, REMOVE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateRenameResult(nfsstat4 status, RENAME4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RENAME,
                    oprename = new RENAME4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateRestoreFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RESTOREFH,
                    oprestorefh = new RESTOREFH4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateSaveFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SAVEFH,
                    opsavefh = new SAVEFH4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateSecurityInfoResult(
            nfsstat4 status,
            SECINFO4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SECINFO,
                    opsecinfo = new SECINFO4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateSetClientIdConfirmResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                    opsetclientid_confirm = new SETCLIENTID_CONFIRM4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateSetClientIdResult(
            nfsstat4 status,
            SETCLIENTID4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETCLIENTID,
                    opsetclientid = new SETCLIENTID4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateVerifyResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_VERIFY,
                    opverify = new VERIFY4res
                    {
                        status = status,
                    },
                });
        }

        private static Nfs40CompoundOperationResult CreateWriteResult(
            nfsstat4 status,
            WRITE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_WRITE,
                    opwrite = new WRITE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        private async Task<Nfs40CompoundOperationResult> ExecuteOperationAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            nfs_opnum4? operationNumber = operation.argop;
            if (!operationNumber.HasValue)
            {
                return CreateIllegalOperationResult();
            }

            switch (operationNumber.Value)
            {
                case nfs_opnum4.OP_ACCESS:
                    return await HandleAccessAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_CLOSE:
                    return HandleClose(operation);

                case nfs_opnum4.OP_CREATE:
                    return await HandleCreateAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_COMMIT:
                    return await HandleCommitAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_DELEGPURGE:
                    return HandleDelegationPurge(operation);

                case nfs_opnum4.OP_DELEGRETURN:
                    return await HandleDelegationReturnAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_GETATTR:
                    return await HandleGetAttributesAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_GETFH:
                    return HandleGetFileHandle(state);

                case nfs_opnum4.OP_LINK:
                    return await HandleLinkAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_LOCK:
                    return await HandleLockAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_LOCKT:
                    return await HandleLockTestAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_LOCKU:
                    return await HandleLockUnlockAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_LOOKUP:
                    return await HandleLookupAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_LOOKUPP:
                    return await HandleLookupParentAsync(state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_NVERIFY:
                    return await HandleVerifyAsync(
                        operation.opnverify?.obj_attributes,
                        state,
                        expectMatch: false,
                        cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_OPEN:
                    return await HandleOpenAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_OPENATTR:
                    return await HandleOpenAttributeAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_OPEN_CONFIRM:
                    return HandleOpenConfirm(operation);

                case nfs_opnum4.OP_OPEN_DOWNGRADE:
                    return HandleOpenDowngrade(operation);

                case nfs_opnum4.OP_PUTFH:
                    return await HandlePutFileHandleAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_PUTPUBFH:
                    return await HandlePutPublicFileHandleAsync(state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_PUTROOTFH:
                    return await HandlePutRootFileHandleAsync(state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_READ:
                    return await HandleReadAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_READDIR:
                    return await HandleReadDirectoryAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_READLINK:
                    return await HandleReadLinkAsync(state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_REMOVE:
                    return await HandleRemoveAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_RENAME:
                    return await HandleRenameAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_RENEW:
                    return HandleRenew(operation);

                case nfs_opnum4.OP_RESTOREFH:
                    return HandleRestoreFileHandle(state);

                case nfs_opnum4.OP_SAVEFH:
                    return HandleSaveFileHandle(state);

                case nfs_opnum4.OP_SECINFO:
                    return await HandleSecurityInfoAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_SETATTR:
                    return await HandleSetAttributesAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_SETCLIENTID:
                    return HandleSetClientId(operation);

                case nfs_opnum4.OP_SETCLIENTID_CONFIRM:
                    return HandleSetClientIdConfirm(operation);

                case nfs_opnum4.OP_VERIFY:
                    return await HandleVerifyAsync(
                        operation.opverify?.obj_attributes,
                        state,
                        expectMatch: true,
                        cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_WRITE:
                    return await HandleWriteAsync(operation, state, cancellationToken).ConfigureAwait(false);

                case nfs_opnum4.OP_RELEASE_LOCKOWNER:
                    return HandleReleaseLockOwner(operation);

                default:
                    return CreateIllegalOperationResult();
            }
        }

        private async Task<Nfs40CompoundOperationResult> HandleAccessAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            ACCESS4args? arguments = operation.opaccess;
            if (arguments is null)
            {
                return CreateAccessResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateAccessResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateAccessResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateAccessResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            uint supported = CreateAccessMask(refreshedHandle.PathInfo.Kind);
            return CreateAccessResult(
                nfsstat4.NFS4_OK,
                new ACCESS4resok
                {
                    supported = supported,
                    access = supported & arguments.access,
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleCreateAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            CREATE4args? arguments = operation.opcreate;
            nfs_ftype4? requestedType = arguments?.objtype?.type;
            if (arguments is null || !requestedType.HasValue)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_BADXDR);
            }

            createtype4 objectType = arguments.objtype!;

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedDirectory, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedDirectory is null)
            {
                return CreateCreateResult(refreshStatus);
            }

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.objname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateCreateResult(nameStatus);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;
            Nfs40CompoundResolvedHandle? createdHandle = null;

            switch (requestedType.Value)
            {
                case nfs_ftype4.NF4DIR:
                    try
                    {
                        NfsCreatePathResponse createDirectoryResponse =
                            await _server.Settings.FileSystem.CreatePathAsync(
                                new NfsCreatePathRequest(
                                    refreshedDirectory.Target.SourcePath,
                                    entryName,
                                    NfsPathKind.Directory,
                                    failIfExists: true,
                                    cancellationToken)).ConfigureAwait(false);

                        if (!createDirectoryResponse.CreatedNew)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_EXIST);
                        }

                        if (!createDirectoryResponse.PathInfo.Exists)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_SERVERFAULT);
                        }

                        if (createDirectoryResponse.PathInfo.Kind == NfsPathKind.Other)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
                        }

                        if (createDirectoryResponse.PathInfo.Kind != NfsPathKind.Directory)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_SERVERFAULT);
                        }

                        createdHandle = await CreateResolvedHandleAsync(
                            new NfsFileHandleTarget(
                                refreshedDirectory.Target.ExportPath,
                                createDirectoryResponse.PathInfo.Path),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (
                        exception is UnauthorizedAccessException
                        || exception is IOException
                        || exception is NotSupportedException
                        || exception is DirectoryNotFoundException
                        || exception is FileNotFoundException
                        || exception is PathTooLongException
                        || exception is ArgumentException)
                    {
                        return CreateCreateResult(Nfs40MutationSupport.MapCreateException(exception));
                    }

                    break;

                case nfs_ftype4.NF4LNK:
                    if (!Nfs40MutationSupport.TryReadLinkTarget(objectType.linkdata, out string targetPath, out nfsstat4 linkStatus))
                    {
                        return CreateCreateResult(linkStatus);
                    }

                    try
                    {
                        NfsCreateSymbolicLinkResponse createLinkResponse =
                            await _server.Settings.FileSystem.CreateSymbolicLinkAsync(
                                new NfsCreateSymbolicLinkRequest(
                                    refreshedDirectory.Target.SourcePath,
                                    entryName,
                                    targetPath,
                                    failIfExists: true,
                                    cancellationToken)).ConfigureAwait(false);

                        if (!createLinkResponse.CreatedNew)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_EXIST);
                        }

                        if (!createLinkResponse.PathInfo.Exists)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_SERVERFAULT);
                        }

                        if (createLinkResponse.PathInfo.Kind == NfsPathKind.Other)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
                        }

                        if (createLinkResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
                        {
                            return CreateCreateResult(nfsstat4.NFS4ERR_SERVERFAULT);
                        }

                        createdHandle = await CreateResolvedHandleAsync(
                            new NfsFileHandleTarget(
                                refreshedDirectory.Target.ExportPath,
                                createLinkResponse.PathInfo.Path),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (
                        exception is UnauthorizedAccessException
                        || exception is IOException
                        || exception is NotSupportedException
                        || exception is DirectoryNotFoundException
                        || exception is FileNotFoundException
                        || exception is PathTooLongException
                        || exception is ArgumentException)
                    {
                        return CreateCreateResult(Nfs40MutationSupport.MapCreateException(exception));
                    }

                    break;

                case nfs_ftype4.NF4REG:
                case nfs_ftype4.NF4ATTRDIR:
                case nfs_ftype4.NF4NAMEDATTR:
                    return CreateCreateResult(nfsstat4.NFS4ERR_BADTYPE);

                default:
                    return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (createdHandle is null)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_SERVERFAULT);
            }

            NfsPathInfo afterDirectoryPathInfo =
                await GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(createdHandle);
            return CreateCreateResult(
                nfsstat4.NFS4_OK,
                new CREATE4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                    attrset = Nfs40MutationSupport.CreateEmptyAttributeSet(),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleGetAttributesAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            GETATTR4args? arguments = operation.opgetattr;
            if (arguments is null)
            {
                return CreateGetAttrResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateGetAttrResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateGetAttrResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateGetAttrResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            (fattr4? attributes, nfsstat4 errorStatus) =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    refreshedHandle,
                    arguments.attr_request,
                    cancellationToken).ConfigureAwait(false);
            if (attributes is null)
            {
                return CreateGetAttrResult(errorStatus);
            }

            return CreateGetAttrResult(nfsstat4.NFS4_OK, attributes);
        }

        private Nfs40CompoundOperationResult HandleGetFileHandle(Nfs40CompoundState state)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateGetFileHandleResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            return CreateGetFileHandleResult(nfsstat4.NFS4_OK, currentHandle);
        }

        private async Task<Nfs40CompoundOperationResult> HandleSecurityInfoAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            SECINFO4args? arguments = operation.opsecinfo;
            if (arguments is null)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateSecurityInfoResult(refreshStatus);
            }

            switch (refreshedHandle.PathInfo.Kind)
            {
                case NfsPathKind.SymbolicLink:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_SYMLINK);
                case NfsPathKind.Other:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTSUPP);
                case not NfsPathKind.Directory:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(
                arguments.name,
                out string entryName,
                out nfsstat4 entryNameStatus))
            {
                return CreateSecurityInfoResult(entryNameStatus);
            }

            NfsPathInfo childPathInfo;
            try
            {
                childPathInfo =
                    await LookupChildPathInfoAsync(
                        refreshedHandle.Target,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }
            catch (DirectoryNotFoundException)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (!childPathInfo.Exists)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            return CreateSecurityInfoResult(
                nfsstat4.NFS4_OK,
                new SECINFO4resok
                {
                    Value = CreateSupportedSecurityInfos(),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleSetAttributesAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            SETATTR4args? arguments = operation.opsetattr;
            if (arguments?.stateid is null || arguments.obj_attributes is null)
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateSetAttrResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (!Nfs40AttributeEncoder.TryReadSettableAttributes(
                arguments.obj_attributes,
                includeIdentityAttributes: _server.Capabilities.IdMapper is not null,
                includeAclAttributes: _server.Capabilities.Acls is not null,
                out Nfs40SetAttributeUpdate? update,
                out nfsstat4 decodeStatus))
            {
                return CreateSetAttrResult(decodeStatus);
            }

            List<int> updatedAttributeIds = new List<int>();
            try
            {
                if (update!.HasAclUpdate)
                {
                    await _server.Capabilities.Acls!.SetAclAsync(
                        new NfsSetAclRequest(
                            refreshedHandle.Target.SourcePath,
                            refreshedHandle.PathInfo.Kind,
                            update.AclEntries,
                            cancellationToken)).ConfigureAwait(false);
                    updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_ACL);
                }

                if (update.HasIdentityUpdate)
                {
                    NfsSetIdentityResponse identityResponse =
                        await _server.Capabilities.IdMapper!.SetIdentityAsync(
                            new NfsSetIdentityRequest(
                                refreshedHandle.Target.SourcePath,
                                refreshedHandle.PathInfo.Kind,
                                update.Owner,
                                update.OwnerGroup,
                                cancellationToken)).ConfigureAwait(false);

                    if (update.Owner is not null)
                    {
                        updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER);
                    }

                    if (update.OwnerGroup is not null)
                    {
                        updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER_GROUP);
                    }

                    if (string.IsNullOrWhiteSpace(identityResponse.Owner) || string.IsNullOrWhiteSpace(identityResponse.OwnerGroup))
                    {
                        return CreateSetAttrResult(nfsstat4.NFS4ERR_SERVERFAULT);
                    }
                }
            }
            catch (Exception exception)
            {
                return CreateSetAttrResult(
                    update!.HasIdentityUpdate && !update.HasAclUpdate
                        ? Nfs40MutationSupport.MapIdentityException(exception)
                        : update.HasAclUpdate && !update.HasIdentityUpdate
                            ? Nfs40MutationSupport.MapAclException(exception)
                            : Nfs40MutationSupport.MapCreateException(exception));
            }

            return CreateSetAttrResult(
                nfsstat4.NFS4_OK,
                Nfs40AttributeEncoder.CreateBitmap(updatedAttributeIds.ToArray()));
        }

        private async Task<Nfs40CompoundOperationResult> HandleLinkAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LINK4args? arguments = operation.oplink;
            if (arguments is null)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetSavedHandle(out Nfs40CompoundResolvedHandle? savedHandle)
                || !state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedSource, nfsstat4 sourceRefreshStatus) =
                await TryRefreshResolvedHandleAsync(savedHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedSource is null)
            {
                return CreateLinkResult(sourceRefreshStatus);
            }

            (Nfs40CompoundResolvedHandle? refreshedDirectory, nfsstat4 directoryRefreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedDirectory is null)
            {
                return CreateLinkResult(directoryRefreshStatus);
            }

            if (refreshedSource.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedSource.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateLinkResult(
                    refreshedSource.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!string.Equals(
                refreshedSource.Target.ExportPath,
                refreshedDirectory.Target.ExportPath,
                StringComparison.Ordinal))
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_XDEV);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.newname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateLinkResult(nameStatus);
            }

            NfsPathInfo existingChildPathInfo =
                await LookupChildPathInfoAsync(refreshedDirectory.Target, entryName, cancellationToken).ConfigureAwait(false);
            if (existingChildPathInfo.Exists)
            {
                return CreateLinkResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_EXIST);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;

            try
            {
                NfsCreateHardLinkResponse createResponse =
                    await _server.Settings.FileSystem.CreateHardLinkAsync(
                        new NfsCreateHardLinkRequest(
                            refreshedSource.Target.SourcePath,
                            refreshedDirectory.Target.SourcePath,
                            entryName,
                            cancellationToken)).ConfigureAwait(false);

                if (!createResponse.SourcePathInfo.Exists || !createResponse.LinkPathInfo.Exists)
                {
                    return CreateLinkResult(nfsstat4.NFS4ERR_SERVERFAULT);
                }

                if (createResponse.SourcePathInfo.Kind != NfsPathKind.File || createResponse.LinkPathInfo.Kind != NfsPathKind.File)
                {
                    return CreateLinkResult(
                        createResponse.SourcePathInfo.Kind == NfsPathKind.Other
                        || createResponse.LinkPathInfo.Kind == NfsPathKind.Other
                            ? nfsstat4.NFS4ERR_NOTSUPP
                            : nfsstat4.NFS4ERR_SERVERFAULT);
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateLinkResult(Nfs40MutationSupport.MapHardLinkException(exception));
            }

            NfsPathInfo afterDirectoryPathInfo =
                await GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedDirectory.FileHandle,
                    refreshedDirectory.Target,
                    afterDirectoryPathInfo));

            return CreateLinkResult(
                nfsstat4.NFS4_OK,
                new LINK4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleLockAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LOCK4args? arguments = operation.oplock;
            if (arguments is null)
            {
                return CreateLockResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLockResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            if (_server.Capabilities.Locking is null)
            {
                return CreateLockResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateLockResult(refreshStatus);
            }

            nfsstat4 handleStatus = ValidateLockableFile(refreshedHandle.PathInfo.Kind);
            if (handleStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockResult(handleStatus);
            }

            if (!TryMapLockType(arguments.locktype, out nfs_lock_type4 protocolLockType, out bool exclusive, out bool block, out nfsstat4 typeStatus))
            {
                return CreateLockResult(typeStatus);
            }

            if (arguments.offset is null || arguments.length is null || arguments.locker is null)
            {
                return CreateLockResult(nfsstat4.NFS4ERR_BADXDR);
            }

            string fileKey = BuildOpenFileKey(refreshedHandle.Target.ExportPath, refreshedHandle.Target.SourcePath);
            Nfs40StateManager.Nfs40LockPreparationResult preparation;
            if (arguments.locker.new_lock_owner)
            {
                open_to_lock_owner4? openOwner = arguments.locker.open_owner;
                if (openOwner?.lock_owner?.clientid is null || openOwner.lock_owner.owner is null)
                {
                    return CreateLockResult(nfsstat4.NFS4ERR_BADXDR);
                }

                preparation = _stateManager.PrepareLockFromOpen(
                    openOwner.open_stateid,
                    openOwner.open_seqid,
                    openOwner.lock_owner.clientid,
                    openOwner.lock_owner.owner,
                    openOwner.lock_seqid,
                    fileKey,
                    arguments.reclaim);
            }
            else
            {
                exist_lock_owner4? existingOwner = arguments.locker.lock_owner;
                if (existingOwner is null)
                {
                    return CreateLockResult(nfsstat4.NFS4ERR_BADXDR);
                }

                preparation = _stateManager.PrepareLock(
                    existingOwner.lock_stateid,
                    existingOwner.lock_seqid,
                    fileKey,
                    arguments.reclaim);
            }

            if (preparation.Status != nfsstat4.NFS4_OK || preparation.PendingOperation is null)
            {
                return CreateLockResult(preparation.Status);
            }

            preparation.PendingOperation.Target = refreshedHandle.Target;
            preparation.PendingOperation.Offset = arguments.offset.Value;
            preparation.PendingOperation.Length = arguments.length.Value;
            preparation.PendingOperation.Exclusive = exclusive;

            NfsLockRequest request = CreateHostLockRequest(
                NfsLockOperation.Lock,
                refreshedHandle.Target,
                preparation.PendingOperation.ClientId,
                preparation.PendingOperation.OwnerBytes,
                arguments.offset.Value,
                arguments.length.Value,
                exclusive,
                block,
                arguments.reclaim,
                cancellationToken);
            NfsLockResponse response =
                await _server.Capabilities.Locking.ProcessLockAsync(request).ConfigureAwait(false);
            if (response.Disposition == NfsLockDisposition.Granted)
            {
                Nfs40StateManager.Nfs40LockTransitionResult transition =
                    _stateManager.CommitLockGranted(preparation.PendingOperation);
                return CreateLockResult(
                    transition.Status,
                    transition.Status == nfsstat4.NFS4_OK
                        ? new LOCK4resok
                        {
                            lock_stateid = transition.StateId,
                        }
                        : null);
            }

            return CreateLockResult(
                MapLockDisposition(response.Disposition),
                deniedPayload: response.Disposition == NfsLockDisposition.Denied
                    ? CreateDeniedLock(response.Conflict, protocolLockType)
                    : null);
        }

        private async Task<Nfs40CompoundOperationResult> HandleLockTestAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LOCKT4args? arguments = operation.oplockt;
            if (arguments is null)
            {
                return CreateLockTestResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLockTestResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            if (_server.Capabilities.Locking is null)
            {
                return CreateLockTestResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateLockTestResult(refreshStatus);
            }

            nfsstat4 handleStatus = ValidateLockableFile(refreshedHandle.PathInfo.Kind);
            if (handleStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockTestResult(handleStatus);
            }

            if (!TryMapLockType(arguments.locktype, out nfs_lock_type4 protocolLockType, out bool exclusive, out _, out nfsstat4 typeStatus))
            {
                return CreateLockTestResult(typeStatus);
            }

            if (arguments.offset is null || arguments.length is null || arguments.owner?.clientid is null || arguments.owner.owner is null)
            {
                return CreateLockTestResult(nfsstat4.NFS4ERR_BADXDR);
            }

            nfsstat4 validationStatus = _stateManager.ValidateLockTest(arguments.owner.clientid, arguments.owner.owner);
            if (validationStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockTestResult(validationStatus);
            }

            NfsLockRequest request = CreateHostLockRequest(
                NfsLockOperation.Test,
                refreshedHandle.Target,
                arguments.owner.clientid.Value,
                arguments.owner.owner,
                arguments.offset.Value,
                arguments.length.Value,
                exclusive,
                block: false,
                reclaim: false,
                cancellationToken);
            NfsLockResponse response =
                await _server.Capabilities.Locking.ProcessLockAsync(request).ConfigureAwait(false);
            return CreateLockTestResult(
                MapLockDisposition(response.Disposition),
                deniedPayload: response.Disposition == NfsLockDisposition.Denied
                    ? CreateDeniedLock(response.Conflict, protocolLockType)
                    : null);
        }

        private async Task<Nfs40CompoundOperationResult> HandleLockUnlockAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LOCKU4args? arguments = operation.oplocku;
            if (arguments is null)
            {
                return CreateLockUnlockResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLockUnlockResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            if (_server.Capabilities.Locking is null)
            {
                return CreateLockUnlockResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateLockUnlockResult(refreshStatus);
            }

            nfsstat4 handleStatus = ValidateLockableFile(refreshedHandle.PathInfo.Kind);
            if (handleStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockUnlockResult(handleStatus);
            }

            if (!TryMapLockType(arguments.locktype, out _, out bool exclusive, out _, out nfsstat4 typeStatus))
            {
                return CreateLockUnlockResult(typeStatus);
            }

            if (arguments.offset is null || arguments.length is null)
            {
                return CreateLockUnlockResult(nfsstat4.NFS4ERR_BADXDR);
            }

            string fileKey = BuildOpenFileKey(refreshedHandle.Target.ExportPath, refreshedHandle.Target.SourcePath);
            Nfs40StateManager.Nfs40LockPreparationResult preparation =
                _stateManager.PrepareUnlock(arguments.lock_stateid, arguments.seqid, fileKey);
            if (preparation.Status != nfsstat4.NFS4_OK || preparation.PendingOperation is null)
            {
                return CreateLockUnlockResult(preparation.Status);
            }

            NfsLockRequest request = CreateHostLockRequest(
                NfsLockOperation.Unlock,
                refreshedHandle.Target,
                preparation.PendingOperation.ClientId,
                preparation.PendingOperation.OwnerBytes,
                arguments.offset.Value,
                arguments.length.Value,
                exclusive,
                block: false,
                reclaim: false,
                cancellationToken);
            NfsLockResponse response =
                await _server.Capabilities.Locking.ProcessLockAsync(request).ConfigureAwait(false);
            if (response.Disposition != NfsLockDisposition.Granted)
            {
                return CreateLockUnlockResult(MapLockDisposition(response.Disposition));
            }

            Nfs40StateManager.Nfs40LockTransitionResult transition =
                _stateManager.CommitUnlock(preparation.PendingOperation);
            return CreateLockUnlockResult(transition.Status, transition.StateId);
        }

        private async Task<Nfs40CompoundOperationResult> HandleLookupAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LOOKUP4args? arguments = operation.oplookup;
            if (arguments is null)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateLookupResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLookupResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.SymbolicLink
                        ? nfsstat4.NFS4ERR_SYMLINK
                        : refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                            ? nfsstat4.NFS4ERR_NOTSUPP
                            : nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.objname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateLookupResult(nameStatus);
            }

            NfsLookupPathResponse lookupResponse =
                await _server.Settings.FileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(
                        refreshedHandle.Target.SourcePath,
                        entryName,
                        cancellationToken)).ConfigureAwait(false);

            NfsPathInfo childPathInfo = lookupResponse.PathInfo;
            if (!childPathInfo.Exists)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            NfsFileHandleTarget childTarget = new NfsFileHandleTarget(
                refreshedHandle.Target.ExportPath,
                childPathInfo.Path);
            NfsFileHandle childFileHandle =
                await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(new Nfs40CompoundResolvedHandle(childFileHandle, childTarget, childPathInfo));
            return CreateLookupResult(nfsstat4.NFS4_OK);
        }

        private async Task<Nfs40CompoundOperationResult> HandleLookupParentAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateLookupParentResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            (Nfs40CompoundResolvedHandle? parentHandle, nfsstat4 parentStatus) =
                await TryResolveParentHandleAsync(refreshedHandle, cancellationToken).ConfigureAwait(false);
            if (parentHandle is null)
            {
                return CreateLookupParentResult(parentStatus);
            }

            state.SetCurrentHandle(parentHandle);
            return CreateLookupParentResult(nfsstat4.NFS4_OK);
        }

        private Nfs40CompoundOperationResult HandleClose(nfs_argop4 operation)
        {
            CLOSE4args? arguments = operation.opclose;
            if (arguments is null)
            {
                return CreateCloseResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40StateManager.Nfs40OpenStateTransitionResult transition =
                _stateManager.CloseOpen(arguments.open_stateid, arguments.seqid);
            return CreateCloseResult(transition.Status, transition.StateId);
        }

        private async Task<Nfs40CompoundOperationResult> HandleCommitAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            COMMIT4args? arguments = operation.opcommit;
            if (arguments?.offset is null || arguments.count is null)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateCommitResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateCommitResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsCommitFileResponse commitResponse;
            try
            {
                commitResponse =
                    await _server.Settings.FileSystem.CommitFileAsync(
                        new NfsCommitFileRequest(
                            refreshedHandle.Target.SourcePath,
                            arguments.offset.Value,
                            arguments.count.Value,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException
                || exception is OverflowException)
            {
                return CreateCommitResult(Nfs40MutationSupport.MapCommitException(exception));
            }

            if (!commitResponse.PathInfo.Exists)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_STALE);
            }

            if (commitResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (commitResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateCommitResult(
                    commitResponse.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            return CreateCommitResult(
                nfsstat4.NFS4_OK,
                new COMMIT4resok
                {
                    writeverf = CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleOpenAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            OPEN4args? arguments = operation.opopen;
            if (arguments is null)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateOpenResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            open_claim_type4? claimType = arguments.claim?.claim;
            if (!claimType.HasValue)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            string entryName;
            switch (claimType.Value)
            {
                case open_claim_type4.CLAIM_NULL:
                    if (!Nfs40MutationSupport.TryReadComponent(arguments.claim?.file, out entryName, out nfsstat4 claimStatus))
                    {
                        return CreateOpenResult(claimStatus);
                    }

                    break;

                case open_claim_type4.CLAIM_PREVIOUS:
                    entryName = string.Empty;
                    break;

                case open_claim_type4.CLAIM_DELEGATE_CUR:
                case open_claim_type4.CLAIM_DELEGATE_PREV:
                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);

                default:
                    return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            opentype4? openType = arguments.openhow?.opentype;
            if (!openType.HasValue)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40StateManager.Nfs40OpenStateTransitionResult transition;
            NfsPathInfo beforeChangePathInfo = refreshedHandle.PathInfo;
            NfsPathInfo afterChangePathInfo = beforeChangePathInfo;
            Nfs40CompoundResolvedHandle? openedHandle = null;
            string fileKey;

            switch (claimType.Value)
            {
                case open_claim_type4.CLAIM_NULL:
                {
                    if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_NOTDIR);
                    }

                    string targetSourcePath = OpenNFS.Server.Internal.NfsSourcePath.Combine(refreshedHandle.Target.SourcePath, entryName);
                    fileKey = BuildOpenFileKey(refreshedHandle.Target.ExportPath, targetSourcePath);
                    nfsstat4 validationStatus = _stateManager.ValidateOpen(
                        arguments.owner?.clientid,
                        arguments.owner?.owner ?? Array.Empty<byte>(),
                        arguments.seqid,
                        fileKey,
                        arguments.share_access,
                        arguments.share_deny);
                    if (validationStatus != nfsstat4.NFS4_OK)
                    {
                        if (validationStatus == nfsstat4.NFS4ERR_DELAY
                            && _server.Capabilities.Delegations is not null)
                        {
                            IReadOnlyList<Nfs40StateManager.Nfs40DelegationRecallInfo> recallRequests =
                                _stateManager.GetConflictingDelegationRecalls(
                                    fileKey,
                                    arguments.owner?.clientid?.Value ?? 0UL,
                                    arguments.share_access,
                                    arguments.share_deny);
                            if (recallRequests.Count == 0)
                            {
                                recallRequests = _stateManager.GetDelegationsForFile(
                                    fileKey,
                                    arguments.owner?.clientid?.Value ?? 0UL);
                            }

                            await NotifyDelegationRecallsAsync(
                                recallRequests,
                                refreshedHandle.Target.ExportPath,
                                targetSourcePath,
                                cancellationToken).ConfigureAwait(false);
                        }

                        return CreateOpenResult(validationStatus);
                    }

                    switch (openType.Value)
                    {
                        case opentype4.OPEN4_NOCREATE:
                        {
                            NfsPathInfo childPathInfo =
                                await LookupChildPathInfoAsync(refreshedHandle.Target, entryName, cancellationToken).ConfigureAwait(false);
                            if (!childPathInfo.Exists)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_NOENT);
                            }

                            if (childPathInfo.Kind == NfsPathKind.Other)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                            }

                            if (childPathInfo.Kind == NfsPathKind.Directory)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_ISDIR);
                            }

                            if (childPathInfo.Kind == NfsPathKind.SymbolicLink)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_SYMLINK);
                            }

                            openedHandle = await CreateResolvedHandleAsync(
                                new NfsFileHandleTarget(refreshedHandle.Target.ExportPath, childPathInfo.Path),
                                cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        case opentype4.OPEN4_CREATE:
                        {
                            createhow4? createHow = arguments.openhow?.how;
                            createmode4? createMode = createHow?.mode;
                            if (!createMode.HasValue)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
                            }

                            if (createMode.Value == createmode4.EXCLUSIVE4)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                            }

                            if (createHow?.createattrs is null)
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
                            }

                            if (HasRequestedAttributes(createHow.createattrs))
                            {
                                return CreateOpenResult(nfsstat4.NFS4ERR_ATTRNOTSUPP);
                            }

                            NfsPathInfo childPathInfo =
                                await LookupChildPathInfoAsync(refreshedHandle.Target, entryName, cancellationToken).ConfigureAwait(false);
                            if (childPathInfo.Exists)
                            {
                                if (childPathInfo.Kind == NfsPathKind.Other)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                                }

                                if (createMode.Value == createmode4.GUARDED4)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_EXIST);
                                }

                                if (childPathInfo.Kind == NfsPathKind.Directory)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_ISDIR);
                                }

                                if (childPathInfo.Kind == NfsPathKind.SymbolicLink)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_SYMLINK);
                                }

                                openedHandle = await CreateResolvedHandleAsync(
                                    new NfsFileHandleTarget(refreshedHandle.Target.ExportPath, childPathInfo.Path),
                                    cancellationToken).ConfigureAwait(false);
                                break;
                            }

                            try
                            {
                                NfsCreatePathResponse createResponse =
                                    await _server.Settings.FileSystem.CreatePathAsync(
                                        new NfsCreatePathRequest(
                                            refreshedHandle.Target.SourcePath,
                                            entryName,
                                            NfsPathKind.File,
                                            failIfExists: true,
                                            cancellationToken)).ConfigureAwait(false);

                                if (!createResponse.CreatedNew)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_EXIST);
                                }

                                if (!createResponse.PathInfo.Exists)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_SERVERFAULT);
                                }

                                if (createResponse.PathInfo.Kind == NfsPathKind.Other)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                                }

                                if (createResponse.PathInfo.Kind != NfsPathKind.File)
                                {
                                    return CreateOpenResult(nfsstat4.NFS4ERR_SERVERFAULT);
                                }

                                openedHandle = await CreateResolvedHandleAsync(
                                    new NfsFileHandleTarget(
                                        refreshedHandle.Target.ExportPath,
                                        createResponse.PathInfo.Path),
                                    cancellationToken).ConfigureAwait(false);
                                afterChangePathInfo =
                                    await GetPathInfoAsync(refreshedHandle.Target.SourcePath, cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception exception) when (
                                exception is UnauthorizedAccessException
                                || exception is IOException
                                || exception is NotSupportedException
                                || exception is DirectoryNotFoundException
                                || exception is FileNotFoundException
                                || exception is PathTooLongException
                                || exception is ArgumentException)
                            {
                                return CreateOpenResult(Nfs40MutationSupport.MapCreateException(exception));
                            }

                            break;
                        }

                        default:
                            return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
                    }

                    if (openedHandle is null)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_SERVERFAULT);
                    }

                    transition = _stateManager.Open(
                        arguments.owner?.clientid,
                        arguments.owner?.owner ?? Array.Empty<byte>(),
                        arguments.seqid,
                        fileKey,
                        arguments.share_access,
                        arguments.share_deny);
                    if (transition.Status != nfsstat4.NFS4_OK)
                    {
                        if (transition.Status == nfsstat4.NFS4ERR_DELAY
                            && openedHandle is not null
                            && _server.Capabilities.Delegations is not null)
                        {
                            IReadOnlyList<Nfs40StateManager.Nfs40DelegationRecallInfo> recallRequests =
                                transition.RecallRequests.Count > 0
                                    ? transition.RecallRequests
                                    : _stateManager.GetConflictingDelegationRecalls(
                                        fileKey,
                                        arguments.owner?.clientid?.Value ?? 0UL,
                                        arguments.share_access,
                                        arguments.share_deny);
                            if (recallRequests.Count == 0)
                            {
                                recallRequests = _stateManager.GetDelegationsForFile(
                                    fileKey,
                                    arguments.owner?.clientid?.Value ?? 0UL);
                            }

                            await NotifyDelegationRecallsAsync(
                                recallRequests,
                                openedHandle.Target.ExportPath,
                                openedHandle.Target.SourcePath,
                                cancellationToken).ConfigureAwait(false);
                        }

                        return CreateOpenResult(transition.Status);
                    }

                    break;
                }

                case open_claim_type4.CLAIM_PREVIOUS:
                {
                    if (arguments.claim?.delegate_type is not open_delegation_type4.OPEN_DELEGATE_NONE)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                    }

                    if (openType.Value != opentype4.OPEN4_NOCREATE)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_INVAL);
                    }

                    if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_ISDIR);
                    }

                    if (refreshedHandle.PathInfo.Kind == NfsPathKind.SymbolicLink)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_SYMLINK);
                    }

                    if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                    }

                    fileKey = BuildOpenFileKey(refreshedHandle.Target.ExportPath, refreshedHandle.Target.SourcePath);
                    openedHandle = refreshedHandle;
                    transition = _stateManager.ReclaimOpen(
                        arguments.owner?.clientid,
                        arguments.owner?.owner ?? Array.Empty<byte>(),
                        arguments.seqid,
                        fileKey,
                        arguments.share_access,
                        arguments.share_deny);
                    if (transition.Status != nfsstat4.NFS4_OK)
                    {
                        return CreateOpenResult(transition.Status);
                    }

                    break;
                }

                case open_claim_type4.CLAIM_DELEGATE_CUR:
                case open_claim_type4.CLAIM_DELEGATE_PREV:
                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);

                default:
                    return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            open_delegation4 delegation = CreateNoDelegation();
            if (claimType.Value != open_claim_type4.CLAIM_PREVIOUS
                && _server.Capabilities.Delegations is not null
                && openedHandle is not null)
            {
                NfsAcquireDelegationResponse delegationDecision =
                    await _server.Capabilities.Delegations.AcquireDelegationAsync(
                        new NfsAcquireDelegationRequest(
                            openedHandle.Target.ExportPath,
                            openedHandle.Target.SourcePath,
                            openedHandle.PathInfo.Kind,
                            arguments.owner?.clientid?.Value ?? 0UL,
                            arguments.share_access,
                            arguments.share_deny,
                            cancellationToken)).ConfigureAwait(false);

                if (delegationDecision.DelegationKind != NfsDelegationKind.None)
                {
                    Nfs40StateManager.Nfs40DelegationTransitionResult delegationTransition =
                        _stateManager.TryGrantDelegation(transition.StateId, delegationDecision.DelegationKind);
                    if (delegationTransition.Status != nfsstat4.NFS4_OK)
                    {
                        return CreateOpenResult(delegationTransition.Status);
                    }

                    if (delegationTransition.DelegationState is not null)
                    {
                        delegation = CreateDelegation(delegationTransition.DelegationState);
                    }
                }
            }

            state.SetCurrentHandle(openedHandle!);
            return CreateOpenResult(
                nfsstat4.NFS4_OK,
                new OPEN4resok
                {
                    stateid = transition.StateId,
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeChangePathInfo, afterChangePathInfo),
                    rflags = transition.RequiresConfirmation
                        ? (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM
                        : 0U,
                    attrset = Nfs40MutationSupport.CreateEmptyAttributeSet(),
                    delegation = delegation,
                });
        }

        private Nfs40CompoundOperationResult HandleOpenConfirm(nfs_argop4 operation)
        {
            OPEN_CONFIRM4args? arguments = operation.opopen_confirm;
            if (arguments is null)
            {
                return CreateOpenConfirmResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40StateManager.Nfs40OpenStateTransitionResult transition =
                _stateManager.ConfirmOpen(arguments.open_stateid, arguments.seqid);
            return CreateOpenConfirmResult(
                transition.Status,
                transition.Status == nfsstat4.NFS4_OK
                    ? new OPEN_CONFIRM4resok
                    {
                        open_stateid = transition.StateId,
                    }
                    : null);
        }

        private Nfs40CompoundOperationResult HandleOpenDowngrade(nfs_argop4 operation)
        {
            OPEN_DOWNGRADE4args? arguments = operation.opopen_downgrade;
            if (arguments is null)
            {
                return CreateOpenDowngradeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40StateManager.Nfs40OpenStateTransitionResult transition =
                _stateManager.DowngradeOpen(
                    arguments.open_stateid,
                    arguments.seqid,
                    arguments.share_access,
                    arguments.share_deny);
            return CreateOpenDowngradeResult(
                transition.Status,
                transition.Status == nfsstat4.NFS4_OK
                    ? new OPEN_DOWNGRADE4resok
                    {
                        open_stateid = transition.StateId,
                    }
                    : null);
        }

        private Nfs40CompoundOperationResult HandleRenew(nfs_argop4 operation)
        {
            RENEW4args? arguments = operation.oprenew;
            if (arguments is null)
            {
                return CreateRenewResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateRenewResult(_stateManager.RenewClient(arguments.clientid));
        }

        private async Task<Nfs40CompoundOperationResult> HandleDelegationReturnAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            DELEGRETURN4args? arguments = operation.opdelegreturn;
            if (arguments?.deleg_stateid is null)
            {
                return CreateDelegationReturnResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateDelegationReturnResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateDelegationReturnResult(refreshStatus);
            }

            string fileKey = BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            Nfs40StateManager.Nfs40DelegationTransitionResult transition =
                _stateManager.ReturnDelegation(arguments.deleg_stateid, fileKey);
            if (transition.Status != nfsstat4.NFS4_OK)
            {
                return CreateDelegationReturnResult(transition.Status);
            }

            if (_server.Capabilities.Delegations is not null && transition.DelegationState is not null)
            {
                await _server.Capabilities.Delegations.ReturnDelegationAsync(
                    new NfsReturnDelegationRequest(
                        refreshedHandle.Target.ExportPath,
                        refreshedHandle.Target.SourcePath,
                        transition.DelegationState.ClientId,
                        transition.DelegationState.DelegationKind,
                        transition.DelegationState.StateId.other ?? Array.Empty<byte>(),
                        cancellationToken)).ConfigureAwait(false);
            }

            return CreateDelegationReturnResult(nfsstat4.NFS4_OK);
        }

        private Nfs40CompoundOperationResult HandleSetClientId(nfs_argop4 operation)
        {
            SETCLIENTID4args? arguments = operation.opsetclientid;
            if (arguments is null)
            {
                return CreateSetClientIdResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40StateManager.Nfs40ClientRegistrationResult registration =
                _stateManager.RegisterClient(arguments.client, arguments.callback, arguments.callback_ident);
            return CreateSetClientIdResult(
                registration.Status,
                registration.Status == nfsstat4.NFS4_OK
                    ? new SETCLIENTID4resok
                    {
                        clientid = new clientid4
                        {
                            Value = registration.ClientId,
                        },
                        setclientid_confirm = new verifier4
                        {
                            Value = registration.ConfirmationVerifier,
                        },
                    }
                    : null);
        }

        private Nfs40CompoundOperationResult HandleSetClientIdConfirm(nfs_argop4 operation)
        {
            SETCLIENTID_CONFIRM4args? arguments = operation.opsetclientid_confirm;
            if (arguments is null)
            {
                return CreateSetClientIdConfirmResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateSetClientIdConfirmResult(
                _stateManager.ConfirmClient(arguments.clientid, arguments.setclientid_confirm));
        }

        private async Task<Nfs40CompoundOperationResult> HandlePutFileHandleAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            PUTFH4args? arguments = operation.opputfh;
            if (arguments?.@object?.Value is null || arguments.@object.Value.Length == 0)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }

            if (arguments.@object.Value.Length > (int)Nfs40Constants.NFS4_FHSIZE)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }

            try
            {
                Nfs40CompoundResolvedHandle resolvedHandle =
                    await ResolveExistingFileHandleAsync(arguments.@object.Value, cancellationToken).ConfigureAwait(false);
                state.SetCurrentHandle(resolvedHandle);
                return CreatePutFileHandleResult(nfsstat4.NFS4_OK);
            }
            catch (FileNotFoundException)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_STALE);
            }
            catch (InvalidDataException)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }
        }

        private async Task<Nfs40CompoundOperationResult> HandlePutPublicFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await HandleNamespaceRootFileHandleAsync(
                state,
                CreatePutPublicFileHandleResult,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<Nfs40CompoundOperationResult> HandlePutRootFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await HandleNamespaceRootFileHandleAsync(
                state,
                CreatePutRootFileHandleResult,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<Nfs40CompoundOperationResult> HandleNamespaceRootFileHandleAsync(
            Nfs40CompoundState state,
            Func<nfsstat4, Nfs40CompoundOperationResult> createResult,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsExportDefinition> exports =
                await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
            if (exports.Count == 0)
            {
                return createResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            OpenNfsExportDefinition rootExport = SelectRootExport(exports);
            Nfs40CompoundResolvedHandle resolvedHandle =
                await CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(rootExport.ExportPath, rootExport.SourcePath),
                    cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);
            return createResult(nfsstat4.NFS4_OK);
        }

        private async Task<Nfs40CompoundOperationResult> HandleWriteAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            WRITE4args? arguments = operation.opwrite;
            if (arguments?.stateid?.other is null || arguments.offset is null || arguments.stable is null || arguments.data is null)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateWriteResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateWriteResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            string fileKey = BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            nfsstat4 stateStatus = _stateManager.ValidateWriteState(arguments.stateid, fileKey);
            if (stateStatus != nfsstat4.NFS4_OK)
            {
                return CreateWriteResult(stateStatus);
            }

            if (!TryMapWriteStability(arguments.stable, out NfsWriteStability requestedStability))
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_INVAL);
            }

            NfsWriteFileResponse writeResponse;
            try
            {
                writeResponse =
                    await _server.Settings.FileSystem.WriteFileAsync(
                        new NfsWriteFileRequest(
                            refreshedHandle.Target.SourcePath,
                            arguments.offset.Value,
                            arguments.data,
                            requestedStability,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException
                || exception is OverflowException)
            {
                return CreateWriteResult(Nfs40MutationSupport.MapWriteException(exception));
            }

            if (!writeResponse.PathInfo.Exists)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_STALE);
            }

            if (writeResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (writeResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateWriteResult(
                    writeResponse.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            return CreateWriteResult(
                nfsstat4.NFS4_OK,
                new WRITE4resok
                {
                    count = new count4
                    {
                        Value = writeResponse.BytesWritten,
                    },
                    committed = MapWriteStability(writeResponse.CommittedStability),
                    writeverf = CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleReadAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            READ4args? arguments = operation.opread;
            if (arguments?.count is null || arguments.offset is null || arguments.stateid?.other is null)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateReadResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateReadResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsReadFileResponse readResponse =
                await _server.Settings.FileSystem.ReadFileAsync(
                    new NfsReadFileRequest(
                        refreshedHandle.Target.SourcePath,
                        arguments.offset.Value,
                        arguments.count.Value,
                        cancellationToken)).ConfigureAwait(false);

            if (!readResponse.Found)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_STALE);
            }

            byte[] data = readResponse.Data.ToArray();
            return CreateReadResult(
                nfsstat4.NFS4_OK,
                new READ4resok
                {
                    eof = readResponse.EndOfFile,
                    data = data,
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleReadDirectoryAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            READDIR4args? arguments = operation.opreaddir;
            if (arguments?.cookieverf?.Value is null || arguments.maxcount is null)
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateReadDirectoryResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateReadDirectoryResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_NOTDIR);
            }

            NfsReadDirectoryResponse directoryResponse =
                await _server.Settings.FileSystem.ReadDirectoryAsync(
                    new NfsReadDirectoryRequest(
                        refreshedHandle.Target.SourcePath,
                        cancellationToken)).ConfigureAwait(false);

            IReadOnlyList<NfsDirectoryEntryInfo> directoryEntries = directoryResponse.Entries;
            byte[] currentVerifier = Nfs40DirectoryListingSupport.CreateCookieVerifier(
                refreshedHandle.Target.SourcePath,
                directoryEntries);
            ulong requestedCookie = Nfs40DirectoryListingSupport.ReadCookie(arguments.cookie);
            byte[] suppliedVerifier = Nfs40DirectoryListingSupport.ReadCookieVerifier(arguments.cookieverf);

            if (!Nfs40DirectoryListingSupport.IsCookieValid(
                requestedCookie,
                suppliedVerifier,
                currentVerifier,
                directoryEntries.Count))
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_BAD_COOKIE);
            }

            uint maximumCount = arguments.maxcount.Value;
            READDIR4res emptyResult = CreateReadDirectorySuccessResult(
                currentVerifier,
                new List<entry4>(),
                eof: requestedCookie >= (ulong)directoryEntries.Count);

            if (maximumCount < Nfs40DirectoryListingSupport.MeasureReadDirectoryLength(emptyResult))
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_TOOSMALL);
            }

            List<entry4> selectedEntries = new List<entry4>();
            bool eof = true;
            bool appendedEntry = false;
            int startIndex = checked((int)requestedCookie);

            for (int index = startIndex; index < directoryEntries.Count; index++)
            {
                entry4 candidateEntry =
                    await CreateDirectoryEntryAsync(
                        refreshedHandle.Target.ExportPath,
                        arguments.attr_request,
                        directoryEntries[index],
                        (ulong)(index + 1),
                        cancellationToken).ConfigureAwait(false);
                selectedEntries.Add(candidateEntry);

                READDIR4res candidateResult = CreateReadDirectorySuccessResult(
                    currentVerifier,
                    selectedEntries,
                    eof: index + 1 >= directoryEntries.Count);

                if (Nfs40DirectoryListingSupport.MeasureReadDirectoryLength(candidateResult) > maximumCount)
                {
                    selectedEntries.RemoveAt(selectedEntries.Count - 1);
                    eof = false;

                    if (!appendedEntry)
                    {
                        return CreateReadDirectoryResult(nfsstat4.NFS4ERR_TOOSMALL);
                    }

                    break;
                }

                appendedEntry = true;
            }

            if (requestedCookie >= (ulong)directoryEntries.Count)
            {
                eof = true;
            }

            return CreateReadDirectoryResult(
                nfsstat4.NFS4_OK,
                new READDIR4resok
                {
                    cookieverf = new verifier4
                    {
                        Value = currentVerifier,
                    },
                    reply = new dirlist4
                    {
                        entries = Nfs40DirectoryListingSupport.BuildEntryList(selectedEntries),
                        eof = eof,
                    },
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleReadLinkAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateReadLinkResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateReadLinkResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsReadSymbolicLinkResponse readResponse;
            try
            {
                readResponse =
                    await _server.Settings.FileSystem.ReadSymbolicLinkAsync(
                        new NfsReadSymbolicLinkRequest(
                            refreshedHandle.Target.SourcePath,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_ACCESS);
            }
            catch (IOException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_IO);
            }
            catch (NotSupportedException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_NOTSUPP);
            }
            catch (ArgumentException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_INVAL);
            }

            if (!readResponse.PathInfo.Exists)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_STALE);
            }

            if (readResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_INVAL);
            }

            return CreateReadLinkResult(
                nfsstat4.NFS4_OK,
                new READLINK4resok
                {
                    link = new linktext4
                    {
                        Value = Encoding.UTF8.GetBytes(readResponse.TargetPath),
                    },
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleRemoveAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            REMOVE4args? arguments = operation.opremove;
            if (arguments is null)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedDirectory, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedDirectory is null)
            {
                return CreateRemoveResult(refreshStatus);
            }

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.target, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateRemoveResult(nameStatus);
            }

            NfsPathInfo childPathInfo =
                await LookupChildPathInfoAsync(refreshedDirectory.Target, entryName, cancellationToken).ConfigureAwait(false);
            if (!childPathInfo.Exists)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;

            try
            {
                await _server.Settings.FileSystem.DeletePathAsync(
                    new NfsDeletePathRequest(
                        refreshedDirectory.Target.SourcePath,
                        entryName,
                        childPathInfo.Kind,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateRemoveResult(Nfs40MutationSupport.MapDeleteException(exception));
            }

            NfsPathInfo afterDirectoryPathInfo =
                await GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedDirectory.FileHandle,
                    refreshedDirectory.Target,
                    afterDirectoryPathInfo));

            return CreateRemoveResult(
                nfsstat4.NFS4_OK,
                new REMOVE4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                });
        }

        private async Task<Nfs40CompoundOperationResult> HandleRenameAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            RENAME4args? arguments = operation.oprename;
            if (arguments is null)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetSavedHandle(out Nfs40CompoundResolvedHandle? savedHandle)
                || !state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedSourceDirectory, nfsstat4 sourceRefreshStatus) =
                await TryRefreshResolvedHandleAsync(savedHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedSourceDirectory is null)
            {
                return CreateRenameResult(sourceRefreshStatus);
            }

            (Nfs40CompoundResolvedHandle? refreshedTargetDirectory, nfsstat4 targetRefreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedTargetDirectory is null)
            {
                return CreateRenameResult(targetRefreshStatus);
            }

            if (refreshedSourceDirectory.PathInfo.Kind == NfsPathKind.Other
                || refreshedTargetDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedSourceDirectory.PathInfo.Kind != NfsPathKind.Directory
                || refreshedTargetDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!string.Equals(
                refreshedSourceDirectory.Target.ExportPath,
                refreshedTargetDirectory.Target.ExportPath,
                StringComparison.Ordinal))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_XDEV);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.oldname, out string oldName, out nfsstat4 oldNameStatus))
            {
                return CreateRenameResult(oldNameStatus);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.newname, out string newName, out nfsstat4 newNameStatus))
            {
                return CreateRenameResult(newNameStatus);
            }

            NfsPathInfo sourceChildPathInfo =
                await LookupChildPathInfoAsync(refreshedSourceDirectory.Target, oldName, cancellationToken).ConfigureAwait(false);
            if (!sourceChildPathInfo.Exists)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (sourceChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            string destinationSourcePath = OpenNFS.Server.Internal.NfsSourcePath.Combine(refreshedTargetDirectory.Target.SourcePath, newName);
            if (sourceChildPathInfo.Kind == NfsPathKind.Directory
                && Nfs40MutationSupport.IsDescendantPath(destinationSourcePath, sourceChildPathInfo.Path))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_INVAL);
            }

            NfsPathInfo destinationChildPathInfo =
                await LookupChildPathInfoAsync(refreshedTargetDirectory.Target, newName, cancellationToken).ConfigureAwait(false);
            if (destinationChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (Nfs40MutationSupport.IsSamePath(sourceChildPathInfo.Path, destinationSourcePath))
            {
                return CreateRenameResult(
                    nfsstat4.NFS4_OK,
                    new RENAME4resok
                    {
                        source_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                            refreshedSourceDirectory.PathInfo,
                            refreshedSourceDirectory.PathInfo),
                        target_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                            refreshedTargetDirectory.PathInfo,
                            refreshedTargetDirectory.PathInfo),
                    });
            }

            if (destinationChildPathInfo.Exists)
            {
                bool sourceIsDirectory = sourceChildPathInfo.Kind == NfsPathKind.Directory;
                bool destinationIsDirectory = destinationChildPathInfo.Kind == NfsPathKind.Directory;
                if (sourceIsDirectory != destinationIsDirectory)
                {
                    return CreateRenameResult(nfsstat4.NFS4ERR_EXIST);
                }

                if (destinationIsDirectory)
                {
                    NfsReadDirectoryResponse destinationDirectoryResponse =
                        await _server.Settings.FileSystem.ReadDirectoryAsync(
                            new NfsReadDirectoryRequest(destinationChildPathInfo.Path, cancellationToken)).ConfigureAwait(false);
                    if (destinationDirectoryResponse.Entries.Count > 0)
                    {
                        return CreateRenameResult(nfsstat4.NFS4ERR_EXIST);
                    }
                }
            }

            NfsPathInfo beforeSourceDirectoryPathInfo = refreshedSourceDirectory.PathInfo;
            NfsPathInfo beforeTargetDirectoryPathInfo = refreshedTargetDirectory.PathInfo;

            try
            {
                await _server.Settings.FileSystem.RenamePathAsync(
                    new NfsRenamePathRequest(
                        refreshedSourceDirectory.Target.SourcePath,
                        oldName,
                        refreshedTargetDirectory.Target.SourcePath,
                        newName,
                        sourceChildPathInfo.Kind,
                        replaceExistingDestination: destinationChildPathInfo.Exists,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateRenameResult(Nfs40MutationSupport.MapRenameException(exception));
            }

            NfsPathInfo afterSourceDirectoryPathInfo =
                await GetPathInfoAsync(refreshedSourceDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            NfsPathInfo afterTargetDirectoryPathInfo =
                await GetPathInfoAsync(refreshedTargetDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);

            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedTargetDirectory.FileHandle,
                    refreshedTargetDirectory.Target,
                    afterTargetDirectoryPathInfo));

            return CreateRenameResult(
                nfsstat4.NFS4_OK,
                new RENAME4resok
                {
                    source_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                        beforeSourceDirectoryPathInfo,
                        afterSourceDirectoryPathInfo),
                    target_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                        beforeTargetDirectoryPathInfo,
                        afterTargetDirectoryPathInfo),
                });
        }

        private Nfs40CompoundOperationResult HandleRestoreFileHandle(Nfs40CompoundState state)
        {
            if (!state.TryRestoreSavedHandle(out _))
            {
                return CreateRestoreFileHandleResult(nfsstat4.NFS4ERR_RESTOREFH);
            }

            return CreateRestoreFileHandleResult(nfsstat4.NFS4_OK);
        }

        private Nfs40CompoundOperationResult HandleSaveFileHandle(Nfs40CompoundState state)
        {
            if (!state.SaveCurrentHandle())
            {
                return CreateSaveFileHandleResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            return CreateSaveFileHandleResult(nfsstat4.NFS4_OK);
        }

        private static nfsstat4 ValidateLockableFile(NfsPathKind pathKind)
        {
            return pathKind switch
            {
                NfsPathKind.File => nfsstat4.NFS4_OK,
                NfsPathKind.Directory => nfsstat4.NFS4ERR_ISDIR,
                NfsPathKind.Other => nfsstat4.NFS4ERR_NOTSUPP,
                _ => nfsstat4.NFS4ERR_INVAL,
            };
        }

        private static verifier4 CreateWriteVerifier(byte[] writeVerifierBytes)
        {
            ArgumentNullException.ThrowIfNull(writeVerifierBytes);

            return new verifier4
            {
                Value = writeVerifierBytes.AsSpan().ToArray(),
            };
        }

        private static stable_how4 MapWriteStability(NfsWriteStability stability)
        {
            return stability switch
            {
                NfsWriteStability.Unstable => stable_how4.UNSTABLE4,
                NfsWriteStability.DataSync => stable_how4.DATA_SYNC4,
                _ => stable_how4.FILE_SYNC4,
            };
        }

        private static bool TryMapWriteStability(stable_how4? value, out NfsWriteStability stability)
        {
            stability = NfsWriteStability.Unstable;
            if (!value.HasValue)
            {
                return false;
            }

            switch (value.Value)
            {
                case stable_how4.UNSTABLE4:
                    stability = NfsWriteStability.Unstable;
                    return true;
                case stable_how4.DATA_SYNC4:
                    stability = NfsWriteStability.DataSync;
                    return true;
                case stable_how4.FILE_SYNC4:
                    stability = NfsWriteStability.FileSync;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryMapLockType(
            nfs_lock_type4? value,
            out nfs_lock_type4 protocolLockType,
            out bool exclusive,
            out bool block,
            out nfsstat4 status)
        {
            protocolLockType = default;
            exclusive = false;
            block = false;
            status = nfsstat4.NFS4_OK;

            if (!value.HasValue)
            {
                status = nfsstat4.NFS4ERR_BADXDR;
                return false;
            }

            protocolLockType = value.Value;
            switch (value.Value)
            {
                case nfs_lock_type4.READ_LT:
                    return true;
                case nfs_lock_type4.WRITE_LT:
                    exclusive = true;
                    return true;
                case nfs_lock_type4.READW_LT:
                    block = true;
                    return true;
                case nfs_lock_type4.WRITEW_LT:
                    exclusive = true;
                    block = true;
                    return true;
                default:
                    status = nfsstat4.NFS4ERR_BADXDR;
                    return false;
            }
        }

        private static NfsLockRequest CreateHostLockRequest(
            NfsLockOperation operation,
            NfsFileHandleTarget target,
            ulong clientId,
            byte[] ownerBytes,
            ulong offset,
            ulong length,
            bool exclusive,
            bool block,
            bool reclaim,
            CancellationToken cancellationToken)
        {
            return new NfsLockRequest(
                operation,
                target,
                new NfsLockOwner(
                    "nfs4:" + clientId.ToString(),
                    CreateHostLockOwnerHandle(clientId, ownerBytes),
                    processId: 0),
                new NfsLockRange(offset, length),
                exclusive,
                block,
                reclaim,
                state: 0,
                cancellationToken);
        }

        private static byte[] CreateHostLockOwnerHandle(ulong clientId, byte[] ownerBytes)
        {
            byte[] handle = new byte[8 + ownerBytes.Length];
            BinaryPrimitives.WriteUInt64BigEndian(handle.AsSpan(0, 8), clientId);
            ownerBytes.AsSpan().CopyTo(handle.AsSpan(8));
            return handle;
        }

        private static bool TryDecodeHostLockOwnerHandle(byte[] ownerHandle, out ulong clientId, out byte[] ownerBytes)
        {
            clientId = 0UL;
            ownerBytes = Array.Empty<byte>();

            if (ownerHandle.Length < 9)
            {
                return false;
            }

            clientId = BinaryPrimitives.ReadUInt64BigEndian(ownerHandle.AsSpan(0, 8));
            ownerBytes = ownerHandle.AsSpan(8).ToArray();
            return ownerBytes.Length > 0;
        }

        private static nfsstat4 MapLockDisposition(NfsLockDisposition disposition)
        {
            return disposition switch
            {
                NfsLockDisposition.Granted => nfsstat4.NFS4_OK,
                NfsLockDisposition.Denied => nfsstat4.NFS4ERR_DENIED,
                NfsLockDisposition.DeniedNoLocks => nfsstat4.NFS4ERR_RESOURCE,
                NfsLockDisposition.Blocked => nfsstat4.NFS4ERR_DELAY,
                NfsLockDisposition.DeniedGracePeriod => nfsstat4.NFS4ERR_GRACE,
                NfsLockDisposition.ReadOnlyFileSystem => nfsstat4.NFS4ERR_ROFS,
                NfsLockDisposition.StaleFileHandle => nfsstat4.NFS4ERR_STALE,
                NfsLockDisposition.FileTooLarge => nfsstat4.NFS4ERR_FBIG,
                _ => nfsstat4.NFS4ERR_SERVERFAULT,
            };
        }

        private static LOCK4denied? CreateDeniedLock(NfsLockConflict? conflict, nfs_lock_type4 protocolLockType)
        {
            if (conflict is null)
            {
                return null;
            }

            ulong clientId = 0UL;
            byte[] ownerBytes = conflict.Owner.ToArray();
            if (TryDecodeHostLockOwnerHandle(ownerBytes, out ulong decodedClientId, out byte[] decodedOwnerBytes))
            {
                clientId = decodedClientId;
                ownerBytes = decodedOwnerBytes;
            }

            return new LOCK4denied
            {
                offset = new offset4
                {
                    Value = conflict.Range.Offset,
                },
                length = new length4
                {
                    Value = conflict.Range.Length,
                },
                locktype = conflict.Exclusive ? nfs_lock_type4.WRITE_LT : nfs_lock_type4.READ_LT,
                owner = new lock_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = ownerBytes,
                },
            };
        }

        private static string BuildOpenFileKey(string exportPath, string sourcePath)
        {
            return exportPath + "|" + Path.GetFullPath(sourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static open_delegation4 CreateNoDelegation()
        {
            return new open_delegation4
            {
                delegation_type = open_delegation_type4.OPEN_DELEGATE_NONE,
            };
        }

        private static open_delegation4 CreateDelegation(Nfs40StateManager.Nfs40DelegationState delegationState)
        {
            return delegationState.DelegationKind switch
            {
                NfsDelegationKind.Read => new open_delegation4
                {
                    delegation_type = open_delegation_type4.OPEN_DELEGATE_READ,
                    read = new open_read_delegation4
                    {
                        stateid = delegationState.StateId,
                        recall = delegationState.RecallRequested,
                        permissions = CreateDelegationPermissionsAce(),
                    },
                },
                NfsDelegationKind.Write => new open_delegation4
                {
                    delegation_type = open_delegation_type4.OPEN_DELEGATE_WRITE,
                    write = new open_write_delegation4
                    {
                        stateid = delegationState.StateId,
                        recall = delegationState.RecallRequested,
                        space_limit = new nfs_space_limit4
                        {
                            limitby = limit_by4.NFS_LIMIT_SIZE,
                            filesize = ulong.MaxValue,
                        },
                        permissions = CreateDelegationPermissionsAce(),
                    },
                },
                _ => CreateNoDelegation(),
            };
        }

        private static nfsace4 CreateDelegationPermissionsAce()
        {
            return new nfsace4
            {
                type = new acetype4
                {
                    Value = (uint)Nfs40Constants.ACE4_ACCESS_ALLOWED_ACE_TYPE,
                },
                flag = new aceflag4
                {
                    Value = 0U,
                },
                access_mask = new acemask4
                {
                    Value = 0xFFFFFFFFU,
                },
                who = new utf8str_mixed
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes("EVERYONE@"),
                    },
                },
            };
        }

        private static secinfo4[] CreateSupportedSecurityInfos()
        {
            return new[]
            {
                new secinfo4
                {
                    flavor = (uint)auth_flavor.AUTH_NONE,
                },
                new secinfo4
                {
                    flavor = (uint)auth_flavor.AUTH_SYS,
                },
            };
        }

        private static bool HasRequestedAttributes(fattr4? attributes)
        {
            if (attributes?.attrmask?.Value is uint[] attributeMaskWords)
            {
                for (int index = 0; index < attributeMaskWords.Length; index++)
                {
                    if (attributeMaskWords[index] != 0U)
                    {
                        return true;
                    }
                }
            }

            return attributes?.attr_vals?.Value is { Length: > 0 };
        }

        private static nfsstat4 CompareVerifiedAttributes(byte[] expectedBytes, byte[] actualBytes, bool expectMatch)
        {
            bool isSame = expectedBytes.AsSpan().SequenceEqual(actualBytes);
            if (expectMatch)
            {
                return isSame ? nfsstat4.NFS4_OK : nfsstat4.NFS4ERR_NOT_SAME;
            }

            return isSame ? nfsstat4.NFS4ERR_SAME : nfsstat4.NFS4_OK;
        }

        private async Task<Nfs40CompoundOperationResult> HandleVerifyAsync(
            fattr4? requestedAttributes,
            Nfs40CompoundState state,
            bool expectMatch,
            CancellationToken cancellationToken)
        {
            Func<nfsstat4, Nfs40CompoundOperationResult> createResult = expectMatch
                ? CreateVerifyResult
                : CreateNotVerifyResult;

            if (!Nfs40AttributeEncoder.TryValidateAttributePayload(
                requestedAttributes,
                includeIdentityAttributes: _server.Capabilities.IdMapper is not null,
                includeAclAttributes: _server.Capabilities.Acls is not null,
                out nfsstat4 validationStatus))
            {
                return createResult(validationStatus);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return createResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return createResult(refreshStatus);
            }

            (fattr4? actualAttributes, nfsstat4 actualStatus) =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    refreshedHandle,
                    requestedAttributes!.attrmask,
                    cancellationToken).ConfigureAwait(false);
            if (actualAttributes is null)
            {
                return createResult(actualStatus);
            }

            byte[] expectedBytes = requestedAttributes.attr_vals?.Value ?? Array.Empty<byte>();
            byte[] actualBytes = actualAttributes.attr_vals?.Value ?? Array.Empty<byte>();
            return createResult(CompareVerifiedAttributes(expectedBytes, actualBytes, expectMatch));
        }

        private Nfs40CompoundOperationResult HandleDelegationPurge(nfs_argop4 operation)
        {
            if (operation.opdelegpurge is null)
            {
                return CreateDelegationPurgeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateDelegationPurgeResult(nfsstat4.NFS4ERR_NOTSUPP);
        }

        private async Task<Nfs40CompoundOperationResult> HandleOpenAttributeAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (operation.opopenattr is null)
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            (Nfs40CompoundResolvedHandle? refreshedHandle, nfsstat4 refreshStatus) =
                await TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshedHandle is null)
            {
                return CreateOpenAttributeResult(refreshStatus);
            }

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_BADTYPE);
            }

            return CreateOpenAttributeResult(nfsstat4.NFS4ERR_NOTSUPP);
        }

        private Nfs40CompoundOperationResult HandleReleaseLockOwner(nfs_argop4 operation)
        {
            if (operation.oprelease_lockowner?.lock_owner is null)
            {
                return CreateReleaseLockOwnerResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateReleaseLockOwnerResult(nfsstat4.NFS4ERR_NOTSUPP);
        }

        private static READDIR4res CreateReadDirectorySuccessResult(
            byte[] cookieVerifier,
            IReadOnlyList<entry4> entries,
            bool eof)
        {
            return new READDIR4res
            {
                status = nfsstat4.NFS4_OK,
                resok4 = new READDIR4resok
                {
                    cookieverf = new verifier4
                    {
                        Value = cookieVerifier,
                    },
                    reply = new dirlist4
                    {
                        entries = Nfs40DirectoryListingSupport.BuildEntryList(entries),
                        eof = eof,
                    },
                },
            };
        }

        private async Task<entry4> CreateDirectoryEntryAsync(
            string exportPath,
            bitmap4? attributeRequest,
            NfsDirectoryEntryInfo directoryEntry,
            ulong cookie,
            CancellationToken cancellationToken)
        {
            NfsFileHandleTarget childTarget = new NfsFileHandleTarget(exportPath, directoryEntry.PathInfo.Path);
            NfsFileHandle childFileHandle =
                await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
            Nfs40CompoundResolvedHandle childHandle =
                new Nfs40CompoundResolvedHandle(childFileHandle, childTarget, directoryEntry.PathInfo);

            (fattr4? attributes, nfsstat4 errorStatus) =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    childHandle,
                    attributeRequest,
                    cancellationToken).ConfigureAwait(false);
            if (attributes is null)
            {
                throw new InvalidDataException(
                    "Unable to encode requested READDIR attributes due to status " + errorStatus.ToString() + ".");
            }

            return new entry4
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                name = new component4
                {
                    Value = new utf8str_cs
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(directoryEntry.Name),
                        },
                    },
                },
                attrs = attributes,
            };
        }

        private async Task<Nfs40CompoundResolvedHandle> CreateResolvedHandleAsync(
            NfsFileHandleTarget target,
            CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await _server.CreateFileHandleAsync(target, cancellationToken).ConfigureAwait(false);
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(target.SourcePath, cancellationToken)).ConfigureAwait(false);

            return new Nfs40CompoundResolvedHandle(fileHandle, target, pathInfoResponse.PathInfo);
        }

        private async Task<NfsPathInfo> GetPathInfoAsync(string sourcePath, CancellationToken cancellationToken)
        {
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(sourcePath, cancellationToken)).ConfigureAwait(false);
            return pathInfoResponse.PathInfo;
        }

        private async Task<NfsPathInfo> LookupChildPathInfoAsync(
            NfsFileHandleTarget directoryTarget,
            string entryName,
            CancellationToken cancellationToken)
        {
            NfsLookupPathResponse lookupResponse =
                await _server.Settings.FileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(
                        directoryTarget.SourcePath,
                        entryName,
                        cancellationToken)).ConfigureAwait(false);
            return lookupResponse.PathInfo;
        }

        private async Task NotifyDelegationRecallsAsync(
            IReadOnlyList<Nfs40StateManager.Nfs40DelegationRecallInfo> recallRequests,
            string exportPath,
            string sourcePath,
            CancellationToken cancellationToken)
        {
            if (_server.Capabilities.Delegations is null || recallRequests.Count == 0)
            {
                return;
            }

            for (int index = 0; index < recallRequests.Count; index++)
            {
                Nfs40StateManager.Nfs40DelegationRecallInfo recall = recallRequests[index];
                await _server.Capabilities.Delegations.RecallDelegationAsync(
                    new NfsRecallDelegationRequest(
                        exportPath,
                        sourcePath,
                        recall.ClientId,
                        recall.DelegationKind,
                        recall.StateId.other ?? Array.Empty<byte>(),
                        NfsDelegationRecallReason.OpenConflict,
                        cancellationToken)).ConfigureAwait(false);
            }
        }

        private async Task<Nfs40CompoundResolvedHandle> RefreshResolvedHandleAsync(
            Nfs40CompoundResolvedHandle resolvedHandle,
            CancellationToken cancellationToken)
        {
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(resolvedHandle.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                throw new FileNotFoundException("The resolved filehandle target no longer exists.", resolvedHandle.Target.SourcePath);
            }

            return new Nfs40CompoundResolvedHandle(
                resolvedHandle.FileHandle,
                resolvedHandle.Target,
                pathInfoResponse.PathInfo);
        }

        private async Task<Nfs40CompoundResolvedHandle> ResolveExistingFileHandleAsync(
            byte[] fileHandleBytes,
            CancellationToken cancellationToken)
        {
            NfsResolveFileHandleResponse resolutionResponse =
                await _server.ResolveFileHandleAsync(
                    new NfsResolveFileHandleRequest(new NfsFileHandle(fileHandleBytes), cancellationToken)).ConfigureAwait(false);

            if (!resolutionResponse.Resolution.Found || resolutionResponse.Resolution.Target is null)
            {
                throw new FileNotFoundException("The supplied NFSv4 filehandle could not be resolved.");
            }

            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(resolutionResponse.Resolution.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                throw new FileNotFoundException(
                    "The resolved NFSv4 filehandle target no longer exists.",
                    resolutionResponse.Resolution.Target.SourcePath);
            }

            return new Nfs40CompoundResolvedHandle(
                new NfsFileHandle(fileHandleBytes),
                resolutionResponse.Resolution.Target,
                pathInfoResponse.PathInfo);
        }

        private static OpenNfsExportDefinition SelectRootExport(IReadOnlyList<OpenNfsExportDefinition> exports)
        {
            OpenNfsExportDefinition selected = exports[0];
            for (int index = 0; index < exports.Count; index++)
            {
                OpenNfsExportDefinition candidate = exports[index];
                if (string.Equals(candidate.ExportPath, "/", StringComparison.Ordinal))
                {
                    return candidate;
                }

                if (string.CompareOrdinal(candidate.ExportPath, selected.ExportPath) < 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private async Task<(Nfs40CompoundResolvedHandle? Handle, nfsstat4 Status)> TryRefreshResolvedHandleAsync(
            Nfs40CompoundResolvedHandle resolvedHandle,
            CancellationToken cancellationToken)
        {
            try
            {
                return (await RefreshResolvedHandleAsync(resolvedHandle, cancellationToken).ConfigureAwait(false), nfsstat4.NFS4_OK);
            }
            catch (FileNotFoundException)
            {
                return (null, nfsstat4.NFS4ERR_STALE);
            }
        }

        private async Task<(Nfs40CompoundResolvedHandle? Handle, nfsstat4 Status)> TryResolveParentHandleAsync(
            Nfs40CompoundResolvedHandle currentHandle,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsExportDefinition> exports =
                await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
            OpenNfsExportDefinition? owningExport = null;

            for (int index = 0; index < exports.Count; index++)
            {
                if (string.Equals(exports[index].ExportPath, currentHandle.Target.ExportPath, StringComparison.Ordinal))
                {
                    owningExport = exports[index];
                    break;
                }
            }

            if (owningExport is null)
            {
                return (null, nfsstat4.NFS4ERR_STALE);
            }

            if (!Nfs40MutationSupport.IsSamePath(currentHandle.Target.SourcePath, owningExport.SourcePath)
                && !Nfs40MutationSupport.IsDescendantPath(currentHandle.Target.SourcePath, owningExport.SourcePath))
            {
                return (null, nfsstat4.NFS4ERR_STALE);
            }

            if (Nfs40MutationSupport.IsSamePath(currentHandle.Target.SourcePath, owningExport.SourcePath))
            {
                return (null, nfsstat4.NFS4ERR_NOENT);
            }

            string? parentSourcePath = OpenNFS.Server.Internal.NfsSourcePath.GetDirectoryName(currentHandle.Target.SourcePath);
            if (string.IsNullOrWhiteSpace(parentSourcePath))
            {
                return (null, nfsstat4.NFS4ERR_NOENT);
            }

            if (!Nfs40MutationSupport.IsSamePath(parentSourcePath, owningExport.SourcePath)
                && !Nfs40MutationSupport.IsDescendantPath(parentSourcePath, owningExport.SourcePath))
            {
                return (null, nfsstat4.NFS4ERR_NOENT);
            }

            Nfs40CompoundResolvedHandle parentHandle =
                await CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(currentHandle.Target.ExportPath, parentSourcePath),
                    cancellationToken).ConfigureAwait(false);
            return (parentHandle, nfsstat4.NFS4_OK);
        }
    }
}
