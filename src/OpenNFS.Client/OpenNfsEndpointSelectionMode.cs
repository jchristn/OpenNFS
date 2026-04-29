namespace OpenNFS.Client
{
    /// <summary>
    /// Endpoint selection mode exposed by the public OpenNFS client surface.
    /// </summary>
    public enum OpenNfsEndpointSelectionMode
    {
        /// <summary>
        /// Use only the primary endpoint.
        /// </summary>
        PrimaryOnly = 0,

        /// <summary>
        /// Try the primary endpoint first, then configured alternates in order.
        /// </summary>
        SequentialFailover = 1,
    }
}
