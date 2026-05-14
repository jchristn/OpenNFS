namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal abstract class DictionaryNfsFileSystemBase : INfsFileSystem
    {
        private static readonly DateTimeOffset TimestampBaseUtc = new DateTimeOffset(2026, 01, 01, 00, 00, 00, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _fileContents;
        private readonly Dictionary<string, NfsPathKind> _pathKinds;
        private readonly Dictionary<string, PathTimestamps> _pathTimestamps;
        private readonly Dictionary<string, string> _symbolicLinkTargets;
        private long _nextTimestampTicks;

        protected DictionaryNfsFileSystemBase(
            IReadOnlyDictionary<string, NfsPathKind> pathKinds,
            IReadOnlyDictionary<string, byte[]>? fileContents = null,
            IReadOnlyDictionary<string, string>? symbolicLinkTargets = null)
        {
            ArgumentNullException.ThrowIfNull(pathKinds);
            _pathKinds = new Dictionary<string, NfsPathKind>(pathKinds, SeparatorAgnosticPathComparer.Instance);
            _fileContents = new Dictionary<string, byte[]>(SeparatorAgnosticPathComparer.Instance);
            _pathTimestamps = new Dictionary<string, PathTimestamps>(SeparatorAgnosticPathComparer.Instance);
            _symbolicLinkTargets = new Dictionary<string, string>(SeparatorAgnosticPathComparer.Instance);
            _nextTimestampTicks = TimestampBaseUtc.UtcTicks;

            if (fileContents is not null)
            {
                foreach (KeyValuePair<string, byte[]> fileContent in fileContents)
                {
                    ArgumentNullException.ThrowIfNull(fileContent.Value);
                    _fileContents.Add(fileContent.Key, fileContent.Value.AsSpan().ToArray());
                }
            }

            if (symbolicLinkTargets is not null)
            {
                foreach (KeyValuePair<string, string> symbolicLinkTarget in symbolicLinkTargets)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(symbolicLinkTarget.Value);
                    _symbolicLinkTargets.Add(symbolicLinkTarget.Key, symbolicLinkTarget.Value);
                }
            }

            List<string> initialPaths = new List<string>(_pathKinds.Keys);
            initialPaths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string initialPath in initialPaths)
            {
                EnsureTimestampForExistingPath(initialPath);
            }
        }

        public Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.GetPathInfoAsync(this, request);
        }

        public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.LookupPathAsync(this, request);
        }

        public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.ReadDirectoryAsync(this, request);
        }

        public Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.ReadFileAsync(this, request);
        }

        public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.ReadSymbolicLinkAsync(this, request);
        }

        public Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.WriteFileAsync(this, request);
        }

        public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request)
        {
            return DictionaryNfsFileSystemReadOperations.CommitFileAsync(this, request);
        }

        public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.CreatePathAsync(this, request);
        }

        public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.CreateSymbolicLinkAsync(this, request);
        }

        public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.CreateHardLinkAsync(this, request);
        }

        public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.DeletePathAsync(this, request);
        }

        public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request)
        {
            return DictionaryNfsFileSystemMutationOperations.RenamePathAsync(this, request);
        }

        protected abstract string FileSystemDescription { get; }

        protected virtual void OnPathRequested(string sourcePath)
        {
        }

        protected virtual void OnWriteRequest()
        {
        }

        internal Dictionary<string, byte[]> FileContentsCore => _fileContents;

        internal string FileSystemDescriptionCore => FileSystemDescription;

        internal Dictionary<string, NfsPathKind> PathKindsCore => _pathKinds;

        internal Dictionary<string, PathTimestamps> PathTimestampsCore => _pathTimestamps;

        internal Dictionary<string, string> SymbolicLinkTargetsCore => _symbolicLinkTargets;

        internal void NotifyWriteRequestCore()
        {
            OnWriteRequest();
        }

        internal void TrackPathRequestCore(string sourcePath)
        {
            OnPathRequested(sourcePath);
        }

        internal NfsPathInfo GetPathInfoCore(string sourcePath)
        {
            NfsPathKind pathKind = NfsPathKind.Missing;
            if (_pathKinds.TryGetValue(sourcePath, out NfsPathKind resolvedPathKind))
            {
                pathKind = resolvedPathKind;
            }

            ulong length = 0;
            if (_fileContents.TryGetValue(sourcePath, out byte[]? fileContents))
            {
                length = (ulong)fileContents.LongLength;
            }
            else if (_symbolicLinkTargets.TryGetValue(sourcePath, out string? symbolicLinkTarget))
            {
                length = (ulong)Encoding.UTF8.GetByteCount(symbolicLinkTarget);
            }

            if (pathKind == NfsPathKind.Missing)
            {
                return new NfsPathInfo(sourcePath, pathKind, length);
            }

            if (!_pathTimestamps.TryGetValue(sourcePath, out PathTimestamps timestamps))
            {
                timestamps = CreateNextTimestamps();
                _pathTimestamps[sourcePath] = timestamps;
            }

            return new NfsPathInfo(
                sourcePath,
                pathKind,
                length,
                timestamps.AccessTimeUtc,
                timestamps.ModificationTimeUtc,
                timestamps.ChangeTimeUtc);
        }

        internal bool HasDirectoryChildrenCore(string sourcePath)
        {
            string normalizedSourcePath = NormalizePath(sourcePath);

            foreach (string candidatePath in _pathKinds.Keys)
            {
                string? parentDirectory = GetParentSourceDirectory(candidatePath);
                if (string.Equals(NormalizePath(parentDirectory), normalizedSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        internal void MovePathAndDescendantsCore(string sourceSourcePath, string destinationSourcePath)
        {
            List<KeyValuePair<string, NfsPathKind>> pathKindsToMove = new List<KeyValuePair<string, NfsPathKind>>();
            foreach (KeyValuePair<string, NfsPathKind> pathKind in _pathKinds)
            {
                if (IsSamePathOrDescendant(pathKind.Key, sourceSourcePath))
                {
                    pathKindsToMove.Add(pathKind);
                }
            }

            List<KeyValuePair<string, byte[]>> fileContentsToMove = new List<KeyValuePair<string, byte[]>>();
            foreach (KeyValuePair<string, byte[]> fileContent in _fileContents)
            {
                if (IsSamePathOrDescendant(fileContent.Key, sourceSourcePath))
                {
                    fileContentsToMove.Add(fileContent);
                }
            }

            foreach (KeyValuePair<string, NfsPathKind> pathKind in pathKindsToMove)
            {
                _pathKinds.Remove(pathKind.Key);
            }

            foreach (KeyValuePair<string, byte[]> fileContent in fileContentsToMove)
            {
                _fileContents.Remove(fileContent.Key);
            }

            foreach (KeyValuePair<string, NfsPathKind> pathKind in pathKindsToMove)
            {
                _pathKinds[RewritePath(pathKind.Key, sourceSourcePath, destinationSourcePath)] = pathKind.Value;
            }

            foreach (KeyValuePair<string, byte[]> fileContent in fileContentsToMove)
            {
                _fileContents[RewritePath(fileContent.Key, sourceSourcePath, destinationSourcePath)] = fileContent.Value.AsSpan().ToArray();
            }

            List<KeyValuePair<string, PathTimestamps>> pathTimestampsToMove = new List<KeyValuePair<string, PathTimestamps>>();
            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in _pathTimestamps)
            {
                if (IsSamePathOrDescendant(pathTimestamp.Key, sourceSourcePath))
                {
                    pathTimestampsToMove.Add(pathTimestamp);
                }
            }

            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in pathTimestampsToMove)
            {
                _pathTimestamps.Remove(pathTimestamp.Key);
            }

            foreach (KeyValuePair<string, PathTimestamps> pathTimestamp in pathTimestampsToMove)
            {
                _pathTimestamps[RewritePath(pathTimestamp.Key, sourceSourcePath, destinationSourcePath)] = pathTimestamp.Value;
            }

            List<KeyValuePair<string, string>> symbolicLinkTargetsToMove = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> symbolicLinkTarget in _symbolicLinkTargets)
            {
                if (IsSamePathOrDescendant(symbolicLinkTarget.Key, sourceSourcePath))
                {
                    symbolicLinkTargetsToMove.Add(symbolicLinkTarget);
                }
            }

            foreach (KeyValuePair<string, string> symbolicLinkTarget in symbolicLinkTargetsToMove)
            {
                _symbolicLinkTargets.Remove(symbolicLinkTarget.Key);
            }

            foreach (KeyValuePair<string, string> symbolicLinkTarget in symbolicLinkTargetsToMove)
            {
                _symbolicLinkTargets[RewritePath(symbolicLinkTarget.Key, sourceSourcePath, destinationSourcePath)] = symbolicLinkTarget.Value;
            }
        }

        internal void RemovePathAndDescendantsCore(string sourcePath)
        {
            List<string> pathKindsToRemove = new List<string>();
            foreach (string candidatePath in _pathKinds.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    pathKindsToRemove.Add(candidatePath);
                }
            }

            foreach (string pathKindToRemove in pathKindsToRemove)
            {
                _pathKinds.Remove(pathKindToRemove);
            }

            List<string> fileContentsToRemove = new List<string>();
            foreach (string candidatePath in _fileContents.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    fileContentsToRemove.Add(candidatePath);
                }
            }

            foreach (string fileContentToRemove in fileContentsToRemove)
            {
                _fileContents.Remove(fileContentToRemove);
            }

            List<string> pathTimestampsToRemove = new List<string>();
            foreach (string candidatePath in _pathTimestamps.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    pathTimestampsToRemove.Add(candidatePath);
                }
            }

            foreach (string pathTimestampToRemove in pathTimestampsToRemove)
            {
                _pathTimestamps.Remove(pathTimestampToRemove);
            }

            List<string> symbolicLinkTargetsToRemove = new List<string>();
            foreach (string candidatePath in _symbolicLinkTargets.Keys)
            {
                if (IsSamePathOrDescendant(candidatePath, sourcePath))
                {
                    symbolicLinkTargetsToRemove.Add(candidatePath);
                }
            }

            foreach (string symbolicLinkTargetToRemove in symbolicLinkTargetsToRemove)
            {
                _symbolicLinkTargets.Remove(symbolicLinkTargetToRemove);
            }
        }

        private void EnsureTimestampForExistingPath(string sourcePath)
        {
            if (!_pathKinds.ContainsKey(sourcePath) || _pathTimestamps.ContainsKey(sourcePath))
            {
                return;
            }

            _pathTimestamps[sourcePath] = CreateNextTimestamps();
        }

        internal void TouchPathCore(string sourcePath)
        {
            if (!_pathKinds.ContainsKey(sourcePath))
            {
                return;
            }

            _pathTimestamps[sourcePath] = CreateNextTimestamps();
        }

        internal void TouchParentDirectoryCore(string sourcePath)
        {
            string? parentDirectory = GetParentSourceDirectory(sourcePath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                return;
            }

            if (_pathKinds.TryGetValue(parentDirectory, out NfsPathKind parentPathKind)
                && parentPathKind == NfsPathKind.Directory)
            {
                TouchPathCore(parentDirectory);
            }
        }

        private PathTimestamps CreateNextTimestamps()
        {
            DateTimeOffset timestampUtc = new DateTimeOffset(_nextTimestampTicks, TimeSpan.Zero);
            _nextTimestampTicks += TimeSpan.TicksPerSecond;
            return new PathTimestamps(timestampUtc, timestampUtc, timestampUtc);
        }

        internal static bool IsSamePathOrDescendant(string candidatePath, string sourcePath)
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

        internal static string RewritePath(string candidatePath, string sourcePath, string destinationPath)
        {
            if (string.Equals(NormalizePath(candidatePath), NormalizePath(sourcePath), StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            return destinationPath + candidatePath.Substring(sourcePath.Length);
        }

        internal static string NormalizePath(string? sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            // Canonicalize the separator so '\' and '/' compare equal. Server-side path resolution on Linux can
            // produce mixed-separator strings when an export's source path uses Windows syntax.
            return sourcePath.TrimEnd('/', '\\').Replace('\\', '/');
        }

        private static bool IsSourcePathSeparator(char value)
        {
            return value == '/' || value == '\\';
        }

        internal static string CombineSourcePath(string parentDirectory, string entryName)
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

        internal static string GetParentSourceDirectory(string? sourcePath)
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

        internal static string GetSourceFileName(string sourcePath)
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

        internal readonly struct PathTimestamps
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
