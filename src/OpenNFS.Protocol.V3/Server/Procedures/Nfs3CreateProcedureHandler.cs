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

    internal sealed class Nfs3CreateProcedureHandler : Nfs3ProcedureHandlerBase<CREATE3args, CREATE3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3CreateProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_CREATE, CREATE3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<CREATE3res> HandleCoreAsync(CREATE3args arguments, CancellationToken cancellationToken)
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
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            createmode3 createMode = arguments.how?.mode ?? createmode3.UNCHECKED;
            bool failIfExists = createMode != createmode3.UNCHECKED;

            NfsPathInfo existingChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    directoryTarget,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (existingChildPathInfo.Exists)
            {
                if (existingChildPathInfo.Kind == NfsPathKind.Other)
                {
                    return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentDirectoryWcc);
                }

                if (createMode == createmode3.UNCHECKED && existingChildPathInfo.Kind == NfsPathKind.File)
                {
                    NfsFileHandleTarget existingTarget = new NfsFileHandleTarget(
                        directoryTarget.ExportPath,
                        existingChildPathInfo.Path);

                    return new CREATE3res
                    {
                        status = nfsstat3.NFS3_OK,
                        resok = new CREATE3resok
                        {
                            obj = await Nfs3DirectoryMutationSupport.CreatePostOperationHandleAsync(
                                _server,
                                existingTarget,
                                cancellationToken).ConfigureAwait(false),
                            obj_attributes = Nfs3DirectoryMutationSupport.CreatePostOperationAttributes(existingTarget, existingChildPathInfo),
                            dir_wcc = currentDirectoryWcc,
                        },
                    };
                }

                return CreateFailureResult(nfsstat3.NFS3ERR_EXIST, currentDirectoryWcc);
            }

            NfsCreatePathResponse createResponse;
            try
            {
                createResponse =
                    await _server.Settings.FileSystem.CreatePathAsync(
                        new NfsCreatePathRequest(
                            directoryTarget.SourcePath,
                            entryName,
                            NfsPathKind.File,
                            failIfExists,
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
                    Nfs3DirectoryMutationSupport.MapCreateException(exception, failIfExists),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryPathInfo,
                        parentAfterException));
            }

            if (!createResponse.PathInfo.Exists)
            {
                NfsPathInfo parentAfterFailure =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        directoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    nfsstat3.NFS3ERR_SERVERFAULT,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryResolution.Target,
                        directoryPathInfo,
                        parentAfterFailure));
            }

            if (createResponse.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentDirectoryWcc);
            }

            if (createResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentDirectoryWcc);
            }

            NfsPathInfo createdPathInfo = createResponse.CreatedNew && createMode != createmode3.EXCLUSIVE
                ? await Nfs3DirectoryMutationSupport.ApplyRequestedCreateModeAsync(
                    _server,
                    createResponse.PathInfo,
                    arguments.how?.obj_attributes,
                    cancellationToken).ConfigureAwait(false)
                : createResponse.PathInfo;

            NfsPathInfo parentAfterCreate =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    directoryTarget.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            NfsFileHandleTarget createdTarget = new NfsFileHandleTarget(
                directoryTarget.ExportPath,
                createdPathInfo.Path);

            return new CREATE3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new CREATE3resok
                {
                    obj = await Nfs3DirectoryMutationSupport.CreatePostOperationHandleAsync(
                        _server,
                        createdTarget,
                        cancellationToken).ConfigureAwait(false),
                    obj_attributes = Nfs3DirectoryMutationSupport.CreatePostOperationAttributes(createdTarget, createdPathInfo),
                    dir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        directoryTarget,
                        directoryPathInfo,
                        parentAfterCreate),
                },
            };
        }

        protected override void WriteResult(CREATE3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static CREATE3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new CREATE3res
            {
                status = status,
                resfail = new CREATE3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
