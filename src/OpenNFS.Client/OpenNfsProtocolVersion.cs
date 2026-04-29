namespace OpenNFS.Client
{
    /// <summary>
    /// NFS protocol versions exposed by the public raw-client surface.
    /// </summary>
    public enum OpenNfsProtocolVersion
    {
        /// <summary>
        /// NFS version 3.
        /// </summary>
        Nfs3 = 3,

        /// <summary>
        /// NFS version 4.0.
        /// </summary>
        Nfs40 = 40,

        /// <summary>
        /// NFS version 4.1.
        /// </summary>
        Nfs41 = 41,

        /// <summary>
        /// NFS version 4.2.
        /// </summary>
        Nfs42 = 42,
    }
}
