namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using static Nfs40CompoundMetadataResults;

    internal sealed class Nfs40CompoundGetAttributeOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundGetAttributeOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleGetAttributesAsync(
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

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateGetAttrResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateGetAttrResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            TryCreateAttributesResult attributeResult =
                await Nfs40AttributeEncoder.TryCreateAttributesAsync(
                    _server,
                    refreshedHandle,
                    arguments.attr_request,
                    cancellationToken).ConfigureAwait(false);
            if (attributeResult.Attributes is null)
            {
                return CreateGetAttrResult(attributeResult.ErrorStatus);
            }

            return CreateGetAttrResult(nfsstat4.NFS4_OK, attributeResult.Attributes);
        }
    }
}
