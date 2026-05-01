namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a registered GSS-API mechanism that the RPCSEC_GSS layer drives during context
    /// establishment, MIC computation, and wrap/unwrap.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are responsible for the actual cryptography. The OpenNFS RPC
    /// layer only sees opaque tokens, MIC bytes, and wrapped buffers; the mechanism interprets them.
    /// The first concrete implementation is the Kerberos v5 provider; other mechanisms can plug in
    /// without changing the RPC layer.
    /// </remarks>
    public interface IRpcSecGssMechanism
    {
        /// <summary>
        /// Gets the mechanism name advertised through this provider.
        /// </summary>
        RpcSecGssMechanismName MechanismName { get; }

        /// <summary>
        /// Begins or continues GSS-API context establishment by accepting an inbound token from a peer.
        /// </summary>
        /// <param name="contextHandle">The context handle for an in-progress context, or empty for a fresh context.</param>
        /// <param name="inboundToken">The inbound GSS-API token bytes.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The acceptance result.</returns>
        ValueTask<RpcSecGssAcceptResult> AcceptSecurityContextAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> inboundToken,
            CancellationToken cancellationToken);

        /// <summary>
        /// Verifies a GSS-API MIC checksum.
        /// </summary>
        /// <param name="contextHandle">The established context handle.</param>
        /// <param name="message">The bytes the checksum was computed over.</param>
        /// <param name="checksum">The MIC bytes presented by the peer.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns><c>true</c> when the checksum verifies; otherwise <c>false</c>.</returns>
        ValueTask<bool> VerifyMicAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> message,
            ReadOnlyMemory<byte> checksum,
            CancellationToken cancellationToken);

        /// <summary>
        /// Computes a GSS-API MIC checksum over <paramref name="message"/>.
        /// </summary>
        /// <param name="contextHandle">The established context handle.</param>
        /// <param name="message">The bytes to sign.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The MIC bytes.</returns>
        ValueTask<byte[]> ComputeMicAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken);

        /// <summary>
        /// Unwraps a privacy-protected payload.
        /// </summary>
        /// <param name="contextHandle">The established context handle.</param>
        /// <param name="wrappedPayload">The wrapped payload bytes.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The decrypted plain-text bytes.</returns>
        ValueTask<byte[]> UnwrapAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> wrappedPayload,
            CancellationToken cancellationToken);

        /// <summary>
        /// Wraps a plain-text payload with privacy protection.
        /// </summary>
        /// <param name="contextHandle">The established context handle.</param>
        /// <param name="plaintext">The bytes to encrypt.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The wrapped payload bytes.</returns>
        ValueTask<byte[]> WrapAsync(
            ReadOnlyMemory<byte> contextHandle,
            ReadOnlyMemory<byte> plaintext,
            CancellationToken cancellationToken);

        /// <summary>
        /// Releases server-side resources associated with the supplied context handle.
        /// </summary>
        /// <param name="contextHandle">The context handle to release.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when the context is released.</returns>
        ValueTask DeleteSecurityContextAsync(
            ReadOnlyMemory<byte> contextHandle,
            CancellationToken cancellationToken);
    }
}
