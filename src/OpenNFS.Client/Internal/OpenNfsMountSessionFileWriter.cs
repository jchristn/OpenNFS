namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Writes a whole-file payload through NFSv3 <c>WRITE</c>, <c>SETATTR</c>, and <c>COMMIT</c> with RFC 1813 semantics:
    /// short writes are continued from <c>offset + count</c>, the final size is forced to the written length, and
    /// unstable or data-sync replies are committed and verified against the server write verifier.
    /// </summary>
    internal sealed class OpenNfsMountSessionFileWriter
    {
        private readonly byte[] _fileHandle;
        private readonly string _operationName;
        private readonly string _path;
        private readonly OpenNfsMountSession _session;
        private readonly OpenNfsWriteStability _stability;
        private OpenNfsV3Attributes? _lastAttributes;
        private bool _needsCommit;
        private ulong _offset;
        private byte[]? _unstableVerifier;
        private bool _verifierChanged;

        internal OpenNfsMountSessionFileWriter(
            OpenNfsMountSession session,
            byte[] fileHandle,
            string path,
            string operationName,
            OpenNfsWriteStability stability)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(operationName);

            _session = session;
            _fileHandle = fileHandle;
            _path = path;
            _operationName = operationName;
            _stability = stability;
        }

        internal ulong BytesWritten
        {
            get
            {
                return _offset;
            }
        }

        internal static async Task<(byte[] FileHandle, bool Created)> ResolveOrCreateFileAsync(
            OpenNfsMountSession session,
            string path,
            string operationName,
            CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) parent =
                await session.ResolveParentOrThrowAsync(path, operationName, cancellationToken).ConfigureAwait(false);

            OpenNfsV3LookupResult lookupResult =
                await session.Client.Directories.LookupV3Async(parent.ParentHandle, parent.EntryName, cancellationToken).ConfigureAwait(false);

            if (lookupResult.IsSuccess && lookupResult.ObjectFileHandle.Length > 0)
            {
                byte[] existingHandle = lookupResult.ObjectFileHandle.ToArray();
                OpenNfsV3Attributes? attributes = lookupResult.ObjectAttributes;
                if (attributes is null)
                {
                    OpenNfsV3GetAttributesResult attributesResult =
                        await session.Client.Files.GetAttributesV3Async(existingHandle, cancellationToken).ConfigureAwait(false);
                    if (!attributesResult.IsSuccess || attributesResult.Attributes is null)
                    {
                        throw OpenNfsMountSession.CreateStatusException(operationName, path, attributesResult.Status);
                    }

                    attributes = attributesResult.Attributes;
                }

                if (attributes.FileType == OpenNfsV3FileType.Directory)
                {
                    throw OpenNfsMountSession.CreateStatusException(operationName, path, OpenNfsV3Status.IsDirectory);
                }

                return (existingHandle, false);
            }

            if (lookupResult.Status != OpenNfsV3Status.NoEntry)
            {
                throw OpenNfsMountSession.CreateStatusException(
                    operationName,
                    path,
                    lookupResult.IsSuccess ? OpenNfsV3Status.ServerFault : lookupResult.Status);
            }

            OpenNfsV3CreatePathResult createResult =
                await session.Client.Directories.CreateFileV3Async(parent.ParentHandle, parent.EntryName, failIfExists: false, cancellationToken).ConfigureAwait(false);
            if (!createResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException(operationName, path, createResult.Status);
            }

            if (createResult.ObjectFileHandle.Length > 0)
            {
                return (createResult.ObjectFileHandle.ToArray(), true);
            }

            OpenNfsV3LookupResult createdLookup =
                await session.Client.Directories.LookupV3Async(parent.ParentHandle, parent.EntryName, cancellationToken).ConfigureAwait(false);
            if (!createdLookup.IsSuccess || createdLookup.ObjectFileHandle.Length == 0)
            {
                throw OpenNfsMountSession.CreateStatusException(
                    operationName,
                    path,
                    createdLookup.IsSuccess ? OpenNfsV3Status.ServerFault : createdLookup.Status);
            }

            return (createdLookup.ObjectFileHandle.ToArray(), true);
        }

        internal async Task WriteChunkAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken)
        {
            int written = 0;
            while (written < chunk.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] payload = chunk.Slice(written).ToArray();
                ulong writeOffset = checked(_offset + (ulong)written);
                OpenNfsV3WriteResult writeResult =
                    await _session.Client.Files.WriteV3Async(_fileHandle, writeOffset, _stability, payload, cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException(_operationName, _path, writeResult.Status);
                }

                if (writeResult.Count == 0)
                {
                    throw new OpenNfsClientProtocolException(
                        _operationName
                        + " for path '"
                        + _path
                        + "' made no progress: the server acknowledged zero bytes at offset "
                        + writeOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ".");
                }

                if (writeResult.Count > (uint)payload.Length)
                {
                    throw new OpenNfsClientProtocolException(
                        _operationName
                        + " for path '"
                        + _path
                        + "' acknowledged "
                        + writeResult.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " byte(s), more than the "
                        + payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " byte(s) sent.");
                }

                TrackStability(writeResult);
                written += (int)writeResult.Count;
            }

            _offset = checked(_offset + (ulong)chunk.Length);
        }

        /// <summary>
        /// Forces the final file size to the written length and commits unstable data.
        /// </summary>
        /// <returns>
        /// <c>true</c> when all written data is known to be durable; <c>false</c> when the server write verifier changed
        /// between the unstable writes and the commit (for example after a server restart), which means the data must be rewritten.
        /// </returns>
        internal async Task<bool> CompleteAsync(CancellationToken cancellationToken)
        {
            ulong expectedSize = _offset;
            ulong? currentSize = _lastAttributes?.SizeBytes;

            if (!currentSize.HasValue)
            {
                OpenNfsV3GetAttributesResult attributesResult =
                    await _session.Client.Files.GetAttributesV3Async(_fileHandle, cancellationToken).ConfigureAwait(false);
                if (!attributesResult.IsSuccess || attributesResult.Attributes is null)
                {
                    throw OpenNfsMountSession.CreateStatusException(_operationName, _path, attributesResult.Status);
                }

                currentSize = attributesResult.Attributes.SizeBytes;
            }

            if (currentSize.Value != expectedSize)
            {
                OpenNfsV3SetAttributesResult truncateResult =
                    await _session.Client.Files.SetAttributesV3Async(
                        _fileHandle,
                        new OpenNfsV3SetAttributes(sizeBytes: expectedSize),
                        guardChangeTime: null,
                        cancellationToken).ConfigureAwait(false);
                if (!truncateResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException(_operationName + " (SETATTR size)", _path, truncateResult.Status);
                }
            }

            if (!_needsCommit)
            {
                return true;
            }

            OpenNfsV3CommitResult commitResult =
                await _session.Client.Files.CommitV3Async(_fileHandle, 0, 0, cancellationToken).ConfigureAwait(false);
            if (!commitResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException(_operationName + " (COMMIT)", _path, commitResult.Status);
            }

            if (_verifierChanged
                || _unstableVerifier is null
                || !commitResult.Verifier.Span.SequenceEqual(_unstableVerifier))
            {
                return false;
            }

            return true;
        }

        private void TrackStability(OpenNfsV3WriteResult writeResult)
        {
            _lastAttributes = writeResult.FileWeakCacheConsistency?.After;

            if (writeResult.CommittedStability == OpenNfsWriteStability.FileSync)
            {
                return;
            }

            _needsCommit = true;
            byte[] verifier = writeResult.Verifier.ToArray();
            if (_unstableVerifier is null)
            {
                _unstableVerifier = verifier;
            }
            else if (!_unstableVerifier.AsSpan().SequenceEqual(verifier))
            {
                _verifierChanged = true;
            }
        }
    }
}
