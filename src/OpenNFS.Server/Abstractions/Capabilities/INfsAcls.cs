namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for ACL support.
    /// </summary>
    public interface INfsAcls
    {
        /// <summary>
        /// Resolves the current ACL entries and advertised ACL support flags for a host-local path.
        /// </summary>
        /// <param name="request">Request context for the ACL lookup operation.</param>
        /// <returns>The current ACL state for the requested path.</returns>
        Task<NfsGetAclResponse> GetAclAsync(NfsGetAclRequest request);

        /// <summary>
        /// Applies a replacement ACL entry set for a host-local path.
        /// </summary>
        /// <param name="request">Request context for the ACL update operation.</param>
        /// <returns>The effective ACL state after the update is applied.</returns>
        Task<NfsSetAclResponse> SetAclAsync(NfsSetAclRequest request);
    }
}
