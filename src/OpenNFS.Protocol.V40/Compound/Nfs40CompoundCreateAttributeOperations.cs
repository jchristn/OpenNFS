namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundCreateAttributeOperations
    {
        private readonly Nfs40CompoundCreateOperations _createOperations;
        private readonly Nfs40CompoundSetAttributeOperations _setAttributeOperations;

        internal Nfs40CompoundCreateAttributeOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _createOperations = new Nfs40CompoundCreateOperations(server, handleServices);
            _setAttributeOperations = new Nfs40CompoundSetAttributeOperations(server, handleServices);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleCreateAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _createOperations.HandleCreateAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleSetAttributesAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _setAttributeOperations.HandleSetAttributesAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
