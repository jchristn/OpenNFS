namespace OpenNFS.Rpc.Security.RpcSecGss
{
    /// <summary>
    /// Identifies the RFC 2203 RPCSEC_GSS procedure carried in a credential body.
    /// </summary>
    public enum RpcSecGssProcedure : uint
    {
        /// <summary>
        /// The credential carries a normal data request that uses the established GSS context.
        /// Corresponds to <c>RPCSEC_GSS_DATA</c>.
        /// </summary>
        Data = 0,

        /// <summary>
        /// The credential initiates a new GSS context.
        /// Corresponds to <c>RPCSEC_GSS_INIT</c>.
        /// </summary>
        Init = 1,

        /// <summary>
        /// The credential continues a multi-token GSS context establishment.
        /// Corresponds to <c>RPCSEC_GSS_CONTINUE_INIT</c>.
        /// </summary>
        ContinueInit = 2,

        /// <summary>
        /// The credential requests destruction of an established GSS context.
        /// Corresponds to <c>RPCSEC_GSS_DESTROY</c>.
        /// </summary>
        Destroy = 3,
    }
}
