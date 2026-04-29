namespace OpenNFS.Client
{
    /// <summary>
    /// Write-stability modes exposed by the grouped NFSv3 and NFSv4 file APIs.
    /// </summary>
    public enum OpenNfsWriteStability
    {
        /// <summary>
        /// The server may delay both data and metadata stability work.
        /// </summary>
        Unstable = 0,

        /// <summary>
        /// The server should commit data before replying, but may defer some metadata stability work.
        /// </summary>
        DataSync = 1,

        /// <summary>
        /// The server should commit both data and metadata before replying.
        /// </summary>
        FileSync = 2,
    }
}
