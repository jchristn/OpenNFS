namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs3RenameProcedureHandler : Nfs3ProcedureHandlerBase<RENAME3args, RENAME3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3RenameProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_RENAME, RENAME3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<RENAME3res> HandleCoreAsync(RENAME3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution fromDirectoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.from?.dir, cancellationToken).ConfigureAwait(false);
            Nfs3ObjectResolution toDirectoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.to?.dir, cancellationToken).ConfigureAwait(false);

            wcc_data currentFromDirectoryWcc = CreateCurrentDirectoryWcc(fromDirectoryResolution);
            wcc_data currentToDirectoryWcc = CreateCurrentDirectoryWcc(toDirectoryResolution);

            if (fromDirectoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(fromDirectoryResolution.Status, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            if (fromDirectoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            if (toDirectoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(toDirectoryResolution.Status, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            if (toDirectoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            NfsFileHandleTarget fromDirectoryTarget = fromDirectoryResolution.Target!;
            NfsPathInfo fromDirectoryPathInfo = fromDirectoryResolution.PathInfo;
            NfsFileHandleTarget toDirectoryTarget = toDirectoryResolution.Target!;
            NfsPathInfo toDirectoryPathInfo = toDirectoryResolution.PathInfo;

            if (!string.Equals(fromDirectoryTarget.ExportPath, toDirectoryTarget.ExportPath, StringComparison.Ordinal))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_XDEV, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            string fromEntryName = arguments.from?.name?.Value ?? string.Empty;
            string toEntryName = arguments.to?.name?.Value ?? string.Empty;
            if (fromEntryName.Length < 1 || toEntryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            NfsPathInfo fromChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    fromDirectoryTarget,
                    fromEntryName,
                    cancellationToken).ConfigureAwait(false);

            if (!fromChildPathInfo.Exists)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            if (fromChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            string destinationSourcePath = OpenNFS.Server.Internal.NfsSourcePath.Combine(toDirectoryTarget.SourcePath, toEntryName);
            if (fromChildPathInfo.Kind == NfsPathKind.Directory
                && IsPathDescendantOf(destinationSourcePath, fromChildPathInfo.Path))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            NfsPathInfo toChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    toDirectoryTarget,
                    toEntryName,
                    cancellationToken).ConfigureAwait(false);

            if (toChildPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentFromDirectoryWcc, currentToDirectoryWcc);
            }

            bool isSamePath = string.Equals(
                NormalizePath(fromChildPathInfo.Path),
                NormalizePath(destinationSourcePath),
                StringComparison.OrdinalIgnoreCase);
            if (isSamePath)
            {
                return new RENAME3res
                {
                    status = nfsstat3.NFS3_OK,
                    resok = new RENAME3resok
                    {
                        fromdir_wcc = currentFromDirectoryWcc,
                        todir_wcc = currentToDirectoryWcc,
                    },
                };
            }

            if (toChildPathInfo.Exists)
            {
                if (fromChildPathInfo.Kind == NfsPathKind.Directory && toChildPathInfo.Kind != NfsPathKind.Directory)
                {
                    return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentFromDirectoryWcc, currentToDirectoryWcc);
                }

                if (fromChildPathInfo.Kind != NfsPathKind.Directory && toChildPathInfo.Kind == NfsPathKind.Directory)
                {
                    return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, currentFromDirectoryWcc, currentToDirectoryWcc);
                }

                if (toChildPathInfo.Kind == NfsPathKind.Directory)
                {
                    NfsReadDirectoryResponse destinationDirectoryResponse =
                        await _server.Settings.FileSystem.ReadDirectoryAsync(
                            new NfsReadDirectoryRequest(toChildPathInfo.Path, cancellationToken)).ConfigureAwait(false);

                    if (destinationDirectoryResponse.Entries.Count > 0)
                    {
                        return CreateFailureResult(nfsstat3.NFS3ERR_NOTEMPTY, currentFromDirectoryWcc, currentToDirectoryWcc);
                    }
                }
            }

            try
            {
                await _server.Settings.FileSystem.RenamePathAsync(
                    new NfsRenamePathRequest(
                        fromDirectoryTarget.SourcePath,
                        fromEntryName,
                        toDirectoryTarget.SourcePath,
                        toEntryName,
                        fromChildPathInfo.Kind,
                        replaceExistingDestination: toChildPathInfo.Exists,
                        cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                || exception is IOException
                || exception is NotSupportedException
                || exception is DirectoryNotFoundException
                || exception is FileNotFoundException
                || exception is PathTooLongException)
            {
                NfsPathInfo fromDirectoryAfterException =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        fromDirectoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);
                NfsPathInfo toDirectoryAfterException =
                    await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                        _server,
                        toDirectoryTarget.SourcePath,
                        cancellationToken).ConfigureAwait(false);

                return CreateFailureResult(
                    Nfs3DirectoryMutationSupport.MapRenameException(exception),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        fromDirectoryTarget,
                        fromDirectoryPathInfo,
                        fromDirectoryAfterException),
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        toDirectoryTarget,
                        toDirectoryPathInfo,
                        toDirectoryAfterException));
            }

            NfsPathInfo fromDirectoryAfterRename =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    fromDirectoryTarget.SourcePath,
                    cancellationToken).ConfigureAwait(false);
            NfsPathInfo toDirectoryAfterRename =
                await Nfs3DirectoryMutationSupport.GetPathInfoAsync(
                    _server,
                    toDirectoryTarget.SourcePath,
                    cancellationToken).ConfigureAwait(false);

            return new RENAME3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new RENAME3resok
                {
                    fromdir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        fromDirectoryTarget,
                        fromDirectoryPathInfo,
                        fromDirectoryAfterRename),
                    todir_wcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                        toDirectoryTarget,
                        toDirectoryPathInfo,
                        toDirectoryAfterRename),
                },
            };
        }

        protected override void WriteResult(RENAME3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static RENAME3res CreateFailureResult(nfsstat3 status, wcc_data fromDirectoryWcc, wcc_data toDirectoryWcc)
        {
            return new RENAME3res
            {
                status = status,
                resfail = new RENAME3resfail
                {
                    fromdir_wcc = fromDirectoryWcc,
                    todir_wcc = toDirectoryWcc,
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

        private static bool IsPathDescendantOf(string candidatePath, string sourcePath)
        {
            string normalizedCandidatePath = NormalizePath(candidatePath);
            string normalizedSourcePath = NormalizePath(sourcePath);

            if (!normalizedCandidatePath.StartsWith(normalizedSourcePath, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidatePath.Length <= normalizedSourcePath.Length)
            {
                return false;
            }

            char separator = normalizedCandidatePath[normalizedSourcePath.Length];
            return OpenNFS.Server.Internal.NfsSourcePath.IsSeparator(separator);
        }

        private static string NormalizePath(string path)
        {
            return path.TrimEnd('/', '\\');
        }
    }
}
