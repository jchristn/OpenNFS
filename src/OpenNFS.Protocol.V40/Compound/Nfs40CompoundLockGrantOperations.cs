namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundLockResults;
    using static Nfs40CompoundLockSupport;

    internal sealed class Nfs40CompoundLockGrantOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundLockGrantOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _stateManager = stateManager;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockAsync(
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

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateLockResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            nfsstat4 handleStatus = ValidateLockableFile(refreshedHandle.PathInfo.Kind);
            if (handleStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockResult(handleStatus);
            }

            if (!TryMapLockType(arguments.locktype, out _, out bool exclusive, out bool block, out nfsstat4 typeStatus))
            {
                return CreateLockResult(typeStatus);
            }

            if (arguments.offset is null || arguments.length is null || arguments.locker is null)
            {
                return CreateLockResult(nfsstat4.NFS4ERR_BADXDR);
            }

            string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            Nfs40LockPreparationResult preparation;
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
                Nfs40LockTransitionResult transition =
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
                    ? CreateDeniedLock(response.Conflict)
                    : null);
        }
    }
}
