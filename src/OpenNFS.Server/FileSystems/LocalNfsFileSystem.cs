namespace OpenNFS.Server.FileSystems
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Built-in disk-backed <see cref="INfsFileSystem"/> implementation for host-local exports.
    /// </summary>
    public sealed class LocalNfsFileSystem : INfsFileSystem
    {
        /// <summary>
        /// Gets a reusable default instance of the stateless local filesystem implementation.
        /// </summary>
        public static LocalNfsFileSystem Default { get; } = new LocalNfsFileSystem();

        /// <summary>
        /// Resolves information about a host-local source path.
        /// </summary>
        /// <param name="request">Request context for the path-resolution operation.</param>
        /// <returns>Resolved path information for the supplied source path.</returns>
        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsGetPathInfoResponse(GetPathInfo(request.SourcePath)));
        }

        /// <summary>
        /// Resolves a child entry beneath a host-local directory path.
        /// </summary>
        /// <param name="request">Request context for the child-lookup operation.</param>
        /// <returns>Resolved path information for the requested child entry.</returns>
        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.DirectorySourcePath, request.EntryName);
            return Task.FromResult(new NfsLookupPathResponse(GetPathInfo(resolvedSourcePath)));
        }

        /// <summary>
        /// Enumerates child entries beneath a host-local directory path.
        /// </summary>
        /// <param name="request">Request context for the directory-read operation.</param>
        /// <returns>The ordered child entries discovered beneath the requested directory path.</returns>
        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            List<NfsDirectoryEntryInfo> entries = new List<NfsDirectoryEntryInfo>();
            if (!Directory.Exists(request.DirectorySourcePath))
            {
                return Task.FromResult(new NfsReadDirectoryResponse(entries));
            }

            string[] childPaths = Directory.GetFileSystemEntries(request.DirectorySourcePath);
            Array.Sort(childPaths, StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < childPaths.Length; index++)
            {
                string childPath = childPaths[index];
                string childName = Path.GetFileName(childPath);
                entries.Add(new NfsDirectoryEntryInfo(childName, GetPathInfo(childPath)));
            }

            return Task.FromResult(new NfsReadDirectoryResponse(entries));
        }

        /// <summary>
        /// Reads a byte range from a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-read operation.</param>
        /// <returns>The file-read result for the supplied path and byte range.</returns>
        public async Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(request.SourcePath))
            {
                return new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true, found: false);
            }

            using FileStream stream = new FileStream(
                request.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (request.Offset >= (ulong)stream.Length)
            {
                return new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true);
            }

            stream.Position = checked((long)request.Offset);

            int requestedByteCount = request.Count > int.MaxValue ? int.MaxValue : (int)request.Count;
            byte[] buffer = new byte[requestedByteCount];
            int totalBytesRead = 0;

            while (totalBytesRead < buffer.Length)
            {
                int bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(totalBytesRead, buffer.Length - totalBytesRead),
                    request.CancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            byte[] data = buffer;
            if (totalBytesRead != buffer.Length)
            {
                data = new byte[totalBytesRead];
                Array.Copy(buffer, data, totalBytesRead);
            }

            bool endOfFile = stream.Position >= stream.Length;
            return new NfsReadFileResponse(data, endOfFile);
        }

        /// <summary>
        /// Reads the target path from a host-local symbolic link path.
        /// </summary>
        /// <param name="request">Request context for the symbolic-link-read operation.</param>
        /// <returns>The symbolic-link-read result for the supplied path.</returns>
        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsPathInfo pathInfo = GetPathInfo(request.SourcePath);
            if (!pathInfo.Exists || pathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, string.Empty));
            }

            FileSystemInfo fileSystemInfo = CreateFileSystemInfo(request.SourcePath);
            string targetPath = fileSystemInfo.LinkTarget ?? string.Empty;
            return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, targetPath));
        }

        /// <summary>
        /// Writes a byte range to a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-write operation.</param>
        /// <returns>The file-write result for the supplied path, byte range, and stability mode.</returns>
        public async Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
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

            return new NfsWriteFileResponse(GetPathInfo(request.SourcePath), (uint)data.Length, committedStability);
        }

        /// <summary>
        /// Commits previously acknowledged writes for a host-local file path.
        /// </summary>
        /// <param name="request">Request context for the file-commit operation.</param>
        /// <returns>The file-commit result for the supplied path and byte range.</returns>
        public async Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(request.SourcePath))
            {
                return new NfsCommitFileResponse(new NfsPathInfo(request.SourcePath, NfsPathKind.Missing));
            }

            using FileStream stream = new FileStream(
                request.SourcePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);

            await stream.FlushAsync(request.CancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);

            return new NfsCommitFileResponse(GetPathInfo(request.SourcePath));
        }

        /// <summary>
        /// Creates a new host-local filesystem entry beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the create operation.</param>
        /// <returns>The create result for the supplied parent path, entry name, and requested kind.</returns>
        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.ParentDirectorySourcePath, request.EntryName);
            if (File.Exists(resolvedSourcePath) || Directory.Exists(resolvedSourcePath))
            {
                return Task.FromResult(new NfsCreatePathResponse(GetPathInfo(resolvedSourcePath), createdNew: false));
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

            return Task.FromResult(new NfsCreatePathResponse(GetPathInfo(resolvedSourcePath), createdNew: true));
        }

        /// <summary>
        /// Creates a new host-local symbolic link beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the symbolic-link-create operation.</param>
        /// <returns>The create result for the supplied parent path, entry name, and symbolic-link target.</returns>
        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.ParentDirectorySourcePath, request.EntryName);
            if (File.Exists(resolvedSourcePath) || Directory.Exists(resolvedSourcePath))
            {
                return Task.FromResult(new NfsCreateSymbolicLinkResponse(GetPathInfo(resolvedSourcePath), createdNew: false));
            }

            if (Directory.Exists(request.TargetPath))
            {
                Directory.CreateSymbolicLink(resolvedSourcePath, request.TargetPath);
            }
            else
            {
                File.CreateSymbolicLink(resolvedSourcePath, request.TargetPath);
            }

            return Task.FromResult(new NfsCreateSymbolicLinkResponse(GetPathInfo(resolvedSourcePath), createdNew: true));
        }

        /// <summary>
        /// Creates a new host-local hard link beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the hard-link-create operation.</param>
        /// <returns>The create result for the supplied source path, destination parent path, and destination entry name.</returns>
        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("The built-in local filesystem does not currently support hard-link creation.");
        }

        /// <summary>
        /// Deletes a host-local filesystem entry beneath a directory path.
        /// </summary>
        /// <param name="request">Request context for the delete operation.</param>
        /// <returns>The delete result for the supplied parent path, entry name, and expected kind.</returns>
        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
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

            return Task.FromResult(new NfsDeletePathResponse(GetPathInfo(resolvedSourcePath)));
        }

        /// <summary>
        /// Renames or moves a host-local filesystem entry between directory paths.
        /// </summary>
        /// <param name="request">Request context for the rename operation.</param>
        /// <returns>The rename result for the supplied source and destination directory paths and entry names.</returns>
        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
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
                NfsPathInfo currentPathInfo = GetPathInfo(sourceSourcePath);
                return Task.FromResult(new NfsRenamePathResponse(currentPathInfo, currentPathInfo, replacedExistingDestination: false));
            }

            NfsPathInfo sourcePathInfo = GetPathInfo(sourceSourcePath);
            if (!sourcePathInfo.Exists)
            {
                throw new FileNotFoundException("The source path does not exist.", sourceSourcePath);
            }

            NfsPathInfo destinationPathInfo = GetPathInfo(destinationSourcePath);
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
                    GetPathInfo(sourceSourcePath),
                    GetPathInfo(destinationSourcePath),
                    replacedExistingDestination));
        }

        private static NfsPathInfo GetPathInfo(string sourcePath)
        {
            if (TryGetAttributes(sourcePath, out FileAttributes attributes))
            {
                bool isSymbolicLink = (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
                bool isDirectory = (attributes & FileAttributes.Directory) == FileAttributes.Directory;

                if (isSymbolicLink)
                {
                    FileSystemInfo fileSystemInfo = CreateFileSystemInfo(sourcePath, isDirectory);
                    string targetPath = fileSystemInfo.LinkTarget ?? string.Empty;
                    return new NfsPathInfo(
                        sourcePath,
                        NfsPathKind.SymbolicLink,
                        (ulong)Encoding.UTF8.GetByteCount(targetPath),
                        CreateAccessTime(fileSystemInfo),
                        CreateModificationTime(fileSystemInfo),
                        CreateChangeTime(fileSystemInfo));
                }

                if (isDirectory)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(sourcePath);
                    return new NfsPathInfo(
                        sourcePath,
                        NfsPathKind.Directory,
                        0,
                        CreateAccessTime(directoryInfo),
                        CreateModificationTime(directoryInfo),
                        CreateChangeTime(directoryInfo));
                }

                FileInfo fileInfo = new FileInfo(sourcePath);
                return new NfsPathInfo(
                    sourcePath,
                    NfsPathKind.File,
                    (ulong)fileInfo.Length,
                    CreateAccessTime(fileInfo),
                    CreateModificationTime(fileInfo),
                    CreateChangeTime(fileInfo));
            }

            return new NfsPathInfo(sourcePath, NfsPathKind.Missing);
        }

        private static bool TryGetAttributes(string sourcePath, out FileAttributes attributes)
        {
            try
            {
                attributes = File.GetAttributes(sourcePath);
                return true;
            }
            catch (IOException)
            {
                attributes = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                attributes = default;
                return false;
            }
        }

        private static FileSystemInfo CreateFileSystemInfo(string sourcePath)
        {
            bool isDirectory = TryGetAttributes(sourcePath, out FileAttributes attributes)
                && (attributes & FileAttributes.Directory) == FileAttributes.Directory;
            return CreateFileSystemInfo(sourcePath, isDirectory);
        }

        private static FileSystemInfo CreateFileSystemInfo(string sourcePath, bool isDirectory)
        {
            return isDirectory
                ? new DirectoryInfo(sourcePath)
                : new FileInfo(sourcePath);
        }

        private static DateTimeOffset CreateAccessTime(FileSystemInfo fileSystemInfo)
        {
            return NormalizeTimestamp(fileSystemInfo.LastAccessTimeUtc);
        }

        private static DateTimeOffset CreateModificationTime(FileSystemInfo fileSystemInfo)
        {
            return NormalizeTimestamp(fileSystemInfo.LastWriteTimeUtc);
        }

        private static DateTimeOffset CreateChangeTime(FileSystemInfo fileSystemInfo)
        {
            DateTime latestKnownMetadataTimeUtc =
                fileSystemInfo.LastWriteTimeUtc >= fileSystemInfo.CreationTimeUtc
                    ? fileSystemInfo.LastWriteTimeUtc
                    : fileSystemInfo.CreationTimeUtc;

            return NormalizeTimestamp(latestKnownMetadataTimeUtc);
        }

        private static DateTimeOffset NormalizeTimestamp(DateTime timestampUtc)
        {
            DateTime normalizedTimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
                ? timestampUtc
                : timestampUtc.ToUniversalTime();

            if (normalizedTimestampUtc < DateTime.UnixEpoch)
            {
                normalizedTimestampUtc = DateTime.UnixEpoch;
            }

            return new DateTimeOffset(DateTime.SpecifyKind(normalizedTimestampUtc, DateTimeKind.Utc));
        }
    }
}
