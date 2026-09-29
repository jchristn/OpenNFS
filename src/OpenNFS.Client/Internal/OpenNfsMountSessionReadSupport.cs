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

                uint requestCount = (uint)Math.Min(chunkSize, destination.Length - total);
                ulong readOffset = checked(offset + (ulong)total);
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
