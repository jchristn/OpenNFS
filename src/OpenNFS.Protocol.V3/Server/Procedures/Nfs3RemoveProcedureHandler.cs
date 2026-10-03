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

    internal sealed class Nfs3RemoveProcedureHandler : Nfs3ProcedureHandlerBase<REMOVE3args, REMOVE3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3RemoveProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_REMOVE, REMOVE3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<REMOVE3res> HandleCoreAsync(REMOVE3args arguments, CancellationToken cancellationToken)
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

            if (childPathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, currentDirectoryWcc);
            }

            try
            {
                await _server.Settings.InstrumentedFileSystem.DeletePathAsync(
                    new NfsDeletePathRequest(
                        directoryResolution.Target!.SourcePath,
                        entryName,
                        childPathInfo.Kind,
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

            return new REMOVE3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new REMOVE3resok
                {
                    dir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterDelete),
                },
            };
        }

        protected override void WriteResult(REMOVE3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static REMOVE3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new REMOVE3res
            {
                status = status,
                resfail = new REMOVE3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
