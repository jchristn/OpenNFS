namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundMetadataOperations
    {
        private readonly Nfs40CompoundAccessOperations _accessOperations;
        private readonly Nfs40CompoundGetAttributeOperations _getAttributeOperations;
        private readonly Nfs40CompoundVerifyOperations _verifyOperations;

        internal Nfs40CompoundMetadataOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _accessOperations = new Nfs40CompoundAccessOperations(handleServices);
            _getAttributeOperations = new Nfs40CompoundGetAttributeOperations(server, handleServices);
            _verifyOperations = new Nfs40CompoundVerifyOperations(server, handleServices);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleAccessAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _accessOperations.HandleAccessAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleGetAttributesAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _getAttributeOperations.HandleGetAttributesAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleVerifyAsync(
            fattr4? requestedAttributes,
            Nfs40CompoundState state,
            bool expectMatch,
            CancellationToken cancellationToken)
        {
            return await _verifyOperations.HandleVerifyAsync(
                requestedAttributes,
                state,
                expectMatch,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
