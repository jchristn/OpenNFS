namespace OpenNFS.Server
{
    /// <summary>
    /// Normalized high-level error categories for managed OpenNFS server failures.
    /// </summary>
    /// <remarks>
    /// This enum mirrors the client-side <c>OpenNfsErrorCategory</c> so that consumers writing
    /// cross-cutting error-handling code can work in the same vocabulary across both public packages.
    /// </remarks>
    public enum OpenNfsServerErrorCategory
    {
        /// <summary>
        /// The failure does not map to a more specific normalized category.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The target object or path was not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The caller was denied access.
        /// </summary>
        AccessDenied,

        /// <summary>
        /// The target object state conflicts with the requested operation.
        /// </summary>
        Conflict,

        /// <summary>
        /// The target capability or operation is not supported.
        /// </summary>
        Unsupported,

        /// <summary>
        /// The failure reflects an I/O-state problem on the target object, transport, or handle.
        /// </summary>
        IoError,

        /// <summary>
        /// The failure reflects a malformed, invalid, or otherwise protocol-level request or response state.
        /// </summary>
        ProtocolError,

        /// <summary>
        /// The operation was cancelled.
        /// </summary>
        Cancelled,
    }
}
