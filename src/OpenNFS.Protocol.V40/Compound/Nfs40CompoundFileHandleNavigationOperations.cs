namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using static Nfs40CompoundNavigationResults;

    internal sealed class Nfs40CompoundFileHandleNavigationOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundFileHandleNavigationOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal Nfs40CompoundOperationResult HandleGetFileHandle(Nfs40CompoundState state)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateGetFileHandleResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            return CreateGetFileHandleResult(nfsstat4.NFS4_OK, currentHandle);
        }

        internal Nfs40CompoundOperationResult HandleRestoreFileHandle(Nfs40CompoundState state)
        {
            if (!state.TryRestoreSavedHandle(out _))
            {
                return CreateRestoreFileHandleResult(nfsstat4.NFS4ERR_RESTOREFH);
            }

            return CreateRestoreFileHandleResult(nfsstat4.NFS4_OK);
        }

        internal Nfs40CompoundOperationResult HandleSaveFileHandle(Nfs40CompoundState state)
        {
            if (!state.SaveCurrentHandle())
            {
                return CreateSaveFileHandleResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            return CreateSaveFileHandleResult(nfsstat4.NFS4_OK);
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutFileHandleAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            PUTFH4args? arguments = operation.opputfh;
            if (arguments?.@object?.Value is null || arguments.@object.Value.Length == 0)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }

            if (arguments.@object.Value.Length > (int)Nfs40Constants.NFS4_FHSIZE)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }

            try
            {
                Nfs40CompoundResolvedHandle resolvedHandle =
                    await _handleServices.ResolveExistingFileHandleAsync(
                        arguments.@object.Value,
                        cancellationToken).ConfigureAwait(false);
                state.SetCurrentHandle(resolvedHandle);
                return CreatePutFileHandleResult(nfsstat4.NFS4_OK);
            }
            catch (FileNotFoundException)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_STALE);
            }
            catch (InvalidDataException)
            {
                return CreatePutFileHandleResult(nfsstat4.NFS4ERR_BADHANDLE);
            }
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutPublicFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await HandleNamespaceRootFileHandleAsync(
                state,
                CreatePutPublicFileHandleResult,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandlePutRootFileHandleAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await HandleNamespaceRootFileHandleAsync(
                state,
                CreatePutRootFileHandleResult,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<Nfs40CompoundOperationResult> HandleNamespaceRootFileHandleAsync(
            Nfs40CompoundState state,
            Func<nfsstat4, Nfs40CompoundOperationResult> createResult,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsExportDefinition> exports =
                await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
            if (exports.Count == 0)
            {
                return createResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            OpenNfsExportDefinition rootExport = SelectRootExport(exports);
            Nfs40CompoundResolvedHandle resolvedHandle =
                await _handleServices.CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(rootExport.ExportPath, rootExport.SourcePath),
                    cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(resolvedHandle);
            return createResult(nfsstat4.NFS4_OK);
        }

        private static OpenNfsExportDefinition SelectRootExport(IReadOnlyList<OpenNfsExportDefinition> exports)
        {
            OpenNfsExportDefinition selected = exports[0];
            for (int index = 0; index < exports.Count; index++)
            {
                OpenNfsExportDefinition candidate = exports[index];
                if (string.Equals(candidate.ExportPath, "/", StringComparison.Ordinal))
                {
                    return candidate;
                }

                if (string.CompareOrdinal(candidate.ExportPath, selected.ExportPath) < 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }
    }
}
