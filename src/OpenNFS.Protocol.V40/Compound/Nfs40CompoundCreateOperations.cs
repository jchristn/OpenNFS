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

    internal sealed class Nfs40CompoundCreateOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundCreateOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleCreateAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            CREATE4args? arguments = operation.opcreate;
            nfs_ftype4? requestedType = arguments?.objtype?.type;
            if (arguments is null || !requestedType.HasValue)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_BADXDR);
            }

            createtype4 objectType = arguments.objtype!;

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateCreateResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedDirectory = refreshResult.Handle;

            if (refreshedDirectory.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedDirectory.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateCreateResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.objname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateCreateResult(nameStatus);
            }

            NfsPathInfo beforeDirectoryPathInfo = refreshedDirectory.PathInfo;
            Nfs40CompoundResolvedHandle? createdHandle = null;

            switch (requestedType.Value)
            {
                case nfs_ftype4.NF4DIR:
                {
                    Nfs40CreateAttemptResult directoryResult = await TryCreateDirectoryAsync(
                        refreshedDirectory,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
                    if (directoryResult.Status != nfsstat4.NFS4_OK || directoryResult.CreatedHandle is null)
                    {
                        return CreateCreateResult(directoryResult.Status);
                    }

                    createdHandle = directoryResult.CreatedHandle;
                    break;
                }

                case nfs_ftype4.NF4LNK:
                {
                    Nfs40CreateAttemptResult linkResult = await TryCreateSymbolicLinkAsync(
                        objectType,
                        refreshedDirectory,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
                    if (linkResult.Status != nfsstat4.NFS4_OK || linkResult.CreatedHandle is null)
                    {
                        return CreateCreateResult(linkResult.Status);
                    }

                    createdHandle = linkResult.CreatedHandle;
                    break;
                }

                case nfs_ftype4.NF4REG:
                case nfs_ftype4.NF4ATTRDIR:
                case nfs_ftype4.NF4NAMEDATTR:
                    return CreateCreateResult(nfsstat4.NFS4ERR_BADTYPE);

                default:
                    return CreateCreateResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            NfsPathInfo afterDirectoryPathInfo =
                await _handleServices.GetPathInfoAsync(refreshedDirectory.Target.SourcePath, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(createdHandle);
            return CreateCreateResult(
                nfsstat4.NFS4_OK,
                new CREATE4resok
                {
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(beforeDirectoryPathInfo, afterDirectoryPathInfo),
                    attrset = Nfs40MutationSupport.CreateEmptyAttributeSet(),
                });
        }

        private async Task<Nfs40CreateAttemptResult> TryCreateDirectoryAsync(
            Nfs40CompoundResolvedHandle refreshedDirectory,
            string entryName,
            CancellationToken cancellationToken)
        {
            try
            {
                NfsCreatePathResponse createDirectoryResponse =
                    await _server.Settings.InstrumentedFileSystem.CreatePathAsync(
                        new NfsCreatePathRequest(
                            refreshedDirectory.Target.SourcePath,
                            entryName,
                            NfsPathKind.Directory,
                            failIfExists: true,
                            cancellationToken)).ConfigureAwait(false);

                if (!createDirectoryResponse.CreatedNew)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_EXIST, null);
                }

                if (!createDirectoryResponse.PathInfo.Exists)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_SERVERFAULT, null);
                }

                if (createDirectoryResponse.PathInfo.Kind == NfsPathKind.Other)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_NOTSUPP, null);
                }

                if (createDirectoryResponse.PathInfo.Kind != NfsPathKind.Directory)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_SERVERFAULT, null);
                }

                Nfs40CompoundResolvedHandle createdHandle =
                    await _handleServices.CreateResolvedHandleAsync(
                        new NfsFileHandleTarget(
                            refreshedDirectory.Target.ExportPath,
                            createDirectoryResponse.PathInfo.Path),
                        cancellationToken).ConfigureAwait(false);
                return new Nfs40CreateAttemptResult(nfsstat4.NFS4_OK, createdHandle);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException)
            {
                return new Nfs40CreateAttemptResult(
                    Nfs40MutationSupport.MapCreateException(exception),
                    null);
            }
        }

        private async Task<Nfs40CreateAttemptResult> TryCreateSymbolicLinkAsync(
            createtype4 objectType,
            Nfs40CompoundResolvedHandle refreshedDirectory,
            string entryName,
            CancellationToken cancellationToken)
        {
            if (!Nfs40MutationSupport.TryReadLinkTarget(objectType.linkdata, out string targetPath, out nfsstat4 linkStatus))
            {
                return new Nfs40CreateAttemptResult(linkStatus, null);
            }

            try
            {
                NfsCreateSymbolicLinkResponse createLinkResponse =
                    await _server.Settings.InstrumentedFileSystem.CreateSymbolicLinkAsync(
                        new NfsCreateSymbolicLinkRequest(
                            refreshedDirectory.Target.SourcePath,
                            entryName,
                            targetPath,
                            failIfExists: true,
                            cancellationToken)).ConfigureAwait(false);

                if (!createLinkResponse.CreatedNew)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_EXIST, null);
                }

                if (!createLinkResponse.PathInfo.Exists)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_SERVERFAULT, null);
                }

                if (createLinkResponse.PathInfo.Kind == NfsPathKind.Other)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_NOTSUPP, null);
                }

                if (createLinkResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
                {
                    return new Nfs40CreateAttemptResult(nfsstat4.NFS4ERR_SERVERFAULT, null);
                }

                Nfs40CompoundResolvedHandle createdHandle =
                    await _handleServices.CreateResolvedHandleAsync(
                        new NfsFileHandleTarget(
                            refreshedDirectory.Target.ExportPath,
                            createLinkResponse.PathInfo.Path),
                        cancellationToken).ConfigureAwait(false);
                return new Nfs40CreateAttemptResult(nfsstat4.NFS4_OK, createdHandle);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException
                || exception is ArgumentException)
            {
                return new Nfs40CreateAttemptResult(
                    Nfs40MutationSupport.MapCreateException(exception),
                    null);
            }
        }

        private sealed class Nfs40CreateAttemptResult
        {
            internal Nfs40CreateAttemptResult(
                nfsstat4 status,
                Nfs40CompoundResolvedHandle? createdHandle)
            {
                Status = status;
                CreatedHandle = createdHandle;
            }

            internal Nfs40CompoundResolvedHandle? CreatedHandle { get; }

            internal nfsstat4 Status { get; }
        }
    }
}
