namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for changing file attributes: size (truncate or extend), access and modification timestamps,
    /// permission mode bits, and numeric owner or group. Backs NFSv3 <c>SETATTR</c> and the size, <c>time_modify_set</c>,
    /// <c>time_access_set</c>, and mode attributes of NFSv4.0 <c>SETATTR</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hosts that do not implement this interface keep the previous behavior: size, mode, and ownership changes are rejected with
    /// <c>NFS3ERR_NOTSUPP</c> / <c>NFS4ERR_ATTRNOTSUPP</c>, which means clients cannot truncate files (for example with
    /// <c>O_TRUNC</c> or <c>truncate</c>). Implementing this interface on the configured <see cref="INfsFileSystem"/> is
    /// discovered automatically; it can also be registered separately with <c>OpenNfsServerBuilder.UseAttributeMutation</c>.
    /// </para>
    /// <para>
    /// Implementations signal failures by throwing: <see cref="System.UnauthorizedAccessException"/> maps to an access-denied
    /// status, <see cref="System.NotSupportedException"/> to not-supported, <see cref="System.IO.FileNotFoundException"/> and
    /// <see cref="System.IO.DirectoryNotFoundException"/> to a stale handle, <see cref="System.ArgumentException"/> to an
    /// invalid-argument status, <see cref="System.OverflowException"/> to file-too-large, and other
    /// <see cref="System.IO.IOException"/> instances to an I/O error.
    /// </para>
    /// </remarks>
    public interface INfsAttributeMutation
    {
        /// <summary>
        /// Applies the requested attribute changes to a host-local path.
        /// Only the members of <paramref name="request"/> that are non-null are changed.
        /// </summary>
        /// <param name="request">Request context for the attribute update.</param>
        /// <returns>The path information observed after the update was applied.</returns>
        Task<NfsSetAttributesResponse> SetAttributesAsync(NfsSetAttributesRequest request);
    }
}
