namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Chunked NFSv3 <c>READ</c> helpers shared by the mounted-session file APIs and read streams.
    /// </summary>
    internal static class OpenNfsMountSessionReadSupport
    {
        internal const int PageSize = 4096;

        /// <summary>
        /// Chooses the byte count for one READ so that a transfer-size read never spans more 4 KiB pages than an aligned
        /// transfer-size read would. When the offset is not page-aligned and the request is limited by the chunk size, the
        /// count is shortened so the read ends on a page boundary and every following read is aligned (the same shape the
        /// Linux client uses).
        /// </summary>
        /// <remarks>
        /// Linux knfsd (observed on 6.x kernels) mis-sizes a READ reply whose data spans one page more than <c>rtmax</c> allows
        /// and also needs XDR padding (for example offset 2 MiB + 5, count 1 MiB - 2 at end-of-file): it logs
        /// "rpc-srv/tcp: nfsd: sent N when sending N-2 bytes - shutting down socket" and closes the connection in the middle of
        /// the reply record. Page-aligned chunking never produces such a request.
        /// </remarks>
        internal static uint AlignedReadCount(ulong offset, int remaining, int chunkSize)
        {
            int count = Math.Min(chunkSize, remaining);
            int misalignment = (int)(offset % PageSize);
            if (misalignment != 0 && chunkSize > PageSize && count > chunkSize - misalignment)
            {
                count = chunkSize - misalignment;
            }

            return (uint)Math.Max(1, count);
        }

        /// <summary>
        /// Reads up to <paramref name="destination"/>.Length bytes starting at <paramref name="offset"/>, issuing as many
        /// READ calls as needed. Short reads are continued; the loop stops early only at end-of-file.
        /// </summary>
        /// <returns>The number of bytes copied into <paramref name="destination"/>.</returns>
        internal static async Task<int> ReadRangeAsync(
            OpenNfsMountSession session,
            byte[] fileHandle,
            string path,
            string operationName,
            ulong offset,
            Memory<byte> destination,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < destination.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ulong readOffset = checked(offset + (ulong)total);
                uint requestCount = AlignedReadCount(readOffset, destination.Length - total, chunkSize);
                OpenNfsV3ReadResult readResult =
                    await session.Client.Files.ReadV3Async(fileHandle, readOffset, requestCount, cancellationToken).ConfigureAwait(false);

                if (!readResult.IsSuccess)
                {
                    throw OpenNfsMountSession.CreateStatusException(operationName, path, readResult.Status);
                }

                int received = Math.Min(readResult.Data.Length, (int)requestCount);
                if (received > 0)
                {
                    readResult.Data.Span.Slice(0, received).CopyTo(destination.Span.Slice(total));
                    total += received;
                }

                if (readResult.EndOfFile)
                {
                    break;
                }

                if (received == 0)
                {
                    throw new OpenNfsClientProtocolException(
                        operationName
                        + " for path '"
                        + path
                        + "' returned zero bytes without reaching EOF at offset "
                        + readOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ".");
                }
            }

            return total;
        }
    }
}
