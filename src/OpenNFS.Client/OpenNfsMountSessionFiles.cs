namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides path-first file helpers over an <see cref="OpenNfsMountSession"/>.
    /// </summary>
    public sealed class OpenNfsMountSessionFiles
    {
        private readonly OpenNfsMountSession _session;

        internal OpenNfsMountSessionFiles(OpenNfsMountSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Reads the entire file contents for the specified export-relative path.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="cancellationToken">Cancellation token for the read operation.</param>
        /// <returns>The full file contents.</returns>
        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        {
            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, "Mounted-session file read", cancellationToken).ConfigureAwait(false);
            List<byte> data = new List<byte>();
            ulong offset = 0;

            while (true)
            {
                OpenNfsV3ReadResult readResult =
                    await _session.Client.Files.ReadV3Async(fileHandle, offset, 64 * 1024U, cancellationToken).ConfigureAwait(false);

                if (!readResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException("Mounted-session file read", path, readResult.Status);
                }

                if (readResult.Data.Length > 0)
                {
                    data.AddRange(readResult.Data.ToArray());
                    offset += readResult.Count;
                }

                if (readResult.EndOfFile)
                {
                    return data.ToArray();
                }

                if (readResult.Count == 0)
                {
                    throw new OpenNfsClientProtocolException(
                        "Mounted-session file read for path '"
                        + path
                        + "' returned zero bytes without reaching EOF.");
                }
            }
        }

        /// <summary>
        /// Writes the supplied payload to the specified export-relative file path starting at offset zero.
        /// </summary>
        /// <param name="path">Export-relative file path.</param>
        /// <param name="data">Payload bytes to write.</param>
        /// <param name="stability">Requested NFSv3 write stability.</param>
        /// <param name="cancellationToken">Cancellation token for the write operation.</param>
        /// <returns>A task that completes when the write operation has finished.</returns>
        public async Task WriteAllBytesAsync(
            string path,
            byte[] data,
            OpenNfsWriteStability stability,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(data);

            byte[] fileHandle = await _session.ResolvePathHandleOrThrowAsync(path, "Mounted-session file write", cancellationToken).ConfigureAwait(false);
            ulong offset = 0;
            int cursor = 0;

            while (cursor < data.Length)
            {
                int chunkLength = Math.Min(64 * 1024, data.Length - cursor);
                byte[] chunk = new byte[chunkLength];
                Array.Copy(data, cursor, chunk, 0, chunkLength);

                OpenNfsV3WriteResult writeResult =
                    await _session.Client.Files.WriteV3Async(fileHandle, offset, stability, chunk, cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException("Mounted-session file write", path, writeResult.Status);
                }

                if (writeResult.Count != (uint)chunkLength)
                {
                    throw new OpenNfsClientProtocolException(
                        "Mounted-session file write for path '"
                        + path
                        + "' acknowledged "
                        + writeResult.Count
                        + " byte(s) instead of "
                        + chunkLength
                        + ".");
                }

                cursor += chunkLength;
                offset += (uint)chunkLength;
            }
        }
    }
}
