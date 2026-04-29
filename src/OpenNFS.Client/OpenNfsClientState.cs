namespace OpenNFS.Client
{
    /// <summary>
    /// Lifetime state exposed by the public OpenNFS client wrapper.
    /// </summary>
    public enum OpenNfsClientState
    {
        /// <summary>
        /// The client has been constructed but not opened.
        /// </summary>
        Created = 0,

        /// <summary>
        /// The client has been opened.
        /// </summary>
        Open = 1,

        /// <summary>
        /// The client has been closed and cannot be reopened.
        /// </summary>
        Closed = 2,

        /// <summary>
        /// The client has been disposed.
        /// </summary>
        Disposed = 3,
    }
}
