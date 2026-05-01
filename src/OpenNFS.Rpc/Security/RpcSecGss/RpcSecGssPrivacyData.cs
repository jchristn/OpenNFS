namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the RFC 2203 <c>rpc_gss_priv_data</c> wrapper that frames a privacy-protected
    /// argument or result body.
    /// </summary>
    public sealed class RpcSecGssPrivacyData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssPrivacyData"/> class.
        /// </summary>
        /// <param name="wrappedPayload">
        /// The opaque output of <c>GSS_Wrap</c> applied to the XDR encoding of <c>rpc_gss_data_t</c>.
        /// </param>
        public RpcSecGssPrivacyData(ReadOnlyMemory<byte> wrappedPayload)
        {
            WrappedPayload = wrappedPayload.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(wrappedPayload.ToArray());
        }

        /// <summary>
        /// Gets the opaque output of <c>GSS_Wrap</c> applied to the XDR encoding of <c>rpc_gss_data_t</c>.
        /// </summary>
        public ReadOnlyMemory<byte> WrappedPayload { get; }
    }
}
