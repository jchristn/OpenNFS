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
    using static Nfs40CompoundOpenSupport;

    internal sealed class Nfs40CompoundNamedOpenTargetOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundNamedOpenTargetOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40NamedOpenResolutionResult> ResolveNamedOpenTargetAsync(
            OPEN4args arguments,
            Nfs40CompoundResolvedHandle refreshedHandle,
            string entryName,
            opentype4 openType,
            CancellationToken cancellationToken)
        {
            switch (openType)
            {
                case opentype4.OPEN4_NOCREATE:
                    return await ResolveNoCreateTargetAsync(
                        refreshedHandle,
                        entryName,
                        cancellationToken).ConfigureAwait(false);

                case opentype4.OPEN4_CREATE:
                    return await ResolveCreateTargetAsync(
                        arguments,
                        refreshedHandle,
                        entryName,
                        cancellationToken).ConfigureAwait(false);

                default:
                    return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_BADXDR, null, null);
            }
        }

        private async Task<Nfs40NamedOpenResolutionResult> ResolveCreateTargetAsync(
            OPEN4args arguments,
            Nfs40CompoundResolvedHandle refreshedHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            createhow4? createHow = arguments.openhow?.how;
            createmode4? createMode = createHow?.mode;
            if (!createMode.HasValue)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_BADXDR, null, null);
            }

            if (createMode.Value == createmode4.EXCLUSIVE4)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_NOTSUPP, null, null);
            }

            if (createHow?.createattrs is null)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_BADXDR, null, null);
            }

            if (HasRequestedAttributes(createHow.createattrs))
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_ATTRNOTSUPP, null, null);
            }

            NfsPathInfo childPathInfo =
                await _handleServices.LookupChildPathInfoAsync(
                    refreshedHandle.Target,
                    entryName,
                    cancellationToken).ConfigureAwait(false);
            if (childPathInfo.Exists)
            {
                return await ResolveExistingCreateTargetAsync(
                    createMode.Value,
                    refreshedHandle,
                    childPathInfo,
                    cancellationToken).ConfigureAwait(false);
            }

            try
            {
                NfsCreatePathResponse createResponse =
                    await _server.Settings.FileSystem.CreatePathAsync(
                        new NfsCreatePathRequest(
                            refreshedHandle.Target.SourcePath,
                            entryName,
                            NfsPathKind.File,
                            failIfExists: true,
                            cancellationToken)).ConfigureAwait(false);

                if (!createResponse.CreatedNew)
                {
                    return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_EXIST, null, null);
                }

                if (!createResponse.PathInfo.Exists)
                {
                    return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_SERVERFAULT, null, null);
                }

                if (createResponse.PathInfo.Kind == NfsPathKind.Other)
                {
                    return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_NOTSUPP, null, null);
                }

                if (createResponse.PathInfo.Kind != NfsPathKind.File)
                {
                    return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_SERVERFAULT, null, null);
                }

                Nfs40CompoundResolvedHandle createdHandle =
                    await _handleServices.CreateResolvedHandleAsync(
                        new NfsFileHandleTarget(
                            refreshedHandle.Target.ExportPath,
                            createResponse.PathInfo.Path),
                        cancellationToken).ConfigureAwait(false);
                NfsPathInfo afterChangePathInfo =
                    await _handleServices.GetPathInfoAsync(
                        refreshedHandle.Target.SourcePath,
                        cancellationToken).ConfigureAwait(false);
                return new Nfs40NamedOpenResolutionResult(
                    nfsstat4.NFS4_OK,
                    createdHandle,
                    afterChangePathInfo);
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
                return new Nfs40NamedOpenResolutionResult(
                    Nfs40MutationSupport.MapCreateException(exception),
                    null,
                    null);
            }
        }

        private async Task<Nfs40NamedOpenResolutionResult> ResolveExistingCreateTargetAsync(
            createmode4 createMode,
            Nfs40CompoundResolvedHandle refreshedHandle,
            NfsPathInfo childPathInfo,
            CancellationToken cancellationToken)
        {
            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_NOTSUPP, null, null);
            }

            if (createMode == createmode4.GUARDED4)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_EXIST, null, null);
            }

            if (childPathInfo.Kind == NfsPathKind.Directory)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_ISDIR, null, null);
            }

            if (childPathInfo.Kind == NfsPathKind.SymbolicLink)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_SYMLINK, null, null);
            }

            Nfs40CompoundResolvedHandle existingHandle =
                await _handleServices.CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(
                        refreshedHandle.Target.ExportPath,
                        childPathInfo.Path),
                    cancellationToken).ConfigureAwait(false);
            return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4_OK, existingHandle, null);
        }

        private async Task<Nfs40NamedOpenResolutionResult> ResolveNoCreateTargetAsync(
            Nfs40CompoundResolvedHandle refreshedHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            NfsPathInfo childPathInfo =
                await _handleServices.LookupChildPathInfoAsync(
                    refreshedHandle.Target,
                    entryName,
                    cancellationToken).ConfigureAwait(false);
            if (!childPathInfo.Exists)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_NOENT, null, null);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_NOTSUPP, null, null);
            }

            if (childPathInfo.Kind == NfsPathKind.Directory)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_ISDIR, null, null);
            }

            if (childPathInfo.Kind == NfsPathKind.SymbolicLink)
            {
                return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4ERR_SYMLINK, null, null);
            }

            Nfs40CompoundResolvedHandle openedHandle = await _handleServices.CreateResolvedHandleAsync(
                new NfsFileHandleTarget(
                    refreshedHandle.Target.ExportPath,
                    childPathInfo.Path),
                cancellationToken).ConfigureAwait(false);
            return new Nfs40NamedOpenResolutionResult(nfsstat4.NFS4_OK, openedHandle, null);
        }
    }
}
