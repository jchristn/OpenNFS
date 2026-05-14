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

    internal sealed class Nfs40CompoundLockUnlockOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundLockUnlockOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _stateManager = stateManager;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockUnlockAsync(
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

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateLockUnlockResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

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

            string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            Nfs40LockPreparationResult preparation =
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

            Nfs40LockTransitionResult transition =
                _stateManager.CommitUnlock(preparation.PendingOperation);
            return CreateLockUnlockResult(transition.Status, transition.StateId);
        }
    }
}
