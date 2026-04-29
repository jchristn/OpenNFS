namespace OpenNFS.Server.Abstractions.Capabilities
{
    using System.Threading.Tasks;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    /// <summary>
    /// Optional host contract for advisory and mandatory locking support.
    /// </summary>
    public interface INfsLocking
    {
        /// <summary>
        /// Processes a protocol-neutral lock operation against the host lock manager.
        /// </summary>
        /// <param name="request">Request context for the lock operation.</param>
        /// <returns>The result of the requested lock operation.</returns>
        Task<NfsLockResponse> ProcessLockAsync(NfsLockRequest request);
    }
}
