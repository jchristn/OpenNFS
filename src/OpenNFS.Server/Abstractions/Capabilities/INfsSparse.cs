namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for sparse-file support, exposing the operations required to back the
    /// RFC 7862 <c>SEEK</c>, <c>ALLOCATE</c>, <c>DEALLOCATE</c>, and <c>READ_PLUS</c> operations.
    /// </summary>
    /// <remarks>
    /// Hosts that do not implement this interface have sparse-file capability advertisement gated off,
    /// and the protocol layer surfaces <c>NFS4ERR_NOTSUPP</c> for these operations. Hosts that
    /// implement it can return real data/hole extents and reservation results.
    /// </remarks>
    public interface INfsSparse
    {
        /// <summary>
        /// Finds the next data or hole boundary in a sparse file.
        /// </summary>
        /// <param name="request">The seek request.</param>
        /// <returns>The seek result.</returns>
        ValueTask<NfsSeekResponse> SeekAsync(NfsSeekRequest request);

        /// <summary>
        /// Reserves backing storage for a byte range without changing the file's logical size.
        /// </summary>
        /// <param name="request">The allocate request.</param>
        /// <returns>The allocate result.</returns>
        ValueTask<NfsAllocateResponse> AllocateAsync(NfsAllocateRequest request);

        /// <summary>
        /// Releases backing storage for a byte range and converts the range to a hole.
        /// </summary>
        /// <param name="request">The deallocate request.</param>
        /// <returns>The deallocate result.</returns>
        ValueTask<NfsDeallocateResponse> DeallocateAsync(NfsDeallocateRequest request);

        /// <summary>
        /// Reads a byte range and returns a sequence of data and hole extents.
        /// </summary>
        /// <param name="request">The read-sparse request.</param>
        /// <returns>The read-sparse result.</returns>
        ValueTask<NfsReadSparseResponse> ReadSparseAsync(NfsReadSparseRequest request);
    }
}
