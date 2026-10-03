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

    internal sealed class Nfs40CompoundLockTestOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundLockTestOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _stateManager = stateManager;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockTestAsync(
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

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateLockTestResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            nfsstat4 handleStatus = ValidateLockableFile(refreshedHandle.PathInfo.Kind);
            if (handleStatus != nfsstat4.NFS4_OK)
            {
                return CreateLockTestResult(handleStatus);
            }

            if (!TryMapLockType(arguments.locktype, out _, out bool exclusive, out _, out nfsstat4 typeStatus))
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
                await _server.Capabilities.TrackedLocking!.ProcessLockAsync(request).ConfigureAwait(false);
            return CreateLockTestResult(
                MapLockDisposition(response.Disposition),
                deniedPayload: response.Disposition == NfsLockDisposition.Denied
                    ? CreateDeniedLock(response.Conflict)
                    : null);
        }
    }
}
