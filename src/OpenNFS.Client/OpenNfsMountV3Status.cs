namespace OpenNFS.Client
{
    /// <summary>
    /// MOUNT v3 status codes surfaced by the grouped client decode APIs.
    /// </summary>
    public enum OpenNfsMountV3Status
    {
        /// <summary>
        /// The mount succeeded.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The caller lacked permission to mount the export.
        /// </summary>
        PermissionDenied = 1,

        /// <summary>
        /// The requested export path was not found.
        /// </summary>
        NoEntry = 2,

        /// <summary>
        /// The server reported an I/O failure.
        /// </summary>
        Io = 5,

        /// <summary>
        /// Access to the export was denied.
        /// </summary>
        AccessDenied = 13,

        /// <summary>
        /// The requested path was not a directory.
        /// </summary>
        NotDirectory = 20,

        /// <summary>
        /// The request was invalid.
        /// </summary>
        InvalidArgument = 22,

        /// <summary>
        /// The requested path name was too long.
        /// </summary>
        NameTooLong = 63,

        /// <summary>
        /// The server does not support the requested behavior.
        /// </summary>
        NotSupported = 10004,

        /// <summary>
        /// The server reported an unspecified fault.
        /// </summary>
        ServerFault = 10006,
    }
}
