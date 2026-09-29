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

    internal sealed class Nfs3MkdirProcedureHandler : Nfs3ProcedureHandlerBase<MKDIR3args, MKDIR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3MkdirProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKDIR, MKDIR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<MKDIR3res> HandleCoreAsync(MKDIR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.where?.dir, cancellationToken).ConfigureAwait(false);

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

            string entryName = arguments.where?.name?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            NfsPathInfo existingChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    directoryResolution.Target!,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (existingChildPathInfo.Exists)
            {
                return CreateFailureResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_EXIST,
                    currentDirectoryWcc);
            }

            NfsCreatePathResponse createResponse;
            try
            {
                createResponse =
                    await _server.Settings.FileSystem.CreatePathAsync(
                        new NfsCreatePathRequest(
                            directoryResolution.Target!.SourcePath,
                            entryName,
                            NfsPathKind.Directory,
                            failIfExists: true,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is System.IO.IOException
                || exception is NotSupportedException
                || exception is System.IO.DirectoryNotFoundException
                || exception is System.IO.FileNotFoundException
                || exception is System.IO.PathTooLongException
                || exception is ArgumentException)
            {
                NfsPathInfo parentAfterException =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        directoryResolution.Target!.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    Nfs3DirectoryMutationSupport.MapCreateException(exception, failIfExists: true),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterException));
            }

            if (!createResponse.PathInfo.Exists || createResponse.PathInfo.Kind != NfsPathKind.Directory)
            {
                NfsPathInfo parentAfterFailure =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        directoryResolution.Target!.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    createResponse.PathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_SERVERFAULT,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterFailure));
            }

            NfsPathInfo parentAfterCreate =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    directoryResolution.Target!.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            NfsFileHandleTarget createdTarget = new NfsFileHandleTarget(
                directoryResolution.Target.ExportPath,
                createResponse.PathInfo.Path);

            return new MKDIR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new MKDIR3resok
                {
                    obj = await Nfs3DirectoryMutationSupport.CreatePostOperationHandleAsync(
                        _server,
                        createdTarget,
                        cancellationToken).ConfigureAwait(false),
                    obj_attributes = Nfs3DirectoryMutationSupport.CreatePostOperationAttributes(createdTarget, createResponse.PathInfo),
                    dir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryResolution.PathInfo,
                        parentAfterCreate),
                },
            };
        }

        protected override void WriteResult(MKDIR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static MKDIR3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new MKDIR3res
            {
                status = status,
                resfail = new MKDIR3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
