namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs3RmDirProcedureHandler : Nfs3ProcedureHandlerBase<RMDIR3args, RMDIR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3RmDirProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RMDIR, RMDIR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<RMDIR3res> HandleCoreAsync(RMDIR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object?.dir, cancellationToken).ConfigureAwait(false);

            if (directoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(
                    directoryResolution.Status,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null));
            }

            wcc_data currentDirectoryWcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                directoryResolution.Target,
                directoryResolution.PathInfo,
                directoryResolution.PathInfo);

            if (directoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentDirectoryWcc);
            }

            string entryName = arguments.@object?.name?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            NfsPathInfo childPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    directoryResolution.Target!,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (!childPathInfo.Exists)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentDirectoryWcc);
            }

            if (childPathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentDirectoryWcc);
            }

            NfsReadDirectoryResponse childDirectoryResponse =
                await _server.Settings.FileSystem.ReadDirectoryAsync(
                    new NfsReadDirectoryRequest(childPathInfo.Path, cancellationToken)).ConfigureAwait(false);

            if (childDirectoryResponse.Entries.Count > 0)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTEMPTY, currentDirectoryWcc);
            }

            try
            {
                await _server.Settings.FileSystem.DeletePathAsync(
                    new NfsDeletePathRequest(
                        directoryResolution.Target!.SourcePath,
                        entryName,
                        NfsPathKind.Directory,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is System.IO.IOException
                || exception is NotSupportedException
                || exception is System.IO.DirectoryNotFoundException
                || exception is System.IO.FileNotFoundException
                || exception is System.IO.PathTooLongException)
            {
                NfsPathInfo parentAfterException =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        directoryResolution.Target!.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    Nfs3DirectoryMutationSupport.MapDeleteException(exception),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterException));
            }

            NfsPathInfo parentAfterDelete =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    directoryResolution.Target!.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            return new RMDIR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new RMDIR3resok
                {
                    dir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterDelete),
                },
            };
        }

        protected override void WriteResult(RMDIR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static RMDIR3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new RMDIR3res
            {
                status = status,
                resfail = new RMDIR3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
