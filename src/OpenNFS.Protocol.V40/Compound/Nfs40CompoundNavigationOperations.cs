namespace OpenNFS.Protocol.V40.Compound
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal sealed class Nfs40CompoundNavigationOperations
    {
        private readonly Nfs40CompoundFileHandleNavigationOperations _fileHandleOperations;
        private readonly Nfs40CompoundNamespaceLookupOperations _namespaceLookupOperations;

        internal Nfs40CompoundNavigationOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _fileHandleOperations = new Nfs40CompoundFileHandleNavigationOperations(server, handleServices);
            _namespaceLookupOperations = new Nfs40CompoundNamespaceLookupOperations(server, handleServices);
        }

        internal Nfs40CompoundOperationResult HandleGetFileHandle(Nfs40CompoundState state)
        {
            return _fileHandleOperations.HandleGetFileHandle(state);
        }

        internal Nfs40CompoundOperationResult HandleRestoreFileHandle(Nfs40CompoundState state)
        {
            return _fileHandleOperations.HandleRestoreFileHandle(state);
        }

        internal Nfs40CompoundOperationResult HandleSaveFileHandle(Nfs40CompoundState state)
        {
            return _fileHandleOperations.HandleSaveFileHandle(state);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleSecurityInfoAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _namespaceLookupOperations.HandleSecurityInfoAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLookupAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _namespaceLookupOperations.HandleLookupAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLookupParentAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _namespaceLookupOperations.HandleLookupParentAsync(
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutFileHandleAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _fileHandleOperations.HandlePutFileHandleAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutPublicFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _fileHandleOperations.HandlePutPublicFileHandleAsync(
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutRootFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _fileHandleOperations.HandlePutRootFileHandleAsync(
                state,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
