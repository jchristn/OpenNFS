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

    internal sealed class Nfs3SymLinkProcedureHandler : Nfs3ProcedureHandlerBase<SYMLINK3args, SYMLINK3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3SymLinkProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SYMLINK, SYMLINK3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<SYMLINK3res> HandleCoreAsync(SYMLINK3args arguments, CancellationToken cancellationToken)
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

            NfsFileHandleTarget directoryTarget = directoryResolution.Target!;
            NfsPathInfo directoryPathInfo = directoryResolution.PathInfo;

            string entryName = arguments.where?.name?.Value ?? string.Empty;
            string targetPath = arguments.symlink?.symlink_data?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            if (targetPath.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentDirectoryWcc);
            }

            NfsPathInfo existingChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    directoryTarget,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (existingChildPathInfo.Exists)
            {
                return CreateFailureResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_EXIST,
                    currentDirectoryWcc);
            }

            NfsCreateSymbolicLinkResponse createResponse;
            try
            {
                createResponse =
                    await _server.Settings.FileSystem.CreateSymbolicLinkAsync(
                        new NfsCreateSymbolicLinkRequest(
                            directoryTarget.SourcePath,
                            entryName,
                            targetPath,
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
                        directoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    Nfs3DirectoryMutationSupport.MapSymbolicLinkException(exception, failIfExists: true),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryTarget,
                        directoryPathInfo,
                        parentAfterException));
            }

            if (!createResponse.PathInfo.Exists || createResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                NfsPathInfo parentAfterFailure =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        directoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    createResponse.PathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_SERVERFAULT,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryTarget,
                        directoryPathInfo,
                        parentAfterFailure));
            }

            NfsPathInfo parentAfterCreate =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    directoryTarget.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            NfsFileHandleTarget createdTarget = new NfsFileHandleTarget(
                directoryTarget.ExportPath,
                createResponse.PathInfo.Path);

            return new SYMLINK3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new SYMLINK3resok
                {
                    obj = await Nfs3DirectoryMutationSupport.CreatePostOperationHandleAsync(
                        _server,
                        createdTarget,
                        cancellationToken).ConfigureAwait(false),
                    obj_attributes = Nfs3DirectoryMutationSupport.CreatePostOperationAttributes(createdTarget, createResponse.PathInfo),
                    dir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryTarget,
                        directoryPathInfo,
                        parentAfterCreate),
                },
            };
        }

        protected override void WriteResult(SYMLINK3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static SYMLINK3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new SYMLINK3res
            {
                status = status,
                resfail = new SYMLINK3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
