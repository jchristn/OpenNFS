namespace OpenNFS.Server
{
    /// <summary>
    /// Describes the host decision for a mount-related export access evaluation.
    /// </summary>
    public enum NfsMountAccessDisposition
    {
        /// <summary>
        /// Allow the export to be listed or mounted.
        /// </summary>
        Allow = 0,

        /// <summary>
        /// Hide the export from the caller as though it does not exist.
        /// </summary>
        Hide = 1,

        /// <summary>
        /// Deny access explicitly while still acknowledging the export exists.
        /// </summary>
        Deny = 2,
    }
}
