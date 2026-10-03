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

    internal sealed class Nfs3LinkProcedureHandler : Nfs3ProcedureHandlerBase<LINK3args, LINK3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3LinkProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LINK, LINK3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<LINK3res> HandleCoreAsync(LINK3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution fileResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.file, cancellationToken).ConfigureAwait(false);
            Nfs3ObjectResolution linkDirectoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.link?.dir, cancellationToken).ConfigureAwait(false);

            post_op_attr fileAttributes = fileResolution.PostOperationAttributes;
            wcc_data currentLinkDirectoryWcc = CreateCurrentDirectoryWcc(linkDirectoryResolution);

            if (fileResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(fileResolution.Status, fileAttributes, currentLinkDirectoryWcc);
            }

            if (fileResolution.PathInfo!.Kind == NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, fileAttributes, currentLinkDirectoryWcc);
            }

            if (fileResolution.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, fileAttributes, currentLinkDirectoryWcc);
            }

            if (linkDirectoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(linkDirectoryResolution.Status, fileAttributes, currentLinkDirectoryWcc);
            }

            if (linkDirectoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, fileAttributes, currentLinkDirectoryWcc);
            }

            NfsFileHandleTarget fileTarget = fileResolution.Target!;
            NfsFileHandleTarget linkDirectoryTarget = linkDirectoryResolution.Target!;
            if (!string.Equals(fileTarget.ExportPath, linkDirectoryTarget.ExportPath, StringComparison.Ordinal))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_XDEV, fileAttributes, currentLinkDirectoryWcc);
            }

            string entryName = arguments.link?.name?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, fileAttributes, currentLinkDirectoryWcc);
            }

            NfsPathInfo existingChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    linkDirectoryTarget,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (existingChildPathInfo.Exists)
            {
                return CreateFailureResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_EXIST,
                    fileAttributes,
                    currentLinkDirectoryWcc);
            }

            NfsCreateHardLinkResponse createResponse;
            try
            {
                createResponse =
                    await _server.Settings.InstrumentedFileSystem.CreateHardLinkAsync(
                        new NfsCreateHardLinkRequest(
                            fileTarget.SourcePath,
                            linkDirectoryTarget.SourcePath,
                            entryName,
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
                NfsPathInfo linkDirectoryAfterException =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        linkDirectoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    Nfs3DirectoryMutationSupport.MapHardLinkException(exception),
                    fileAttributes,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        linkDirectoryTarget,
                        linkDirectoryResolution.PathInfo,
                        linkDirectoryAfterException));
            }

            if (!createResponse.SourcePathInfo.Exists || !createResponse.LinkPathInfo.Exists)
            {
                NfsPathInfo linkDirectoryAfterFailure =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        linkDirectoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    nfsstat3.NFS3ERR_SERVERFAULT,
                    fileAttributes,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        linkDirectoryTarget,
                        linkDirectoryResolution.PathInfo,
                        linkDirectoryAfterFailure));
            }

            post_op_attr updatedFileAttributes = Nfs3MetadataResolver.CreatePostOperationAttributes(fileTarget, createResponse.SourcePathInfo);
            NfsPathInfo linkDirectoryAfterCreate =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    linkDirectoryTarget.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            return new LINK3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new LINK3resok
                {
                    file_attributes = updatedFileAttributes,
                    linkdir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        linkDirectoryTarget,
                        linkDirectoryResolution.PathInfo,
                        linkDirectoryAfterCreate),
                },
            };
        }

        protected override void WriteResult(LINK3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static LINK3res CreateFailureResult(nfsstat3 status, post_op_attr fileAttributes, wcc_data linkDirectoryWcc)
        {
            return new LINK3res
            {
                status = status,
                resfail = new LINK3resfail
                {
                    file_attributes = fileAttributes,
                    linkdir_wcc = linkDirectoryWcc,
                },
            };
        }

        private static wcc_data CreateCurrentDirectoryWcc(Nfs3ObjectResolution resolution)
        {
            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null);
            }

            return Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                resolution.Target,
                resolution.PathInfo,
                resolution.PathInfo);
        }
    }
}
