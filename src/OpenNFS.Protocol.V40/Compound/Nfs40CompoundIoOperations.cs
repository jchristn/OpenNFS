namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundIoOperations
    {
        private readonly Nfs40CompoundReadIoOperations _readOperations;
        private readonly Nfs40CompoundWriteIoOperations _writeOperations;

        internal Nfs40CompoundIoOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40WriteStateTracker writeState,
            Nfs40CompoundHandleServices handleServices)
        {
            _readOperations = new Nfs40CompoundReadIoOperations(server, handleServices);
            _writeOperations = new Nfs40CompoundWriteIoOperations(
                server,
                stateManager,
                writeState,
                handleServices);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleCommitAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _writeOperations.HandleCommitAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleWriteAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _writeOperations.HandleWriteAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleReadAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _readOperations.HandleReadAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleReadLinkAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _readOperations.HandleReadLinkAsync(
                state,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
