namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class DictionaryNfsFileSystemMutationOperations
    {
        internal static Task<NfsWriteFileResponse> WriteFileAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsWriteFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.SourcePath);
            fileSystem.NotifyWriteRequestCore();

            NfsPathInfo currentPathInfo = fileSystem.GetPathInfoCore(request.SourcePath);
            if (!currentPathInfo.Exists || currentPathInfo.Kind != NfsPathKind.File)
            {
                return Task.FromResult(new NfsWriteFileResponse(currentPathInfo, 0, request.Stability));
            }

            byte[] data = request.Data.ToArray();
            if (data.Length == 0)
            {
                return Task.FromResult(new NfsWriteFileResponse(currentPathInfo, 0, request.Stability));
            }

            byte[] existingFileContents = fileSystem.FileContentsCore.TryGetValue(request.SourcePath, out byte[]? fileContents)
                ? fileContents.AsSpan().ToArray()
                : Array.Empty<byte>();

            int startOffset = checked((int)request.Offset);
            int requiredLength = checked(startOffset + data.Length);
            int finalLength = Math.Max(existingFileContents.Length, requiredLength);
            byte[] updatedFileContents = new byte[finalLength];

            if (existingFileContents.Length > 0)
            {
                Array.Copy(existingFileContents, updatedFileContents, existingFileContents.Length);
            }

            Array.Copy(data, 0, updatedFileContents, startOffset, data.Length);

            fileSystem.FileContentsCore[request.SourcePath] = updatedFileContents;
            fileSystem.PathKindsCore[request.SourcePath] = NfsPathKind.File;
            fileSystem.TouchPathCore(request.SourcePath);

            return Task.FromResult(
                new NfsWriteFileResponse(
                    fileSystem.GetPathInfoCore(request.SourcePath),
                    (uint)data.Length,
                    request.Stability));
        }

        internal static Task<NfsCreatePathResponse> CreatePathAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsCreatePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            fileSystem.TrackPathRequestCore(resolvedSourcePath);

            NfsPathInfo currentPathInfo = fileSystem.GetPathInfoCore(resolvedSourcePath);
            if (currentPathInfo.Exists)
            {
                return Task.FromResult(new NfsCreatePathResponse(currentPathInfo, createdNew: false));
            }

            switch (request.PathKind)
            {
                case NfsPathKind.File:
                    fileSystem.PathKindsCore[resolvedSourcePath] = NfsPathKind.File;
                    fileSystem.FileContentsCore[resolvedSourcePath] = Array.Empty<byte>();
                    break;

                case NfsPathKind.Directory:
                    fileSystem.PathKindsCore[resolvedSourcePath] = NfsPathKind.Directory;
                    break;

                default:
                    throw new NotSupportedException("The " + fileSystem.FileSystemDescriptionCore + " does not support creating path kind '" + request.PathKind.ToString() + "'.");
            }

            fileSystem.TouchPathCore(resolvedSourcePath);
            fileSystem.TouchParentDirectoryCore(resolvedSourcePath);
            return Task.FromResult(new NfsCreatePathResponse(fileSystem.GetPathInfoCore(resolvedSourcePath), createdNew: true));
        }

        internal static Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsCreateSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            fileSystem.TrackPathRequestCore(resolvedSourcePath);

            NfsPathInfo currentPathInfo = fileSystem.GetPathInfoCore(resolvedSourcePath);
            if (currentPathInfo.Exists)
            {
                return Task.FromResult(new NfsCreateSymbolicLinkResponse(currentPathInfo, createdNew: false));
            }

            fileSystem.PathKindsCore[resolvedSourcePath] = NfsPathKind.SymbolicLink;
            fileSystem.SymbolicLinkTargetsCore[resolvedSourcePath] = request.TargetPath;
            fileSystem.TouchPathCore(resolvedSourcePath);
            fileSystem.TouchParentDirectoryCore(resolvedSourcePath);
            return Task.FromResult(new NfsCreateSymbolicLinkResponse(fileSystem.GetPathInfoCore(resolvedSourcePath), createdNew: true));
        }

        internal static Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsCreateHardLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedDestinationPath = DictionaryNfsFileSystemBase.CombineSourcePath(request.DestinationParentDirectorySourcePath, request.DestinationEntryName);
            fileSystem.TrackPathRequestCore(request.SourcePath);
            fileSystem.TrackPathRequestCore(resolvedDestinationPath);

            NfsPathInfo sourcePathInfo = fileSystem.GetPathInfoCore(request.SourcePath);
            if (!sourcePathInfo.Exists)
            {
                throw new FileNotFoundException("The source path does not exist.", request.SourcePath);
            }

            if (sourcePathInfo.Kind != NfsPathKind.File)
            {
                throw new NotSupportedException("The " + fileSystem.FileSystemDescriptionCore + " only supports hard links for regular files.");
            }

            if (fileSystem.GetPathInfoCore(resolvedDestinationPath).Exists)
            {
                throw new IOException("The destination path already exists.");
            }

            byte[] sourceBytes = fileSystem.FileContentsCore.TryGetValue(request.SourcePath, out byte[]? fileContents)
                ? fileContents.AsSpan().ToArray()
                : Array.Empty<byte>();

            fileSystem.PathKindsCore[resolvedDestinationPath] = NfsPathKind.File;
            fileSystem.FileContentsCore[resolvedDestinationPath] = sourceBytes;
            fileSystem.TouchPathCore(request.SourcePath);
            fileSystem.TouchPathCore(resolvedDestinationPath);
            fileSystem.TouchParentDirectoryCore(resolvedDestinationPath);
            return Task.FromResult(
                new NfsCreateHardLinkResponse(
                    fileSystem.GetPathInfoCore(request.SourcePath),
                    fileSystem.GetPathInfoCore(resolvedDestinationPath)));
        }

        internal static Task<NfsDeletePathResponse> DeletePathAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsDeletePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            fileSystem.TrackPathRequestCore(resolvedSourcePath);

            fileSystem.PathTimestampsCore.Remove(resolvedSourcePath);
            fileSystem.PathKindsCore.Remove(resolvedSourcePath);
            fileSystem.FileContentsCore.Remove(resolvedSourcePath);
            fileSystem.SymbolicLinkTargetsCore.Remove(resolvedSourcePath);
            fileSystem.TouchParentDirectoryCore(resolvedSourcePath);

            return Task.FromResult(new NfsDeletePathResponse(fileSystem.GetPathInfoCore(resolvedSourcePath)));
        }

        internal static Task<NfsRenamePathResponse> RenamePathAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsRenamePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string sourceSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.SourceParentDirectorySourcePath, request.SourceEntryName);
            string destinationSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.DestinationParentDirectorySourcePath, request.DestinationEntryName);
            fileSystem.TrackPathRequestCore(sourceSourcePath);
            fileSystem.TrackPathRequestCore(destinationSourcePath);

            if (string.Equals(
                DictionaryNfsFileSystemBase.NormalizePath(sourceSourcePath),
                DictionaryNfsFileSystemBase.NormalizePath(destinationSourcePath),
                StringComparison.OrdinalIgnoreCase))
            {
                NfsPathInfo currentPathInfo = fileSystem.GetPathInfoCore(sourceSourcePath);
                return Task.FromResult(new NfsRenamePathResponse(currentPathInfo, currentPathInfo, replacedExistingDestination: false));
            }

            NfsPathInfo sourcePathInfo = fileSystem.GetPathInfoCore(sourceSourcePath);
            if (!sourcePathInfo.Exists)
            {
                throw new FileNotFoundException("The source path does not exist.", sourceSourcePath);
            }

            NfsPathInfo destinationPathInfo = fileSystem.GetPathInfoCore(destinationSourcePath);
            bool replacedExistingDestination = destinationPathInfo.Exists;

            if (destinationPathInfo.Exists)
            {
                if (!request.ReplaceExistingDestination)
                {
                    throw new IOException("The destination path already exists.");
                }

                if (destinationPathInfo.Kind == NfsPathKind.Directory && fileSystem.HasDirectoryChildrenCore(destinationSourcePath))
                {
                    throw new IOException("The destination directory is not empty.");
                }

                fileSystem.RemovePathAndDescendantsCore(destinationSourcePath);
            }

            fileSystem.MovePathAndDescendantsCore(sourceSourcePath, destinationSourcePath);
            fileSystem.TouchPathCore(destinationSourcePath);
            fileSystem.TouchParentDirectoryCore(sourceSourcePath);
            fileSystem.TouchParentDirectoryCore(destinationSourcePath);

            return Task.FromResult(
                new NfsRenamePathResponse(
                    fileSystem.GetPathInfoCore(sourceSourcePath),
                    fileSystem.GetPathInfoCore(destinationSourcePath),
                    replacedExistingDestination));
        }
    }
}
