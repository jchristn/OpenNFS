namespace OpenNFS.Server.Abstractions
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Creates and resolves stable server-side filehandles.
    /// </summary>
    public interface IFileHandleProvider
    {
        /// <summary>
        /// Creates or reuses a stable filehandle for a target.
        /// </summary>
        /// <param name="request">Request context for the filehandle-creation operation.</param>
        /// <returns>The created or reused stable filehandle.</returns>
        Task<NfsCreateFileHandleResponse> CreateAsync(NfsCreateFileHandleRequest request);

        /// <summary>
        /// Resolves a previously created filehandle.
        /// </summary>
        /// <param name="request">Request context for the filehandle-resolution operation.</param>
        /// <returns>The filehandle resolution result.</returns>
        Task<NfsResolveFileHandleResponse> ResolveAsync(NfsResolveFileHandleRequest request);
    }
}
