namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using static Nfs40CompoundMetadataResults;

    internal sealed class Nfs40CompoundAccessOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;

        internal Nfs40CompoundAccessOperations(Nfs40CompoundHandleServices handleServices)
        {
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleAccessAsync(
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

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateAccessResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

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
    }
}
