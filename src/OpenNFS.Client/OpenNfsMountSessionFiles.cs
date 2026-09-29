namespace OpenNFS.Client
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;

    /// <summary>
    /// Provides path-first file helpers over an <see cref="OpenNfsMountSession"/>.
    /// Transfers are chunked using the server's NFSv3 <c>FSINFO</c> preferred sizes (bounded by the advertised maximums),
    /// which are fetched once per mounted session; 64 KiB chunks are used when <c>FSINFO</c> is unavailable.
    /// </summary>
    public sealed class OpenNfsMountSessionFiles
    {
        private const string ReadOperationName = "Mounted-session file read";
        private const string RenameOperationName = "Mounted-session rename";
        private const string WriteOperationName = "Mounted-session file write";

        private readonly OpenNfsMountSession _session;

        internal OpenNfsMountSessionFiles(OpenNfsMountSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Reads the entire file contents for the specified export-relative path.
        /// Short reads are continued until the server reports end-of-file.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="cancellationToken">Cancellation token for the read operation.</param>
        /// <returns>The full file contents.</returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved or a READ fails.</exception>
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, ReadOperationName, cancellationToken).ConfigureAwait(false);
            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);

            using MemoryStream data = new MemoryStream();
            ulong offset = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                OpenNfsV3ReadResult readResult =
                    await _session.Client.Files.ReadV3Async(fileHandle, offset, (uint)transferSizes.ReadSize, cancellationToken).ConfigureAwait(false);

                if (!readResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException(ReadOperationName, path, readResult.Status);
                }

                int received = Math.Min(readResult.Data.Length, transferSizes.ReadSize);
                if (received > 0)
                {
                    data.Write(readResult.Data.Span.Slice(0, received));
                    offset += (ulong)received;
                }

                if (readResult.EndOfFile)
                {
                    return data.ToArray();
                }

                if (received == 0)
                {
                    throw new OpenNfsClientProtocolException(
                        "Mounted-session file read for path '"
                        + path
                        + "' returned zero bytes without reaching EOF.");
                }
            }
        }

        /// <summary>
        /// Reads a byte range from the specified export-relative file path.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="offset">Zero-based starting byte offset.</param>
        /// <param name="count">Maximum number of bytes to read. Must be zero or greater.</param>
        /// <param name="cancellationToken">Cancellation token for the read operation.</param>
        /// <returns>
        /// The bytes read. Fewer than <paramref name="count"/> bytes are returned when the range extends past end-of-file,
        /// and an empty array is returned when <paramref name="offset"/> is at or beyond the file size.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved, identifies a directory, or a READ fails.</exception>
        public async Task<byte[]> ReadAsync(string path, ulong offset, int count, CancellationToken cancellationToken)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested byte count must be zero or greater.");
            }

            OpenNfsMountSessionResolution resolution =
                await _session.ResolvePathWithAttributesOrThrowAsync(path, ReadOperationName, cancellationToken).ConfigureAwait(false);
            ThrowIfDirectory(resolution.Attributes, ReadOperationName, path);

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);
            byte[] buffer = new byte[count];
            int read = await OpenNfsMountSessionReadSupport.ReadRangeAsync(
                _session,
                resolution.FileHandle,
                path,
                ReadOperationName,
                offset,
                buffer,
                transferSizes.ReadSize,
                cancellationToken).ConfigureAwait(false);

            if (read == buffer.Length)
            {
                return buffer;
            }

            byte[] trimmed = new byte[read];
            Array.Copy(buffer, trimmed, read);
            return trimmed;
        }

        /// <summary>
        /// Opens a read-only, seekable stream over the specified export-relative file path.
        /// The stream's <see cref="Stream.Length"/> is the file size observed when the stream is opened; data is fetched lazily
        /// in chunks through NFSv3 <c>READ</c> calls. Seeking beyond the end is allowed and subsequent reads return zero bytes.
        /// Disposing the stream never throws. The returned stream is not thread-safe.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="cancellationToken">Cancellation token for the open operation.</param>
        /// <returns>A read-only seekable stream.</returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown when the path cannot be resolved or identifies a directory.</exception>
        public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            OpenNfsMountSessionResolution resolution =
                await _session.ResolvePathWithAttributesOrThrowAsync(path, ReadOperationName, cancellationToken).ConfigureAwait(false);
            ThrowIfDirectory(resolution.Attributes, ReadOperationName, path);

            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);
            ulong size = resolution.Attributes!.SizeBytes;
            long length = size > long.MaxValue ? long.MaxValue : (long)size;
            return new OpenNfsMountSessionReadStream(_session, resolution.FileHandle, path, length, transferSizes.ReadSize);
        }

        /// <summary>
        /// Writes the supplied payload to the specified export-relative file path so that the file contents become exactly <paramref name="data"/>.
        /// The file is created (NFSv3 <c>CREATE</c> UNCHECKED) when it does not exist; its parent directory must already exist.
        /// An existing longer file is truncated through NFSv3 <c>SETATTR</c>, and an empty payload produces an empty file.
        /// Short writes are continued from the acknowledged offset. When any <c>WRITE</c> reply reports a stability weaker than
        /// <see cref="OpenNfsWriteStability.FileSync"/>, a <c>COMMIT</c> is issued and its verifier is compared with the write
        /// verifier; if the verifier changed (for example because the server restarted and discarded uncommitted data), the whole
        /// payload is rewritten once with <see cref="OpenNfsWriteStability.FileSync"/> as described in RFC 1813 section 3.3.21.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="data">Payload bytes to write.</param>
        /// <param name="stability">Requested NFSv3 write stability.</param>
        /// <param name="cancellationToken">Cancellation token for the write operation.</param>
        /// <returns>A task that completes when the data has been written and, where required, committed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="OpenNfsV3StatusException">
        /// Thrown when the parent directory is missing, the path identifies a directory, or a CREATE, WRITE, SETATTR, or COMMIT fails.
        /// </exception>
        /// <exception cref="OpenNfsClientIoException">Thrown when the server write verifier keeps changing and durability cannot be confirmed.</exception>
        public async Task WriteAllBytesAsync(
            string path,
            byte[] data,
            OpenNfsWriteStability stability,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(data);

            (byte[] FileHandle, bool Created) target =
                await OpenNfsMountSessionFileWriter.ResolveOrCreateFileAsync(_session, path, WriteOperationName, cancellationToken).ConfigureAwait(false);
            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);

            if (await WriteBufferAsync(target.FileHandle, path, data, stability, transferSizes.WriteSize, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (await WriteBufferAsync(target.FileHandle, path, data, OpenNfsWriteStability.FileSync, transferSizes.WriteSize, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            throw CreateVerifierChangedException(path);
        }

        /// <summary>
        /// Streams data from <paramref name="source"/> to the specified export-relative file path so that the file contents become
        /// exactly the bytes read. Create, truncate, short-write, and commit semantics match <see cref="WriteAllBytesAsync"/>.
        /// Non-seekable sources are supported and are read sequentially in transfer-size chunks.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="source">Source stream to copy from, starting at its current position.</param>
        /// <param name="length">
        /// Exact number of bytes to copy, or <c>null</c> to copy until <paramref name="source"/> reaches its end.
        /// </param>
        /// <param name="stability">Requested NFSv3 write stability.</param>
        /// <param name="cancellationToken">Cancellation token for the write operation.</param>
        /// <returns>A task that completes when the data has been written and, where required, committed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not readable.</exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown when <paramref name="length"/> is supplied and <paramref name="source"/> ends before that many bytes were read.
        /// The bytes read before the failure may already have been written to the file.
        /// </exception>
        /// <exception cref="OpenNfsV3StatusException">
        /// Thrown when the parent directory is missing, the path identifies a directory, or a CREATE, WRITE, SETATTR, or COMMIT fails.
        /// </exception>
        /// <exception cref="OpenNfsClientIoException">
        /// Thrown when the server write verifier changed before the commit and the data cannot be replayed because
        /// <paramref name="source"/> is not seekable, or when the verifier keeps changing after a replay.
        /// </exception>
        public async Task WriteAsync(
            string path,
            Stream source,
            long? length,
            OpenNfsWriteStability stability,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (length.HasValue && length.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length.Value, "The requested byte count must be zero or greater.");
            }

            if (!source.CanRead)
            {
                throw new ArgumentException("The source stream must be readable.", nameof(source));
            }

            (byte[] FileHandle, bool Created) target =
                await OpenNfsMountSessionFileWriter.ResolveOrCreateFileAsync(_session, path, WriteOperationName, cancellationToken).ConfigureAwait(false);
            OpenNfsMountSessionTransferSizes transferSizes = await _session.GetTransferSizesAsync(cancellationToken).ConfigureAwait(false);

            long startPosition = source.CanSeek ? source.Position : -1;

            if (await WriteStreamAsync(target.FileHandle, path, source, length, stability, transferSizes.WriteSize, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (!source.CanSeek)
            {
                throw new OpenNfsClientIoException(
                    "Mounted-session file write for path '"
                    + path
                    + "' could not be confirmed as durable: the server write verifier changed before COMMIT (for example after a server restart), "
                    + "and the non-seekable source stream cannot be replayed. Rewrite the file from the original data.");
            }

            source.Position = startPosition;
            if (await WriteStreamAsync(target.FileHandle, path, source, length, OpenNfsWriteStability.FileSync, transferSizes.WriteSize, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            throw CreateVerifierChangedException(path);
        }

        /// <summary>
        /// Renames or moves a file or directory within the mounted export through NFSv3 <c>RENAME</c>.
        /// When the destination already exists, the server's replacement semantics apply (a file replaces a file; a directory may replace an empty directory).
        /// </summary>
        /// <param name="path">Existing export-relative source path.</param>
        /// <param name="newPath">Export-relative destination path; its parent directory must already exist.</param>
        /// <param name="cancellationToken">Cancellation token for the rename operation.</param>
        /// <returns>A task that completes when the entry has been renamed.</returns>
        /// <exception cref="OpenNfsV3StatusException">Thrown when either parent cannot be resolved or the server rejects the rename.</exception>
        public async Task RenameAsync(string path, string newPath, CancellationToken cancellationToken)
        {
            (byte[] ParentHandle, string EntryName) source =
                await _session.ResolveParentOrThrowAsync(path, RenameOperationName, cancellationToken).ConfigureAwait(false);
            (byte[] ParentHandle, string EntryName) destination =
                await _session.ResolveParentOrThrowAsync(newPath, RenameOperationName, cancellationToken).ConfigureAwait(false);

            OpenNfsV3RenameResult renameResult =
                await _session.Client.Directories.RenameV3Async(
                    source.ParentHandle,
                    source.EntryName,
                    destination.ParentHandle,
                    destination.EntryName,
                    cancellationToken).ConfigureAwait(false);

            if (!renameResult.IsSuccess)
            {
                throw OpenNfsMountSession.CreateStatusException(RenameOperationName, path, renameResult.Status);
            }
        }

        private static void ThrowIfDirectory(OpenNfsV3Attributes? attributes, string operationName, string path)
        {
            if (attributes is not null && attributes.FileType == OpenNfsV3FileType.Directory)
            {
                throw OpenNfsMountSession.CreateStatusException(operationName, path, OpenNfsV3Status.IsDirectory);
            }
        }

        private static OpenNfsClientIoException CreateVerifierChangedException(string path)
        {
            return new OpenNfsClientIoException(
                "Mounted-session file write for path '"
                + path
                + "' could not be confirmed as durable: the server write verifier changed again after the data was rewritten with FILE_SYNC stability.");
        }

        private async Task<bool> WriteBufferAsync(
            byte[] fileHandle,
            string path,
            byte[] data,
            OpenNfsWriteStability stability,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            OpenNfsMountSessionFileWriter writer =
                new OpenNfsMountSessionFileWriter(_session, fileHandle, path, WriteOperationName, stability);

            int cursor = 0;
            while (cursor < data.Length)
            {
                int chunkLength = Math.Min(chunkSize, data.Length - cursor);
                await writer.WriteChunkAsync(new ReadOnlyMemory<byte>(data, cursor, chunkLength), cancellationToken).ConfigureAwait(false);
                cursor += chunkLength;
            }

            return await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> WriteStreamAsync(
            byte[] fileHandle,
            string path,
            Stream source,
            long? length,
            OpenNfsWriteStability stability,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            OpenNfsMountSessionFileWriter writer =
                new OpenNfsMountSessionFileWriter(_session, fileHandle, path, WriteOperationName, stability);

            byte[] buffer = new byte[chunkSize];
            long remaining = length ?? long.MaxValue;

            while (remaining > 0)
            {
                int target = (int)Math.Min(chunkSize, remaining);
                int filled = 0;
                while (filled < target)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(filled, target - filled), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    filled += read;
                }

                if (filled > 0)
                {
                    await writer.WriteChunkAsync(new ReadOnlyMemory<byte>(buffer, 0, filled), cancellationToken).ConfigureAwait(false);
                    remaining -= filled;
                }

                if (filled < target)
                {
                    if (length.HasValue)
                    {
                        throw new EndOfStreamException(
                            "The source stream ended after "
                            + writer.BytesWritten.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " byte(s), before the requested "
                            + length.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            + " byte(s) could be written to '"
                            + path
                            + "'.");
                    }

                    break;
                }
            }

            return await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
