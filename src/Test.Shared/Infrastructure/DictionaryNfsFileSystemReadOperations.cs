namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class DictionaryNfsFileSystemReadOperations
    {
        internal static Task<NfsGetPathInfoResponse> GetPathInfoAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsGetPathInfoRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.SourcePath);
            return Task.FromResult(new NfsGetPathInfoResponse(fileSystem.GetPathInfoCore(request.SourcePath)));
        }

        internal static Task<NfsLookupPathResponse> LookupPathAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsLookupPathRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = DictionaryNfsFileSystemBase.CombineSourcePath(request.DirectorySourcePath, request.EntryName);
            fileSystem.TrackPathRequestCore(resolvedSourcePath);
            return Task.FromResult(new NfsLookupPathResponse(fileSystem.GetPathInfoCore(resolvedSourcePath)));
        }

        internal static Task<NfsReadDirectoryResponse> ReadDirectoryAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsReadDirectoryRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.DirectorySourcePath);

            List<NfsDirectoryEntryInfo> entries = new List<NfsDirectoryEntryInfo>();
            string normalizedDirectoryPath = DictionaryNfsFileSystemBase.NormalizePath(request.DirectorySourcePath);

            foreach (KeyValuePair<string, NfsPathKind> pathKind in fileSystem.PathKindsCore)
            {
                string? parentDirectory = DictionaryNfsFileSystemBase.GetParentSourceDirectory(pathKind.Key);
                if (!string.Equals(DictionaryNfsFileSystemBase.NormalizePath(parentDirectory), normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string childName = DictionaryNfsFileSystemBase.GetSourceFileName(pathKind.Key);
                entries.Add(new NfsDirectoryEntryInfo(childName, fileSystem.GetPathInfoCore(pathKind.Key)));
            }

            entries.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            return Task.FromResult(new NfsReadDirectoryResponse(entries));
        }

        internal static Task<NfsReadFileResponse> ReadFileAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsReadFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.SourcePath);

            NfsPathInfo pathInfo = fileSystem.GetPathInfoCore(request.SourcePath);
            if (!pathInfo.Exists)
            {
                return Task.FromResult(new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true, found: false));
            }

            if (!fileSystem.FileContentsCore.TryGetValue(request.SourcePath, out byte[]? fileContents))
            {
                return Task.FromResult(new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true));
            }

            if (request.Offset >= (ulong)fileContents.Length)
            {
                return Task.FromResult(new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true));
            }

            int startOffset = checked((int)request.Offset);
            int remainingByteCount = fileContents.Length - startOffset;
            int requestedByteCount = request.Count > int.MaxValue ? int.MaxValue : (int)request.Count;
            int bytesToRead = Math.Min(remainingByteCount, requestedByteCount);
            byte[] data = new byte[bytesToRead];
            Array.Copy(fileContents, startOffset, data, 0, bytesToRead);
            bool endOfFile = startOffset + bytesToRead >= fileContents.Length;
            return Task.FromResult(new NfsReadFileResponse(data, endOfFile));
        }

        internal static Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsReadSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.SourcePath);

            NfsPathInfo pathInfo = fileSystem.GetPathInfoCore(request.SourcePath);
            if (!pathInfo.Exists || pathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, string.Empty));
            }

            string targetPath = fileSystem.SymbolicLinkTargetsCore.TryGetValue(request.SourcePath, out string? symbolicLinkTarget)
                ? symbolicLinkTarget
                : string.Empty;
            return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, targetPath));
        }

        internal static Task<NfsCommitFileResponse> CommitFileAsync(
            DictionaryNfsFileSystemBase fileSystem,
            NfsCommitFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(fileSystem);
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            fileSystem.TrackPathRequestCore(request.SourcePath);
            return Task.FromResult(new NfsCommitFileResponse(fileSystem.GetPathInfoCore(request.SourcePath)));
        }
    }
}
