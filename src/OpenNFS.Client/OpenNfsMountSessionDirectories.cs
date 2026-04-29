namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides path-first directory helpers over an <see cref="OpenNfsMountSession"/>.
    /// </summary>
    public sealed class OpenNfsMountSessionDirectories
    {
        private readonly OpenNfsMountSession _session;

        internal OpenNfsMountSessionDirectories(OpenNfsMountSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Lists the entries contained within the specified export-relative directory path.
        /// </summary>
        /// <param name="path">Export-relative directory path.</param>
        /// <param name="cancellationToken">Cancellation token for the directory read.</param>
        /// <returns>The collected directory entries excluding <c>.</c> and <c>..</c>.</returns>
        public async Task<IReadOnlyList<OpenNfsV3DirectoryEntry>> ListAsync(string path, CancellationToken cancellationToken)
        {
            byte[] directoryHandle = await _session.ResolvePathHandleOrThrowAsync(path, "Mounted-session directory read", cancellationToken).ConfigureAwait(false);
            List<OpenNfsV3DirectoryEntry> entries = new List<OpenNfsV3DirectoryEntry>();
            ulong cookie = 0;
            byte[] cookieVerifier = new byte[8];

            while (true)
            {
                OpenNfsV3ReadDirectoryResult readResult =
                    await _session.Client.Directories.ReadDirectoryV3Async(
                        directoryHandle,
                        cookie,
                        cookieVerifier,
                        64 * 1024U,
                        cancellationToken).ConfigureAwait(false);

                if (!readResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException("Mounted-session directory read", path, readResult.Status);
                }

                foreach (OpenNfsV3DirectoryEntry entry in readResult.Entries)
                {
                    if (!string.Equals(entry.Name, ".", StringComparison.Ordinal)
                        && !string.Equals(entry.Name, "..", StringComparison.Ordinal))
                    {
                        entries.Add(entry);
                    }
                }

                if (readResult.EndOfFile || readResult.Entries.Count == 0)
                {
                    return entries;
                }

                OpenNfsV3DirectoryEntry lastEntry = readResult.Entries[readResult.Entries.Count - 1];
                cookie = lastEntry.Cookie;
                cookieVerifier = readResult.CookieVerifier.Length == 0
                    ? new byte[8]
                    : readResult.CookieVerifier.ToArray();
            }
        }

        /// <summary>
        /// Creates a directory at the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative directory path.</param>
        /// <param name="cancellationToken">Cancellation token for the create operation.</param>
        /// <returns>A task that completes when the directory has been created.</returns>
        public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) parent =
                await _session.ResolveParentOrThrowAsync(path, "Mounted-session directory create", cancellationToken).ConfigureAwait(false);

            OpenNfsV3CreatePathResult createResult =
                await _session.Client.Directories.CreateDirectoryV3Async(parent.ParentHandle, parent.EntryName, cancellationToken).ConfigureAwait(false);

            if (!createResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session directory create", path, createResult.Status);
            }
        }

        /// <summary>
        /// Creates a file at the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="failIfExists">True to request exclusive create behavior.</param>
        /// <param name="cancellationToken">Cancellation token for the create operation.</param>
        /// <returns>A task that completes when the file has been created.</returns>
        public async Task CreateFileAsync(string path, bool failIfExists, CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) parent =
                await _session.ResolveParentOrThrowAsync(path, "Mounted-session file create", cancellationToken).ConfigureAwait(false);

            OpenNfsV3CreatePathResult createResult =
                await _session.Client.Directories.CreateFileV3Async(parent.ParentHandle, parent.EntryName, failIfExists, cancellationToken).ConfigureAwait(false);

            if (!createResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session file create", path, createResult.Status);
            }
        }

        /// <summary>
        /// Deletes a file at the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="cancellationToken">Cancellation token for the delete operation.</param>
        /// <returns>A task that completes when the file has been deleted.</returns>
        public async Task DeleteFileAsync(string path, CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) parent =
                await _session.ResolveParentOrThrowAsync(path, "Mounted-session file delete", cancellationToken).ConfigureAwait(false);

            OpenNfsV3DirectoryMutationResult deleteResult =
                await _session.Client.Directories.RemoveFileV3Async(parent.ParentHandle, parent.EntryName, cancellationToken).ConfigureAwait(false);

            if (!deleteResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session file delete", path, deleteResult.Status);
            }
        }

        /// <summary>
        /// Deletes a directory at the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative directory path.</param>
        /// <param name="cancellationToken">Cancellation token for the delete operation.</param>
        /// <returns>A task that completes when the directory has been deleted.</returns>
        public async Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) parent =
                await _session.ResolveParentOrThrowAsync(path, "Mounted-session directory delete", cancellationToken).ConfigureAwait(false);

            OpenNfsV3DirectoryMutationResult deleteResult =
                await _session.Client.Directories.RemoveDirectoryV3Async(parent.ParentHandle, parent.EntryName, cancellationToken).ConfigureAwait(false);

            if (!deleteResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException("Mounted-session directory delete", path, deleteResult.Status);
            }
        }
    }
}
