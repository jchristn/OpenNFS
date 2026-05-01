namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for name and identity mapping support.
    /// </summary>
    public interface INfsIdMapper
    {
        /// <summary>
        /// Resolves owner and group identity strings for a host-local path.
        /// </summary>
        /// <param name="request">Identity-mapping request context.</param>
        /// <returns>The mapped owner and group identity strings.</returns>
        Task<NfsGetIdentityResponse> GetIdentityAsync(NfsGetIdentityRequest request);

        /// <summary>
        /// Applies replacement owner and owner-group identity strings for a host-local path.
        /// </summary>
        /// <param name="request">Identity-update request context.</param>
        /// <returns>The effective owner and group identity strings after the update.</returns>
        Task<NfsSetIdentityResponse> SetIdentityAsync(NfsSetIdentityRequest request);
    }
}
