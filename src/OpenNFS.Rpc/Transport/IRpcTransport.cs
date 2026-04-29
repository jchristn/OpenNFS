namespace OpenNFS.Rpc.Transport
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Defines the common send and receive surface for ONC RPC transports.
    /// </summary>
    public interface IRpcTransport
    {
        /// <summary>
        /// Gets the transport protocol used by the current transport instance.
        /// </summary>
        RpcTransportProtocol Protocol { get; }

        /// <summary>
        /// Receives a single RPC message envelope from the transport.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the receive operation.</param>
        /// <returns>The decoded RPC message envelope.</returns>
        Task<RpcMessageEnvelope> ReceiveAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends a single RPC message envelope over the transport.
        /// </summary>
        /// <param name="messageEnvelope">The envelope to send.</param>
        /// <param name="cancellationToken">The cancellation token for the send operation.</param>
        /// <returns>A task that completes when the send operation has finished.</returns>
        Task SendAsync(RpcMessageEnvelope messageEnvelope, CancellationToken cancellationToken);
    }
}
