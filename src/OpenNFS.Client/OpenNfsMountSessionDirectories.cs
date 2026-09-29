namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;

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
        /// Creates a directory at the specified export-relative path, optionally creating any missing ancestor directories.
        /// </summary>
        /// <param name="path">Export-relative directory path.</param>
        /// <param name="createParents">
        /// When <c>true</c>, missing ancestors are created and an already-existing directory at <paramref name="path"/> is not an error
        /// (similar to <c>mkdir -p</c>). When <c>false</c>, the behavior matches <see cref="CreateDirectoryAsync(string, CancellationToken)"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for the create operation.</param>
        /// <returns>A task that completes when the directory exists.</returns>
        /// <exception cref="OpenNfsV3StatusException">
        /// Thrown when a create fails, when an ancestor exists but is not a directory (<see cref="OpenNfsV3Status.NotDirectory"/>),
        /// or when <paramref name="path"/> exists as a non-directory (<see cref="OpenNfsV3Status.AlreadyExists"/>).
        /// </exception>
        public async Task CreateDirectoryAsync(string path, bool createParents, CancellationToken cancellationToken)
        {
            if (!createParents)
            {
                await CreateDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
                return;
            }

            const string OperationName = "Mounted-session directory create";
            string[] components = OpenNfsMountSession.SplitPath(path, allowRootPath: false);
            byte[] currentHandle = _session.RootFileHandle.ToArray();

            for (int index = 0; index < components.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool isFinalComponent = index == components.Length - 1;
                string component = components[index];
                OpenNfsV3LookupResult lookupResult =
                    await _session.Client.Directories.LookupV3Async(currentHandle, component, cancellationToken).ConfigureAwait(false);

                if (lookupResult.Status == OpenNfsV3Status.NoEntry)
                {
                    OpenNfsV3CreatePathResult createResult =
                        await _session.Client.Directories.CreateDirectoryV3Async(currentHandle, component, cancellationToken).ConfigureAwait(false);

                    if (createResult.IsSuccess && createResult.ObjectFileHandle.Length > 0)
                    {
                        currentHandle = createResult.ObjectFileHandle.ToArray();
                        continue;
                    }

                    if (!createResult.IsSuccess && createResult.Status != OpenNfsV3Status.AlreadyExists)
                    {
                        throw OpenNfsMountSession.CreateStatusException(OperationName, path, createResult.Status);
                    }

                    lookupResult =
                        await _session.Client.Directories.LookupV3Async(currentHandle, component, cancellationToken).ConfigureAwait(false);
                }

                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    throw OpenNfsMountSession.CreateStatusException(
                        OperationName,
                        path,
                        lookupResult.IsSuccess ? OpenNfsV3Status.ServerFault : lookupResult.Status);
                }

                byte[] childHandle = lookupResult.ObjectFileHandle.ToArray();
                OpenNfsV3Attributes? attributes = lookupResult.ObjectAttributes;
                if (attributes is null)
                {
                    OpenNfsV3GetAttributesResult attributesResult =
                        await _session.Client.Files.GetAttributesV3Async(childHandle, cancellationToken).ConfigureAwait(false);
                    if (!attributesResult.IsSuccess || attributesResult.Attributes is null)
                    {
                        throw OpenNfsMountSession.CreateStatusException(OperationName, path, attributesResult.Status);
                    }

                    attributes = attributesResult.Attributes;
                }

                if (attributes.FileType != OpenNfsV3FileType.Directory)
                {
                    throw OpenNfsMountSession.CreateStatusException(
                        OperationName,
                        path,
                        isFinalComponent ? OpenNfsV3Status.AlreadyExists : OpenNfsV3Status.NotDirectory);
                }

                currentHandle = childHandle;
            }
        }

        /// <summary>
        /// Lists the entries contained within the specified export-relative directory path together with their attributes and filehandles.
        /// Uses NFSv3 <c>READDIRPLUS</c> with full cookie and cookie-verifier paging. When the server answers <c>READDIRPLUS</c> with
        /// <see cref="OpenNfsV3Status.NotSupported"/> (or does not implement the procedure), the listing falls back to <c>READDIR</c>
        /// plus a <c>LOOKUP</c> per entry. Entries for which the server omitted post-operation attributes are completed through
        /// <c>GETATTR</c> or <c>LOOKUP</c>, so every returned entry has non-null <see cref="OpenNfsV3DirectoryPlusEntry.Attributes"/>.
        /// Entries removed concurrently while the listing is completed are omitted.
        /// </summary>
        /// <param name="path">Export-relative directory path.</param>
        /// <param name="cancellationToken">Cancellation token for the directory read.</param>
        /// <returns>The collected directory entries excluding <c>.</c> and <c>..</c>.</returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved or the directory read fails.</exception>
        public async Task<IReadOnlyList<OpenNfsV3DirectoryPlusEntry>> ListWithAttributesAsync(string path, CancellationToken cancellationToken)
        {
            const string OperationName = "Mounted-session directory read";
            byte[] directoryHandle = await _session.ResolvePathHandleOrThrowAsync(path, OperationName, cancellationToken).ConfigureAwait(false);
            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);

            uint directoryCount = (uint)Math.Max(transferSizes.DirectorySize, 1024);
            uint maxCount = (uint)Math.Max(Math.Min(transferSizes.ReadSize, 256 * 1024), 32 * 1024);

            List<OpenNfsV3DirectoryPlusEntry>? rawEntries =
                await ReadDirectoryPlusAllAsync(directoryHandle, path, directoryCount, maxCount, cancellationToken).ConfigureAwait(false);

            if (rawEntries is null)
            {
                return await ListWithAttributesByLookupAsync(directoryHandle, path, cancellationToken).ConfigureAwait(false);
            }

            List<OpenNfsV3DirectoryPlusEntry> completedEntries = new List<OpenNfsV3DirectoryPlusEntry>(rawEntries.Count);
            foreach (OpenNfsV3DirectoryPlusEntry entry in rawEntries)
            {
                if (entry.Attributes is not null && entry.FileHandle.Length > 0)
                {
                    completedEntries.Add(entry);
                    continue;
                }

                OpenNfsV3DirectoryPlusEntry? completedEntry =
                    await CompleteEntryAsync(directoryHandle, entry.Name, entry.Cookie, entry.FileHandle, entry.Attributes, path, cancellationToken).ConfigureAwait(false);
                if (completedEntry is not null)
                {
                    completedEntries.Add(completedEntry);
                }
            }

            return completedEntries;
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

        private static bool IsDotEntry(string name)
        {
            return string.Equals(name, ".", StringComparison.Ordinal)
                || string.Equals(name, "..", StringComparison.Ordinal);
        }

        private static bool IsProcedureUnavailable(OpenNfsClientProtocolException exception)
        {
            return exception.Message.Contains("PROC_UNAVAIL", StringComparison.Ordinal)
                || (exception.InnerException?.Message.Contains("PROC_UNAVAIL", StringComparison.Ordinal) ?? false);
        }

        private async Task<List<OpenNfsV3DirectoryPlusEntry>?> ReadDirectoryPlusAllAsync(
            byte[] directoryHandle,
            string path,
            uint directoryCount,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            const string OperationName = "Mounted-session directory read";
            bool restartedAfterBadCookie = false;

            while (true)
            {
                List<OpenNfsV3DirectoryPlusEntry> entries = new List<OpenNfsV3DirectoryPlusEntry>();
                ulong cookie = 0;
                byte[] cookieVerifier = new byte[8];
                bool restart = false;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    OpenNfsV3ReadDirectoryPlusResult readResult;
                    try
                    {
                        readResult = await _session.Client.Directories.ReadDirectoryPlusV3Async(
                            directoryHandle,
                            cookie,
                            cookieVerifier,
                            directoryCount,
                            maxCount,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OpenNfsClientProtocolException exception) when (cookie == 0 && IsProcedureUnavailable(exception))
                    {
                        return null;
                    }

                    if (readResult.Status == OpenNfsV3Status.NotSupported && cookie == 0)
                    {
                        return null;
                    }

                    if (readResult.Status == OpenNfsV3Status.BadCookie && !restartedAfterBadCookie)
                    {
                        restartedAfterBadCookie = true;
                        restart = true;
                        break;
                    }

                    if (!readResult.IsSuccess)
                    {
                        throw OpenNfsMountSession.CreateStatusException(OperationName, path, readResult.Status);
                    }

                    foreach (OpenNfsV3DirectoryPlusEntry entry in readResult.Entries)
                    {
                        if (!IsDotEntry(entry.Name))
                        {
                            entries.Add(entry);
                        }
                    }

                    if (readResult.EndOfFile || readResult.Entries.Count == 0)
                    {
                        return entries;
                    }

                    cookie = readResult.Entries[readResult.Entries.Count - 1].Cookie;
                    if (readResult.CookieVerifier.Length == 8)
                    {
                        cookieVerifier = readResult.CookieVerifier.ToArray();
                    }
                }

                if (!restart)
                {
                    return entries;
                }
            }
        }

        private async Task<IReadOnlyList<OpenNfsV3DirectoryPlusEntry>> ListWithAttributesByLookupAsync(
            byte[] directoryHandle,
            string path,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsV3DirectoryEntry> entries = await ListAsync(path, cancellationToken).ConfigureAwait(false);
            List<OpenNfsV3DirectoryPlusEntry> completedEntries = new List<OpenNfsV3DirectoryPlusEntry>(entries.Count);

            foreach (OpenNfsV3DirectoryEntry entry in entries)
            {
                OpenNfsV3DirectoryPlusEntry? completedEntry = await CompleteEntryAsync(
                    directoryHandle,
                    entry.Name,
                    entry.Cookie,
                    ReadOnlyMemory<byte>.Empty,
                    attributes: null,
                    path,
                    cancellationToken).ConfigureAwait(false);
                if (completedEntry is not null)
                {
                    completedEntries.Add(completedEntry);
                }
            }

            return completedEntries;
        }

        private async Task<OpenNfsV3DirectoryPlusEntry?> CompleteEntryAsync(
            byte[] directoryHandle,
            string entryName,
            ulong cookie,
            ReadOnlyMemory<byte> fileHandle,
            OpenNfsV3Attributes? attributes,
            string path,
            CancellationToken cancellationToken)
        {
            const string OperationName = "Mounted-session directory read";
            byte[] handle = fileHandle.ToArray();

            if (handle.Length == 0)
            {
                OpenNfsV3LookupResult lookupResult =
                    await _session.Client.Directories.LookupV3Async(directoryHandle, entryName, cancellationToken).ConfigureAwait(false);
                if (lookupResult.Status == OpenNfsV3Status.NoEntry)
                {
                    return null;
                }

                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    throw OpenNfsMountSession.CreateStatusException(
                        OperationName,
                        path,
                        lookupResult.IsSuccess ? OpenNfsV3Status.ServerFault : lookupResult.Status);
                }

                handle = lookupResult.ObjectFileHandle.ToArray();
                attributes ??= lookupResult.ObjectAttributes;
            }

            if (attributes is null)
            {
                OpenNfsV3GetAttributesResult attributesResult =
                    await _session.Client.Files.GetAttributesV3Async(handle, cancellationToken).ConfigureAwait(false);
                if (attributesResult.Status == OpenNfsV3Status.Stale || attributesResult.Status == OpenNfsV3Status.NoEntry)
                {
                    return null;
                }

                if (!attributesResult.IsSuccess || attributesResult.Attributes is null)
                {
                    throw OpenNfsMountSession.CreateStatusException(OperationName, path, attributesResult.Status);
                }

                attributes = attributesResult.Attributes;
            }

            return new OpenNfsV3DirectoryPlusEntry(attributes.FileId, entryName, cookie, attributes, handle);
        }
    }
}
