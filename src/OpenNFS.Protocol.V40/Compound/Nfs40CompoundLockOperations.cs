namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundLockOperations
    {
        private readonly Nfs40CompoundLockGrantOperations _grantOperations;
        private readonly Nfs40CompoundLockTestOperations _testOperations;
        private readonly Nfs40CompoundLockUnlockOperations _unlockOperations;

        internal Nfs40CompoundLockOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _grantOperations = new Nfs40CompoundLockGrantOperations(server, stateManager, handleServices);
            _testOperations = new Nfs40CompoundLockTestOperations(server, stateManager, handleServices);
            _unlockOperations = new Nfs40CompoundLockUnlockOperations(server, stateManager, handleServices);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _grantOperations.HandleLockAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockTestAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _testOperations.HandleLockTestAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLockUnlockAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _unlockOperations.HandleLockUnlockAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
