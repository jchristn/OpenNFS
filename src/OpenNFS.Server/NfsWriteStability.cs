namespace OpenNFS.Server
{
    /// <summary>
    /// Describes the durability level requested or achieved for a host-side write operation.
    /// </summary>
    public enum NfsWriteStability
    {
        /// <summary>
        /// The write has been accepted without requiring stable storage before the reply.
        /// </summary>
        Unstable = 0,

        /// <summary>
        /// The write has been committed to stable storage for file data, but not necessarily all metadata.
        /// </summary>
        DataSync = 1,

        /// <summary>
        /// The write has been committed to stable storage for both file data and required metadata.
        /// </summary>
        FileSync = 2,
    }
}
