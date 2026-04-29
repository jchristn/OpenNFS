namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response context containing the host decision for mount-related export access.
    /// </summary>
    public sealed class NfsAuthorizeMountResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsAuthorizeMountResponse"/> class.
        /// </summary>
        /// <param name="disposition">
        /// The host decision for the requested export access.
        /// Default value: <see cref="NfsMountAccessDisposition.Allow"/>.
        /// </param>
        public NfsAuthorizeMountResponse(NfsMountAccessDisposition disposition = NfsMountAccessDisposition.Allow)
        {
            Disposition = disposition;
        }

        /// <summary>
        /// Gets the host decision for the requested export access.
        /// </summary>
        public NfsMountAccessDisposition Disposition { get; }
    }
}
