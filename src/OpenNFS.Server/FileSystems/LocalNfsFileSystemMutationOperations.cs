namespace OpenNFS.Server.FileSystems
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class LocalNfsFileSystemMutationOperations
    {
        internal static async Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(request.SourcePath))
            {
                return new NfsWriteFileResponse(new NfsPathInfo(request.SourcePath, NfsPathKind.Missing), 0, request.Stability);
            }

            byte[] data = request.Data.ToArray();
            NfsWriteStability committedStability = request.Stability;

            using FileStream stream = new FileStream(
                request.SourcePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);

            stream.Position = checked((long)request.Offset);

            if (data.Length > 0)
            {
                await stream.WriteAsync(data.AsMemory(), request.CancellationToken).ConfigureAwait(false);
            }

            switch (request.Stability)
            {
                case NfsWriteStability.Unstable:
                    break;

                case NfsWriteStability.DataSync:
                    await stream.FlushAsync(request.CancellationToken).ConfigureAwait(false);
                    break;

                default:
                    await stream.FlushAsync(request.CancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                    break;
            }

            return new NfsWriteFileResponse(
                LocalNfsFileSystemPathInfoSupport.GetPathInfo(request.SourcePath),
                (uint)data.Length,
                committedStability);
        }

        internal static Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.ParentDirectorySourcePath, request.EntryName);
            if (File.Exists(resolvedSourcePath) || Directory.Exists(resolvedSourcePath))
            {
                return Task.FromResult(
                    new NfsCreatePathResponse(
                        LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath),
                        createdNew: false));
            }

            switch (request.PathKind)
            {
                case NfsPathKind.File:
                    using (FileStream stream = new FileStream(
                        resolvedSourcePath,
                        request.FailIfExists ? FileMode.CreateNew : FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete))
                    {
                    }

                    break;

                case NfsPathKind.Directory:
                    Directory.CreateDirectory(resolvedSourcePath);
                    break;

                default:
                    throw new NotSupportedException("The local filesystem does not support creating path kind '" + request.PathKind.ToString() + "'.");
            }

            return Task.FromResult(
                new NfsCreatePathResponse(
                    LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath),
                    createdNew: true));
        }

        internal static Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.ParentDirectorySourcePath, request.EntryName);
            if (File.Exists(resolvedSourcePath) || Directory.Exists(resolvedSourcePath))
            {
                return Task.FromResult(
                    new NfsCreateSymbolicLinkResponse(
                        LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath),
                        createdNew: false));
            }

            if (Directory.Exists(request.TargetPath))
            {
                Directory.CreateSymbolicLink(resolvedSourcePath, request.TargetPath);
            }
            else
            {
                File.CreateSymbolicLink(resolvedSourcePath, request.TargetPath);
            }

            return Task.FromResult(
                new NfsCreateSymbolicLinkResponse(
                    LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath),
                    createdNew: true));
        }

        internal static Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("The built-in local filesystem does not currently support hard-link creation.");
        }

        internal static Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.ParentDirectorySourcePath, request.EntryName);
            switch (request.PathKind)
            {
                case NfsPathKind.File:
                case NfsPathKind.SymbolicLink:
                    if (File.Exists(resolvedSourcePath))
                    {
                        File.Delete(resolvedSourcePath);
                    }

                    break;

                case NfsPathKind.Directory:
                    if (Directory.Exists(resolvedSourcePath))
                    {
                        Directory.Delete(resolvedSourcePath);
                    }

                    break;

                default:
                    throw new NotSupportedException("The local filesystem does not support deleting path kind '" + request.PathKind.ToString() + "'.");
            }

            return Task.FromResult(new NfsDeletePathResponse(LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath)));
        }

        internal static Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string sourceSourcePath = Path.Combine(request.SourceParentDirectorySourcePath, request.SourceEntryName);
            string destinationSourcePath = Path.Combine(request.DestinationParentDirectorySourcePath, request.DestinationEntryName);

            if (string.Equals(
                sourceSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                destinationSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                NfsPathInfo currentPathInfo = LocalNfsFileSystemPathInfoSupport.GetPathInfo(sourceSourcePath);
                return Task.FromResult(new NfsRenamePathResponse(currentPathInfo, currentPathInfo, replacedExistingDestination: false));
            }

            NfsPathInfo sourcePathInfo = LocalNfsFileSystemPathInfoSupport.GetPathInfo(sourceSourcePath);
            if (!sourcePathInfo.Exists)
            {
                throw new FileNotFoundException("The source path does not exist.", sourceSourcePath);
            }

            NfsPathInfo destinationPathInfo = LocalNfsFileSystemPathInfoSupport.GetPathInfo(destinationSourcePath);
            bool replacedExistingDestination = destinationPathInfo.Exists;

            switch (request.SourcePathKind)
            {
                case NfsPathKind.File:
                case NfsPathKind.SymbolicLink:
                    File.Move(sourceSourcePath, destinationSourcePath, request.ReplaceExistingDestination);
                    break;

                case NfsPathKind.Directory:
                    if (Directory.Exists(destinationSourcePath))
                    {
                        if (!request.ReplaceExistingDestination)
                        {
                            throw new IOException("The destination directory already exists.");
                        }

                        if (Directory.GetFileSystemEntries(destinationSourcePath).Length > 0)
                        {
                            throw new IOException("The destination directory is not empty.");
                        }

                        Directory.Delete(destinationSourcePath);
                    }

                    Directory.Move(sourceSourcePath, destinationSourcePath);
                    break;

                default:
                    throw new NotSupportedException("The local filesystem does not support renaming path kind '" + request.SourcePathKind.ToString() + "'.");
            }

            return Task.FromResult(
                new NfsRenamePathResponse(
                    LocalNfsFileSystemPathInfoSupport.GetPathInfo(sourceSourcePath),
                    LocalNfsFileSystemPathInfoSupport.GetPathInfo(destinationSourcePath),
                    replacedExistingDestination));
        }
    }
}
