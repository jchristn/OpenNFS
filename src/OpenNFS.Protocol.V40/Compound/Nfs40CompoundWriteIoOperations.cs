namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundIoResults;
    using static Nfs40CompoundIoSupport;

    internal sealed class Nfs40CompoundWriteIoOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;
        private readonly Nfs40WriteStateTracker _writeState;

        internal Nfs40CompoundWriteIoOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40WriteStateTracker writeState,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _stateManager = stateManager;
            _writeState = writeState;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleCommitAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            COMMIT4args? arguments = operation.opcommit;
            if (arguments?.offset is null || arguments.count is null)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateCommitResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateCommitResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsCommitFileResponse commitResponse;
            try
            {
                commitResponse =
                    await _server.Settings.InstrumentedFileSystem.CommitFileAsync(
                        new NfsCommitFileRequest(
                            refreshedHandle.Target.SourcePath,
                            arguments.offset.Value,
                            arguments.count.Value,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException
                || exception is OverflowException)
            {
                return CreateCommitResult(Nfs40MutationSupport.MapCommitException(exception));
            }

            if (!commitResponse.PathInfo.Exists)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_STALE);
            }

            if (commitResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateCommitResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (commitResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateCommitResult(
                    commitResponse.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            return CreateCommitResult(
                nfsstat4.NFS4_OK,
                new COMMIT4resok
                {
                    writeverf = CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                });
        }

        internal async Task<Nfs40CompoundOperationResult> HandleWriteAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            WRITE4args? arguments = operation.opwrite;
            if (arguments?.stateid?.other is null || arguments.offset is null || arguments.stable is null || arguments.data is null)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateWriteResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateWriteResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            nfsstat4 stateStatus = _stateManager.ValidateWriteState(arguments.stateid, fileKey);
            if (stateStatus != nfsstat4.NFS4_OK)
            {
                return CreateWriteResult(stateStatus);
            }

            if (!TryMapWriteStability(arguments.stable, out NfsWriteStability requestedStability))
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_INVAL);
            }

            NfsWriteFileResponse writeResponse;
            try
            {
                writeResponse =
                    await _server.Settings.InstrumentedFileSystem.WriteFileAsync(
                        new NfsWriteFileRequest(
                            refreshedHandle.Target.SourcePath,
                            arguments.offset.Value,
                            arguments.data,
                            requestedStability,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException
                || exception is OverflowException)
            {
                return CreateWriteResult(Nfs40MutationSupport.MapWriteException(exception));
            }

            if (!writeResponse.PathInfo.Exists)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_STALE);
            }

            if (writeResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateWriteResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (writeResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateWriteResult(
                    writeResponse.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            return CreateWriteResult(
                nfsstat4.NFS4_OK,
                new WRITE4resok
                {
                    count = new count4
                    {
                        Value = writeResponse.BytesWritten,
                    },
                    committed = MapWriteStability(writeResponse.CommittedStability),
                    writeverf = CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                });
        }
    }
}
