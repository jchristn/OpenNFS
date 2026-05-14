namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundNamespaceMutationOperations
    {
        private readonly Nfs40CompoundDirectoryMutationOperations _directoryMutationOperations;
        private readonly Nfs40CompoundLinkOperations _linkOperations;

        internal Nfs40CompoundNamespaceMutationOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _directoryMutationOperations = new Nfs40CompoundDirectoryMutationOperations(server, handleServices);
            _linkOperations = new Nfs40CompoundLinkOperations(server, handleServices);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLinkAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _linkOperations.HandleLinkAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleRemoveAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _directoryMutationOperations.HandleRemoveAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleRenameAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _directoryMutationOperations.HandleRenameAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
