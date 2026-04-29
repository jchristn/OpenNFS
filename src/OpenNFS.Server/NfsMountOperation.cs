namespace OpenNFS.Server
{
    /// <summary>
    /// Describes the mount-related operation being evaluated by the host.
    /// </summary>
    public enum NfsMountOperation
    {
        /// <summary>
        /// The caller is requesting the visible export list.
        /// </summary>
        ListExports = 0,

        /// <summary>
        /// The caller is requesting a root filehandle for an export.
        /// </summary>
        Mount = 1,
    }
}
