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

    internal sealed class Nfs40CompoundLinkOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundLinkOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLinkAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LINK4args? arguments = operation.oplink;
            if (arguments is null)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetSavedHandle(out Nfs40CompoundResolvedHandle? savedHandle)
                || !state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus sourceRefreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(savedHandle!, cancellationToken).ConfigureAwait(false);
            if (sourceRefreshResult.Handle is null)
            {
                return CreateLinkResult(sourceRefreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedSource = sourceRefreshResult.Handle;

            ResolvedHandleStatus directoryRefreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (directoryRefreshResult.Handle is null)
            {
                return CreateLinkResult(directoryRefreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedDirectory = directoryRefreshResult.Handle;

            if (refreshedSource.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedSource.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateLinkResult(
                    refreshedSource.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!string.Equals(
                refreshedSource.Target.ExportPath,
                refreshedDirectory.Target.ExportPath,
                StringComparison.Ordinal))
            {
                return CreateLinkResult(nfsstat4.NFS4ERR_XDEV);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.newname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateLinkResult(nameStatus);
            }

            NfsPathInfo existingChildPathInfo =
                await _handleServices.LookupChildPathInfoAsync(refreshedDirectory.Target, entryName, cancellationToken).ConfigureAwait(false);
            if (existingChildPathInfo.Exists)
            {
                return CreateLinkResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_EXIST);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;

            try
            {
                NfsCreateHardLinkResponse createResponse =
                    await _server.Settings.FileSystem.CreateHardLinkAsync(
                        new NfsCreateHardLinkRequest(
                            refreshedSource.Target.SourcePath,
                            refreshedDirectory.Target.SourcePath,
                            entryName,
                            cancellationToken)).ConfigureAwait(false);

                if (!createResponse.SourcePathInfo.Exists || !createResponse.LinkPathInfo.Exists)
                {
                    return CreateLinkResult(nfsstat4.NFS4ERR_SERVERFAULT);
                }

                if (createResponse.SourcePathInfo.Kind != NfsPathKind.File || createResponse.LinkPathInfo.Kind != NfsPathKind.File)
                {
                    return CreateLinkResult(
                        createResponse.SourcePathInfo.Kind == NfsPathKind.Other
                        || createResponse.LinkPathInfo.Kind == NfsPathKind.Other
                            ? nfsstat4.NFS4ERR_NOTSUPP
                            : nfsstat4.NFS4ERR_SERVERFAULT);
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                return CreateLinkResult(Nfs40MutationSupport.MapHardLinkException(exception));
            }

            NfsPathInfo afterDirectoryPathInfo =
                await _handleServices.GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(
                new Nfs40CompoundResolvedHandle(
                    refreshedDirectory.FileHandle,
                    refreshedDirectory.Target,
                    afterDirectoryPathInfo));

            return CreateLinkResult(
                nfsstat4.NFS4_OK,
                new LINK4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                });
        }
    }
}
