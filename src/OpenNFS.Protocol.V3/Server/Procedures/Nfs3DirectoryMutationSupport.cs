namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class Nfs3DirectoryMutationSupport
    {
        internal static async Task<NfsPathInfo> GetPathInfoAsync(
            OpenNfsServer server,
            string sourcePath,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            cancellationToken.ThrowIfCancellationRequested();

            NfsGetPathInfoResponse pathInfoResponse =
                await server.Settings.FileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(sourcePath, cancellationToken)).ConfigureAwait(false);

            return pathInfoResponse.PathInfo;
        }

        internal static async Task<NfsPathInfo> LookupChildPathInfoAsync(
            OpenNfsServer server,
            NfsFileHandleTarget directoryTarget,
            string entryName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(directoryTarget);
            ArgumentNullException.ThrowIfNull(entryName);
            cancellationToken.ThrowIfCancellationRequested();

            NfsLookupPathResponse lookupResponse =
                await server.Settings.FileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(
                        directoryTarget.SourcePath,
                        entryName,
                        cancellationToken)).ConfigureAwait(false);

            return lookupResponse.PathInfo;
        }

        internal static async Task<post_op_fh3> CreatePostOperationHandleAsync(
            OpenNfsServer server,
            NfsFileHandleTarget target,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(target);
            cancellationToken.ThrowIfCancellationRequested();

            NfsFileHandle fileHandle =
                await server.CreateFileHandleAsync(target, cancellationToken).ConfigureAwait(false);

            return new post_op_fh3
            {
                handle_follows = true,
                handle = new nfs_fh3
                {
                    data = fileHandle.ToArray(),
                },
            };
        }

        internal static post_op_attr CreatePostOperationAttributes(
            NfsFileHandleTarget target,
            NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(pathInfo);

            return new post_op_attr
            {
                attributes_follow = true,
                attributes = Nfs3MetadataResolver.CreateAttributes(target, pathInfo),
            };
        }

        /// <summary>
        /// Applies the mode requested in a <c>CREATE</c> or <c>MKDIR</c> <c>sattr3</c> to a newly created entry through the
        /// host's attribute-mutation capability. This is best effort: hosts without the capability, or that cannot represent
        /// the mode, keep their default permissions and the create still succeeds.
        /// </summary>
        /// <returns>The refreshed path information when the mode was applied; otherwise <paramref name="createdPathInfo"/>.</returns>
        internal static async Task<NfsPathInfo> ApplyRequestedCreateModeAsync(
            OpenNfsServer server,
            NfsPathInfo createdPathInfo,
            sattr3? requestedAttributes,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(createdPathInfo);

            if (requestedAttributes?.mode?.set_it != true
                || server.Capabilities.AttributeMutation is null
                || !createdPathInfo.Exists)
            {
                return createdPathInfo;
            }

            uint mode = (requestedAttributes.mode.mode?.Value?.Value ?? 0U) & 0xFFFU;
            try
            {
                NfsSetAttributesResponse response = await server.Capabilities.AttributeMutation.SetAttributesAsync(
                    new NfsSetAttributesRequest(
                        createdPathInfo.Path,
                        createdPathInfo.Kind,
                        mode: mode,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                return response?.PathInfo ?? createdPathInfo;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return createdPathInfo;
            }
        }

        internal static nfsstat3 MapCreateException(Exception exception, bool failIfExists)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_NOENT,
                FileNotFoundException => nfsstat3.NFS3ERR_NOENT,
                ArgumentException => nfsstat3.NFS3ERR_INVAL,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                IOException when failIfExists => nfsstat3.NFS3ERR_EXIST,
                IOException => nfsstat3.NFS3ERR_IO,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }

        internal static nfsstat3 MapDeleteException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_NOENT,
                FileNotFoundException => nfsstat3.NFS3ERR_NOENT,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                IOException => nfsstat3.NFS3ERR_IO,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }

        internal static nfsstat3 MapRenameException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_NOENT,
                FileNotFoundException => nfsstat3.NFS3ERR_NOENT,
                ArgumentException => nfsstat3.NFS3ERR_INVAL,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                IOException => nfsstat3.NFS3ERR_IO,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }

        internal static nfsstat3 MapSymbolicLinkException(Exception exception, bool failIfExists)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_NOENT,
                FileNotFoundException => nfsstat3.NFS3ERR_NOENT,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                ArgumentException => nfsstat3.NFS3ERR_INVAL,
                IOException when failIfExists => nfsstat3.NFS3ERR_EXIST,
                IOException => nfsstat3.NFS3ERR_IO,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }

        internal static nfsstat3 MapHardLinkException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                UnauthorizedAccessException => nfsstat3.NFS3ERR_ACCES,
                PathTooLongException => nfsstat3.NFS3ERR_NAMETOOLONG,
                DirectoryNotFoundException => nfsstat3.NFS3ERR_NOENT,
                FileNotFoundException => nfsstat3.NFS3ERR_NOENT,
                NotSupportedException => nfsstat3.NFS3ERR_NOTSUPP,
                IOException => nfsstat3.NFS3ERR_IO,
                _ => nfsstat3.NFS3ERR_SERVERFAULT,
            };
        }
    }
}
