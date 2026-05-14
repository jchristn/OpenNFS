namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundMutationResults;

    internal sealed class Nfs40CompoundDirectoryMutationOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundDirectoryMutationOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleRemoveAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            REMOVE4args? arguments = operation.opremove;
            if (arguments is null)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateRemoveResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedDirectory = refreshResult.Handle;

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.target, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateRemoveResult(nameStatus);
            }

            NfsPathInfo childPathInfo =
                await _handleServices.LookupChildPathInfoAsync(refreshedDirectory.Target, entryName, cancellationToken).ConfigureAwait(false);
            if (!childPathInfo.Exists)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRemoveResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;

            try
            {
                await _server.Settings.FileSystem.DeletePathAsync(
                    new NfsDeletePathRequest(
                        refreshedDirectory.Target.SourcePath,
                        entryName,
                        childPathInfo.Kind,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateRemoveResult(Nfs40MutationSupport.MapDeleteException(exception));
            }

            NfsPathInfo afterDirectoryPathInfo =
                await _handleServices.GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedDirectory.FileHandle,
                    refreshedDirectory.Target,
                    afterDirectoryPathInfo));

            return CreateRemoveResult(
                nfsstat4.NFS4_OK,
                new REMOVE4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                });
        }

        internal async Task<Nfs40CompoundOperationResult> HandleRenameAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            RENAME4args? arguments = operation.oprename;
            if (arguments is null)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetSavedHandle(out Nfs40CompoundResolvedHandle? savedHandle)
                || !state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus sourceRefreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(savedHandle!, cancellationToken).ConfigureAwait(false);
            if (sourceRefreshResult.Handle is null)
            {
                return CreateRenameResult(sourceRefreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedSourceDirectory = sourceRefreshResult.Handle;

            ResolvedHandleStatus targetRefreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (targetRefreshResult.Handle is null)
            {
                return CreateRenameResult(targetRefreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedTargetDirectory = targetRefreshResult.Handle;

            if (refreshedSourceDirectory.PathInfo.Kind == NfsPathKind.Other
                || refreshedTargetDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedSourceDirectory.PathInfo.Kind != NfsPathKind.Directory
                || refreshedTargetDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!string.Equals(
                refreshedSourceDirectory.Target.ExportPath,
                refreshedTargetDirectory.Target.ExportPath,
                StringComparison.Ordinal))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_XDEV);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.oldname, out string oldName, out nfsstat4 oldNameStatus))
            {
                return CreateRenameResult(oldNameStatus);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.newname, out string newName, out nfsstat4 newNameStatus))
            {
                return CreateRenameResult(newNameStatus);
            }

            NfsPathInfo sourceChildPathInfo =
                await _handleServices.LookupChildPathInfoAsync(refreshedSourceDirectory.Target, oldName, cancellationToken).ConfigureAwait(false);
            if (!sourceChildPathInfo.Exists)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (sourceChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            string destinationSourcePath = OpenNFS.Server.Internal.NfsSourcePath.Combine(
                refreshedTargetDirectory.Target.SourcePath,
                newName);
            if (sourceChildPathInfo.Kind == NfsPathKind.Directory
                && Nfs40MutationSupport.IsDescendantPath(destinationSourcePath, sourceChildPathInfo.Path))
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_INVAL);
            }

            NfsPathInfo destinationChildPathInfo =
                await _handleServices.LookupChildPathInfoAsync(refreshedTargetDirectory.Target, newName, cancellationToken).ConfigureAwait(false);
            if (destinationChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateRenameResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (Nfs40MutationSupport.IsSamePath(sourceChildPathInfo.Path, destinationSourcePath))
            {
                return CreateRenameResult(
                    nfsstat4.NFS4_OK,
                    new RENAME4resok
                    {
                        source_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                            refreshedSourceDirectory.PathInfo,
                            refreshedSourceDirectory.PathInfo),
                        target_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                            refreshedTargetDirectory.PathInfo,
                            refreshedTargetDirectory.PathInfo),
                    });
            }

            if (destinationChildPathInfo.Exists)
            {
                bool sourceIsDirectory = sourceChildPathInfo.Kind == NfsPathKind.Directory;
                bool destinationIsDirectory = destinationChildPathInfo.Kind == NfsPathKind.Directory;
                if (sourceIsDirectory != destinationIsDirectory)
                {
                    return CreateRenameResult(nfsstat4.NFS4ERR_EXIST);
                }

                if (destinationIsDirectory)
                {
                    NfsReadDirectoryResponse destinationDirectoryResponse =
                        await _server.Settings.FileSystem.ReadDirectoryAsync(
                            new NfsReadDirectoryRequest(destinationChildPathInfo.Path, cancellationToken)).ConfigureAwait(false);
                    if (destinationDirectoryResponse.Entries.Count > 0)
                    {
                        return CreateRenameResult(nfsstat4.NFS4ERR_EXIST);
                    }
                }
            }

            NfsPathInfo beforeSourceDirectoryPathInfo = refreshedSourceDirectory.PathInfo;
            NfsPathInfo beforeTargetDirectoryPathInfo = refreshedTargetDirectory.PathInfo;

            try
            {
                await _server.Settings.FileSystem.RenamePathAsync(
                    new NfsRenamePathRequest(
                        refreshedSourceDirectory.Target.SourcePath,
                        oldName,
                        refreshedTargetDirectory.Target.SourcePath,
                        newName,
                        sourceChildPathInfo.Kind,
                        replaceExistingDestination: destinationChildPathInfo.Exists,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateRenameResult(Nfs40MutationSupport.MapRenameException(exception));
            }

            NfsPathInfo afterSourceDirectoryPathInfo =
                await _handleServices.GetPathInfoAsync(refreshedSourceDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            NfsPathInfo afterTargetDirectoryPathInfo =
                await _handleServices.GetPathInfoAsync(refreshedTargetDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);

            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedTargetDirectory.FileHandle,
                    refreshedTargetDirectory.Target,
                    afterTargetDirectoryPathInfo));

            return CreateRenameResult(
                nfsstat4.NFS4_OK,
                new RENAME4resok
                {
                    source_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                        beforeSourceDirectoryPathInfo,
                        afterSourceDirectoryPathInfo),
                    target_cinfo = Nfs40MutationSupport.CreateChangeInfo(
                        beforeTargetDirectoryPathInfo,
                        afterTargetDirectoryPathInfo),
                });
        }
    }
}
