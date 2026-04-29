namespace OpenNFS.Rpc.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a datagram-oriented byte channel used by RPC UDP transport implementations.
    /// </summary>
    public interface IRpcDatagramChannel
    {
        /// <summary>
        /// Receives a single datagram from the underlying channel.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for the receive operation.</param>
        /// <returns>The received datagram payload.</returns>
        ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends a single datagram to the underlying channel.
        /// </summary>
        /// <param name="datagram">The datagram payload to send.</param>
        /// <param name="cancellationToken">The cancellation token for the send operation.</param>
        /// <returns>A task that completes when the send operation has finished.</returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);
    }
}
