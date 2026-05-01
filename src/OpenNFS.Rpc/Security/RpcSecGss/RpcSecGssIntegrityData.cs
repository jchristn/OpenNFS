namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the RFC 2203 <c>rpc_gss_integ_data</c> wrapper that frames an integrity-protected
    /// argument or result body.
    /// </summary>
    public sealed class RpcSecGssIntegrityData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssIntegrityData"/> class.
        /// </summary>
        /// <param name="sequencedPayload">
        /// The XDR encoding of <c>rpc_gss_data_t</c>, containing the per-call sequence number followed by
        /// the encoded procedure arguments or results that the client and server compare.
        /// </param>
        /// <param name="checksum">The GSS-API MIC checksum bytes computed over <paramref name="sequencedPayload"/>.</param>
        public RpcSecGssIntegrityData(
            ReadOnlyMemory<byte> sequencedPayload,
            ReadOnlyMemory<byte> checksum)
        {
            SequencedPayload = sequencedPayload.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(sequencedPayload.ToArray());
            Checksum = checksum.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(checksum.ToArray());
        }

        /// <summary>
        /// Gets the XDR encoding of <c>rpc_gss_data_t</c>.
        /// </summary>
        public ReadOnlyMemory<byte> SequencedPayload { get; }

        /// <summary>
        /// Gets the GSS-API MIC checksum bytes computed over <see cref="SequencedPayload"/>.
        /// </summary>
        public ReadOnlyMemory<byte> Checksum { get; }
    }
}
