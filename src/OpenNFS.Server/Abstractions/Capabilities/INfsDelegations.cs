namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for NFSv4 delegations support.
    /// </summary>
    public interface INfsDelegations
    {
        /// <summary>
        /// Determines whether a delegation should be granted for the requested open flow.
        /// </summary>
        Task<NfsAcquireDelegationResponse> AcquireDelegationAsync(NfsAcquireDelegationRequest request);

        /// <summary>
        /// Notifies the host that a previously granted delegation must be recalled before a conflicting open can complete.
        /// </summary>
        Task RecallDelegationAsync(NfsRecallDelegationRequest request);

        /// <summary>
        /// Notifies the host that a previously granted delegation was returned by the client.
        /// </summary>
        Task ReturnDelegationAsync(NfsReturnDelegationRequest request);
    }
}
