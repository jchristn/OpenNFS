namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class CapabilityAwareDictionaryNfsFileSystem : INfsFileSystem, INfsLocking, INfsDelegations, INfsSparse
    {
        private static readonly DateTimeOffset TimestampBaseUtc = new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _FileContents;
        private readonly InMemoryNfsLockManager _LockManager;
        private readonly Dictionary<string, NfsPathKind> _PathKinds;
        private readonly Dictionary<string, PathTimestamps> _PathTimestamps;
        private readonly Dictionary<string, string> _SymbolicLinkTargets;
        private long _nextTimestampTicks;

        internal CapabilityAwareDictionaryNfsFileSystem(IReadOnlyDictionary<string, NfsPathKind> pathKinds)
        {
            ArgumentNullException.ThrowIfNull(pathKinds);
            _FileContents = new Dictionary<string, byte[]>(SeparatorAgnosticPathComparer.Instance);
            _LockManager = new InMemoryNfsLockManager();
            _PathKinds = new Dictionary<string, NfsPathKind>(pathKinds, SeparatorAgnosticPathComparer.Instance);
            _PathTimestamps = new Dictionary<string, PathTimestamps>(SeparatorAgnosticPathComparer.Instance);
            _SymbolicLinkTargets = new Dictionary<string, string>(SeparatorAgnosticPathComparer.Instance);
            _nextTimestampTicks = TimestampBaseUtc.UtcTicks;

            List<string> initialPaths = new List<string>(_PathKinds.Keys);
            initialPaths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string initialPath in initialPaths)
            {
                EnsureTimestampForExistingPath(initialPath);
            }
        }

        public Task<NfsLockResponse> ProcessLockAsync(NfsLockRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return _LockManager.ProcessAsync(request);
        }

        // INfsSparse implementations: this fixture exists only to verify capability discovery wires
        // through to the protocol layer. Real sparse semantics live in the production sample provider.
        ValueTask<NfsSeekResponse> INfsSparse.SeekAsync(NfsSeekRequest request)
        {
            return ValueTask.FromResult(new NfsSeekResponse(request.Offset, !request.SearchForData));
        }

        ValueTask<NfsAllocateResponse> INfsSparse.AllocateAsync(NfsAllocateRequest request)
        {
            return ValueTask.FromResult(NfsAllocateResponse.Success);
        }

        ValueTask<NfsDeallocateResponse> INfsSparse.DeallocateAsync(NfsDeallocateRequest request)
        {
            return ValueTask.FromResult(NfsDeallocateResponse.Success);
        }

        ValueTask<NfsReadSparseResponse> INfsSparse.ReadSparseAsync(NfsReadSparseRequest request)
        {
            byte[] payload = new byte[request.Count];
            return ValueTask.FromResult(
                new NfsReadSparseResponse(
                    new[] { NfsSparseExtent.ForData(request.Offset, payload) },
                    endOfFile: false));
        }

        public Task<NfsAcquireDelegationResponse> AcquireDelegationAsync(NfsAcquireDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NfsAcquireDelegationResponse.None);
        }

        public Task RecallDelegationAsync(NfsRecallDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task ReturnDelegationAsync(NfsReturnDelegationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new NfsGetPathInfoResponse(GetPathInfo(request.SourcePath)));
        }

        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = CombineSourcePath(request.DirectorySourcePath, request.EntryName);
            return Task.FromResult(new NfsLookupPathResponse(GetPathInfo(resolvedSourcePath)));
        }

        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            List<NfsDirectoryEntryInfo> entries = new List<NfsDirectoryEntryInfo>();
            string normalizedDirectoryPath = NormalizePath(request.DirectorySourcePath);

            foreach (KeyValuePair<string, NfsPathKind> pathKind in _PathKinds)
            {
                string? parentDirectory = GetParentSourceDirectory(pathKind.Key);
                if (!string.Equals(NormalizePath(parentDirectory), normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string childName = GetSourceFileName(pathKind.Key);
                entries.Add(new NfsDirectoryEntryInfo(childName, GetPathInfo(pathKind.Key)));
            }

            entries.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            return Task.FromResult(new NfsReadDirectoryResponse(entries));
        }

        public Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsPathInfo pathInfo = GetPathInfo(request.SourcePath);
            if (!pathInfo.Exists)
            {
                return Task.FromResult(new NfsReadFileResponse(Array.Empty<byte>(), endOfFile: true, found: false));
            }

            if (!_FileContents.TryGetValue(request.SourcePath, out byte[]? fileContents))
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

        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsPathInfo pathInfo = GetPathInfo(request.SourcePath);
            if (!pathInfo.Exists || pathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, string.Empty));
            }

            string targetPath = _SymbolicLinkTargets.TryGetValue(request.SourcePath, out string? symbolicLinkTarget)
                ? symbolicLinkTarget
                : string.Empty;
            return Task.FromResult(new NfsReadSymbolicLinkResponse(pathInfo, targetPath));
        }

        public Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            NfsPathInfo currentPathInfo = GetPathInfo(request.SourcePath);
            if (!currentPathInfo.Exists || currentPathInfo.Kind != NfsPathKind.File)
            {
                return Task.FromResult(new NfsWriteFileResponse(currentPathInfo, 0, request.Stability));
            }

            byte[] data = request.Data.ToArray();
            if (data.Length == 0)
            {
                return Task.FromResult(new NfsWriteFileResponse(currentPathInfo, 0, request.Stability));
            }

            byte[] existingFileContents = _FileContents.TryGetValue(request.SourcePath, out byte[]? fileContents)
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

            _FileContents[request.SourcePath] = updatedFileContents;
            _PathKinds[request.SourcePath] = NfsPathKind.File;
            TouchPath(request.SourcePath);

            return Task.FromResult(
                new NfsWriteFileResponse(
                    GetPathInfo(request.SourcePath),
                    (uint)data.Length,
                    request.Stability));
        }

        public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new NfsCommitFileResponse(GetPathInfo(request.SourcePath)));
        }

        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            NfsPathInfo currentPathInfo = GetPathInfo(resolvedSourcePath);
            if (currentPathInfo.Exists)
            {
                return Task.FromResult(new NfsCreatePathResponse(currentPathInfo, createdNew: false));
            }

            switch (request.PathKind)
            {
                case NfsPathKind.File:
                    _PathKinds[resolvedSourcePath] = NfsPathKind.File;
                    _FileContents[resolvedSourcePath] = Array.Empty<byte>();
                    break;

                case NfsPathKind.Directory:
                    _PathKinds[resolvedSourcePath] = NfsPathKind.Directory;
                    break;

                default:
                    throw new NotSupportedException("The capability-aware in-memory filesystem does not support creating path kind '" + request.PathKind.ToString() + "'.");
            }

            TouchPath(resolvedSourcePath);
            TouchParentDirectory(resolvedSourcePath);
            return Task.FromResult(new NfsCreatePathResponse(GetPathInfo(resolvedSourcePath), createdNew: true));
        }

        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            NfsPathInfo currentPathInfo = GetPathInfo(resolvedSourcePath);
            if (currentPathInfo.Exists)
            {
                return Task.FromResult(new NfsCreateSymbolicLinkResponse(currentPathInfo, createdNew: false));
            }

            _PathKinds[resolvedSourcePath] = NfsPathKind.SymbolicLink;
            _SymbolicLinkTargets[resolvedSourcePath] = request.TargetPath;
            TouchPath(resolvedSourcePath);
            TouchParentDirectory(resolvedSourcePath);
            return Task.FromResult(new NfsCreateSymbolicLinkResponse(GetPathInfo(resolvedSourcePath), createdNew: true));
        }

        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedDestinationPath = CombineSourcePath(request.DestinationParentDirectorySourcePath, request.DestinationEntryName);
            NfsPathInfo sourcePathInfo = GetPathInfo(request.SourcePath);
            if (!sourcePathInfo.Exists)
            {
                throw new FileNotFoundException("The source path does not exist.", request.SourcePath);
            }

            if (sourcePathInfo.Kind != NfsPathKind.File)
            {
                throw new NotSupportedException("The capability-aware in-memory filesystem only supports hard links for regular files.");
            }

            if (GetPathInfo(resolvedDestinationPath).Exists)
            {
                throw new IOException("The destination path already exists.");
            }

            byte[] sourceBytes = _FileContents.TryGetValue(request.SourcePath, out byte[]? fileContents)
                ? fileContents.AsSpan().ToArray()
                : Array.Empty<byte>();

            _PathKinds[resolvedDestinationPath] = NfsPathKind.File;
            _FileContents[resolvedDestinationPath] = sourceBytes;
            TouchPath(request.SourcePath);
            TouchPath(resolvedDestinationPath);
            TouchParentDirectory(resolvedDestinationPath);
            return Task.FromResult(new NfsCreateHardLinkResponse(GetPathInfo(request.SourcePath), GetPathInfo(resolvedDestinationPath)));
        }

        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string resolvedSourcePath = CombineSourcePath(request.ParentDirectorySourcePath, request.EntryName);
            _PathTimestamps.Remove(resolvedSourcePath);
            _PathKinds.Remove(resolvedSourcePath);
            _FileContents.Remove(resolvedSourcePath);
            _SymbolicLinkTargets.Remove(resolvedSourcePath);
            TouchParentDirectory(resolvedSourcePath);
            return Task.FromResult(new NfsDeletePathResponse(GetPathInfo(resolvedSourcePath)));
        }

        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.CancellationToken.ThrowIfCancellationRequested();

            string sourceSourcePath = CombineSourcePath(request.SourceParentDirectorySourcePath, request.SourceEntryName);
            string destinationSourcePath = CombineSourcePath(request.DestinationParentDirectorySourcePath, request.DestinationEntryName);

            if (string.Equals(
                NormalizePath(sourceSourcePath),
                NormalizePath(destinationSourcePath),
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

            if (destinationPathInfo.Exists)
            {
                if (!request.ReplaceExistingDestination)
                {
                    throw new IOException("The destination path already exists.");
                }

                if (destinationPathInfo.Kind == NfsPathKind.Directory && HasDirectoryChildren(destinationSourcePath))
                {
                    throw new IOException("The destination directory is not empty.");
                }

                RemovePathAndDescendants(destinationSourcePath);
            }

            MovePathAndDescendants(sourceSourcePath, destinationSourcePath);
            TouchPath(destinationSourcePath);
            TouchParentDirectory(sourceSourcePath);
            TouchParentDirectory(destinationSourcePath);

            return Task.FromResult(
                new NfsRenamePathResponse(
                    GetPathInfo(sourceSourcePath),
                    GetPathInfo(destinationSourcePath),
                    replacedExistingDestination));
        }

        private NfsPathInfo GetPathInfo(string sourcePath)
        {
            NfsPathKind pathKind = NfsPathKind.Missing;
            if (_PathKinds.TryGetValue(sourcePath, out NfsPathKind resolvedPathKind))
            {
                pathKind = resolvedPathKind;
            }

            ulong length = 0;
            if (_FileContents.TryGetValue(sourcePath, out byte[]? fileContents))
            {
                length = (ulong)fileContents.LongLength;
            }
            else if (_SymbolicLinkTargets.TryGetValue(sourcePath, out string? symbolicLinkTarget))
            {
                length = (ulong)Encoding.UTF8.GetByteCount(symbolicLinkTarget);
            }

            if (pathKind == NfsPathKind.Missing)
            {
                return new NfsPathInfo(sourcePath, pathKind, length);
            }

            if (!_PathTimestamps.TryGetValue(sourcePath, out PathTimestamps timestamps))
            {
                timestamps = CreateNextTimestamps();
                _PathTimestamps[sourcePath] = timestamps;
            }

            return new NfsPathInfo(
                sourcePath,
                pathKind,
                length,
                timestamps.AccessTimeUtc,
                timestamps.ModificationTimeUtc,
                timestamps.ChangeTimeUtc);
        }

        private bool HasDirectoryChildren(string sourcePath)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);

            foreach (string candidatePath in _PathKinds.Keys)
            {
                string? parentDirectory = GetParentSourceDirectory(candidatePath);
                if (string.Equals(NormalizePath(parentDirectory), normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void MovePathAndDescendants(string sourceSourcePath, string destinationSourcePath)
        {
            List<KeyValuePair<string, NfsPathKind>> pathKindsToMove = new List<KeyValuePair<string, NfsPathKind>>();
            foreach (KeyValuePair<string, NfsPathKind> pathKind in _PathKinds)
            {
                if (IsSamePathOrDescendant(pathKind.Key, sourceSourcePath))
                {
                    pathKindsToMove.Add(pathKind);
                }
            }

            List<KeyValuePair<string, byte[]>> fileContentsToMove = new List<KeyValuePair<string, byte[]>>();
            foreach (KeyValuePair<string, byte[]> fileContent in _FileContents)
            {
                if (IsSamePathOrDescendant(fileContent.Key, sourceSourcePath))
                {
                    fileContentsToMove.Add(fileContent);
                }
            }

            foreach (KeyValuePair<string, NfsPathKind> pathKind in pathKindsToMove)
            {
                _PathKinds.Remove(pathKind.Key);
            }

            foreach (KeyValuePair<string, byte[]> fileContent in fileContentsToMove)
            {
                _FileContents.Remove(fileContent.Key);
            }

            foreach (KeyValuePair<string, NfsPathKind> pathKind in pathKindsToMove)
            {
                _PathKinds[RewritePath(pathKind.Key, sourceSourcePath, destinationSourcePath)] = pathKind.Value;
            }

            foreach (KeyValuePair<string, byte[]> fileContent in fileContentsToMove)
            {
                _FileContents[RewritePath(fileContent.Key, sourceSourcePath, destinationSourcePath)] = fileContent.Value.AsSpan().ToArray();
            }

            List<KeyValuePair<string, PathTimestamps>> pathTimestampsToMove = new List<KeyValuePair<string, PathTimestamps>>();
            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in _PathTimestamps)
            {
                if (IsSamePathOrDescendant(pathTimestamp.Key, sourceSourcePath))
                {
                    pathTimestampsToMove.Add(pathTimestamp);
                }
            }

            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in pathTimestampsToMove)
            {
                _PathTimestamps.Remove(pathTimestamp.Key);
            }

            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in pathTimestampsToMove)
            {
                _PathTimestamps[RewritePath(pathTimestamp.Key, sourceSourcePath, destinationSourcePath)] = pathTimestamp.Value;
            }

            List<KeyValuePair<string, string>> symbolicLinkTargetsToMove = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> symbolicLinkTarget in _SymbolicLinkTargets)
            {
                if (IsSamePathOrDescendant(symbolicLinkTarget.Key, sourceSourcePath))
                {
                    symbolicLinkTargetsToMove.Add(symbolicLinkTarget);
                }
            }

            foreach (KeyValuePair<string, string> symbolicLinkTarget in symbolicLinkTargetsToMove)
            {
                _SymbolicLinkTargets.Remove(symbolicLinkTarget.Key);
            }

            foreach (KeyValuePair<string, string> symbolicLinkTarget in symbolicLinkTargetsToMove)
            {
                _SymbolicLinkTargets[RewritePath(symbolicLinkTarget.Key, sourceSourcePath, destinationSourcePath)] = symbolicLinkTarget.Value;
            }
        }

        private void RemovePathAndDescendants(string sourcePath)
        {
            List<string> pathKindsToRemove = new List<string>();
            foreach (string candidatePath in _PathKinds.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    pathKindsToRemove.Add(candidatePath);
                }
            }

            foreach (string pathKindToRemove in pathKindsToRemove)
            {
                _PathKinds.Remove(pathKindToRemove);
            }

            List<string> fileContentsToRemove = new List<string>();
            foreach (string candidatePath in _FileContents.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    fileContentsToRemove.Add(candidatePath);
                }
            }

            foreach (string fileContentToRemove in fileContentsToRemove)
            {
                _FileContents.Remove(fileContentToRemove);
            }

            List<string> pathTimestampsToRemove = new List<string>();
            foreach (string candidatePath in _PathTimestamps.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    pathTimestampsToRemove.Add(candidatePath);
                }
            }

            foreach (string pathTimestampToRemove in pathTimestampsToRemove)
            {
                _PathTimestamps.Remove(pathTimestampToRemove);
            }

            List<string> symbolicLinkTargetsToRemove = new List<string>();
            foreach (string candidatePath in _SymbolicLinkTargets.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    symbolicLinkTargetsToRemove.Add(candidatePath);
                }
            }

            foreach (string symbolicLinkTargetToRemove in symbolicLinkTargetsToRemove)
            {
                _SymbolicLinkTargets.Remove(symbolicLinkTargetToRemove);
            }
        }

        private void EnsureTimestampForExistingPath(string sourcePath)
        {
            if (!_PathKinds.ContainsKey(sourcePath) || _PathTimestamps.ContainsKey(sourcePath))
            {
                return;
            }

            _PathTimestamps[sourcePath] = CreateNextTimestamps();
        }

        private void TouchPath(string sourcePath)
        {
            if (!_PathKinds.ContainsKey(sourcePath))
            {
                return;
            }

            _PathTimestamps[sourcePath] = CreateNextTimestamps();
        }

        private void TouchParentDirectory(string sourcePath)
        {
            string? parentDirectory = GetParentSourceDirectory(sourcePath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                return;
            }

            if (_PathKinds.TryGetValue(parentDirectory, out NfsPathKind parentPathKind)
                && parentPathKind == NfsPathKind.Directory)
            {
                TouchPath(parentDirectory);
            }
        }

        private PathTimestamps CreateNextTimestamps()
        {
            DateTimeOffset timestampUtc = new DateTimeOffset(_nextTimestampTicks, TimeSpan.Zero);
            _nextTimestampTicks += TimeSpan.TicksPerSecond;
            return new PathTimestamps(timestampUtc, timestampUtc, timestampUtc);
        }

        private static bool IsSamePathOrDescendant(string candidatePath, string sourcePath)
        {
            string normalizedCandidatePath = NormalizePath(candidatePath);
            string normalizedSourcePath = NormalizePath(sourcePath);

            if (string.Equals(normalizedCandidatePath, normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!normalizedCandidatePath.StartsWith(normalizedSourcePath, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidatePath.Length <= normalizedSourcePath.Length)
            {
                return false;
            }

            char separator = normalizedCandidatePath[normalizedSourcePath.Length];
            return IsSourcePathSeparator(separator);
        }

        private static string RewritePath(string candidatePath, string sourcePath, string destinationPath)
        {
            if (string.Equals(NormalizePath(candidatePath), NormalizePath(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            return destinationPath + candidatePath.Substring(sourcePath.Length);
        }

        private static string NormalizePath(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            // Canonicalize the separator so '\' and '/' compare equal — server-side path resolution
            // on Linux can produce mixed-separator strings when an export's source path uses Windows
            // syntax, and we still need to match those paths against the dictionary entries.
            return sourcePath.TrimEnd('/', '\\').Replace('\\', '/');
        }

        private sealed class SeparatorAgnosticPathComparer : IEqualityComparer<string>
        {
            internal static SeparatorAgnosticPathComparer Instance { get; } = new SeparatorAgnosticPathComparer();

            public bool Equals(string? x, string? y)
            {
                if (x is null)
                {
                    return y is null;
                }

                if (y is null)
                {
                    return false;
                }

                return string.Equals(Canonicalize(x), Canonicalize(y), StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return Canonicalize(obj).GetHashCode(StringComparison.OrdinalIgnoreCase);
            }

            private static string Canonicalize(string value)
            {
                return value.Replace('\\', '/');
            }
        }

        private static bool IsSourcePathSeparator(char value)
        {
            return value == '/' || value == '\\';
        }

        private static string CombineSourcePath(string parentDirectory, string entryName)
        {
            ArgumentNullException.ThrowIfNull(parentDirectory);
            ArgumentNullException.ThrowIfNull(entryName);

            string trimmedParent = parentDirectory.TrimEnd('/', '\\');
            if (trimmedParent.Length == 0)
            {
                return entryName;
            }

            char separator = '\\';
            for (int index = 0; index < trimmedParent.Length; index++)
            {
                if (trimmedParent[index] == '/')
                {
                    separator = '/';
                    break;
                }

                if (trimmedParent[index] == '\\')
                {
                    separator = '\\';
                    break;
                }
            }

            return trimmedParent + separator + entryName.TrimStart('/', '\\');
        }

        private static string GetParentSourceDirectory(string? sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return string.Empty;
            }

            string normalized = NormalizePath(sourcePath);
            for (int index = normalized.Length - 1; index >= 0; index--)
            {
                if (IsSourcePathSeparator(normalized[index]))
                {
                    return normalized.Substring(0, index);
                }
            }

            return string.Empty;
        }

        private static string GetSourceFileName(string sourcePath)
        {
            string normalized = NormalizePath(sourcePath);
            for (int index = normalized.Length - 1; index >= 0; index--)
            {
                if (IsSourcePathSeparator(normalized[index]))
                {
                    return normalized.Substring(index + 1);
                }
            }

            return normalized;
        }

        private readonly struct PathTimestamps
        {
            internal PathTimestamps(DateTimeOffset accessTimeUtc, DateTimeOffset modificationTimeUtc, DateTimeOffset changeTimeUtc)
            {
                AccessTimeUtc = accessTimeUtc;
                ModificationTimeUtc = modificationTimeUtc;
                ChangeTimeUtc = changeTimeUtc;
            }

            internal DateTimeOffset AccessTimeUtc { get; }

            internal DateTimeOffset ModificationTimeUtc { get; }

            internal DateTimeOffset ChangeTimeUtc { get; }
        }
    }
}
