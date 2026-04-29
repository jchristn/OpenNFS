namespace OpenNFS.Server.Abstractions
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Evaluates whether a caller may view or mount a configured export.
    /// </summary>
    public interface INfsMountAuthorization
    {
        /// <summary>
        /// Evaluates a mount-related export access request.
        /// </summary>
        /// <param name="request">Request context describing the caller, operation, and export.</param>
        /// <returns>The host decision for the requested export access.</returns>
        Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request);
    }
}
