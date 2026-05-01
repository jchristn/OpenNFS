namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for server-side copy and clone support, exposing the operations required
    /// to back the RFC 7862 <c>COPY</c> and <c>CLONE</c> operations.
    /// </summary>
    /// <remarks>
    /// Hosts that do not implement this interface have copy/clone capability advertisement gated off,
    /// and the protocol layer surfaces <c>NFS4ERR_NOTSUPP</c> for these operations. Hosts that
    /// implement it can delegate copies to filesystem-level optimizations such as reflink/cloning.
    /// </remarks>
    public interface INfsCopyClone
    {
        /// <summary>
        /// Copies a byte range from one file to another inside the same server.
        /// </summary>
        /// <param name="request">The copy request.</param>
        /// <returns>The copy result.</returns>
        ValueTask<NfsCopyResponse> CopyAsync(NfsCopyRequest request);

        /// <summary>
        /// Clones a byte range from one file to another, sharing backing storage where supported.
        /// </summary>
        /// <param name="request">The clone request.</param>
        /// <returns>The clone result.</returns>
        ValueTask<NfsCloneResponse> CloneAsync(NfsCloneRequest request);
    }
}
