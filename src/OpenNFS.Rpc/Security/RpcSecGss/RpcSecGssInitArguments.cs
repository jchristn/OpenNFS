namespace OpenNFS.Rpc.Security.RpcSecGss
{
    using System;

    /// <summary>
    /// Represents the RFC 2203 <c>rpc_gss_init_arg</c> payload carried by <see cref="RpcSecGssProcedure.Init"/>
    /// and <see cref="RpcSecGssProcedure.ContinueInit"/> calls.
    /// </summary>
    public sealed class RpcSecGssInitArguments
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcSecGssInitArguments"/> class.
        /// </summary>
        /// <param name="token">The opaque GSS-API token bytes produced by the calling principal.</param>
        public RpcSecGssInitArguments(ReadOnlyMemory<byte> token)
        {
            Token = token.Length == 0 ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(token.ToArray());
        }

        /// <summary>
        /// Gets the opaque GSS-API token bytes produced by the calling principal.
        /// </summary>
        public ReadOnlyMemory<byte> Token { get; }
    }
}
