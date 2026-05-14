namespace OpenNFS.Server.FileSystems
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class LocalNfsFileSystemReadOperations
    {
        internal static Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new NfsGetPathInfoResponse(LocalNfsFileSystemPathInfoSupport.GetPathInfo(request.SourcePath)));
        }

        internal static Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = Path.Combine(request.DirectorySourcePath, request.EntryName);
            return Task.FromResult(new NfsLookupPathResponse(LocalNfsFileSystemPathInfoSupport.GetPathInfo(resolvedSourcePath)));
        }

        internal static Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
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
                entries.Add(new NfsDirectoryEntryInfo(childName, LocalNfsFileSystemPathInfoSupport.GetPathInfo(childPath)));
            }

            return Task.FromResult(new NfsReadDirectoryResponse(entries));
        }

        internal static async Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
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

        internal static Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsPathInfo pathInfo = LocalNfsFileSystemPathInfoSupport.GetPathInfo(request.SourcePath);
            if (!pathInfo.Exists || pathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, string.Empty));
            }

            FileSystemInfo fileSystemInfo = LocalNfsFileSystemPathInfoSupport.CreateFileSystemInfo(request.SourcePath);
            string targetPath = fileSystemInfo.LinkTarget ?? string.Empty;
            return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, targetPath));
        }

        internal static async Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
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

            return new NfsCommitFileResponse(LocalNfsFileSystemPathInfoSupport.GetPathInfo(request.SourcePath));
        }
    }
}
