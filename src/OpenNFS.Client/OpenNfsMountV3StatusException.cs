namespace OpenNFS.Client
{
    /// <summary>
    /// Exception raised when the managed MOUNT v3 bootstrap path returns a non-success mount status.
    /// </summary>
    public sealed class OpenNfsMountV3StatusException : OpenNfsClientException
    {
        /// <summary>
        /// Initialize a MOUNT v3 status exception.
        /// </summary>
        /// <param name="exportPath">Export path associated with the failure.</param>
        /// <param name="status">Returned MOUNT v3 status.</param>
        public OpenNfsMountV3StatusException(string exportPath, OpenNfsMountV3Status status)
            : base(CreateMessage(exportPath, status), ClassifyCategory(status))
        {
            ExportPath = exportPath;
            Status = status;
        }

        /// <summary>
        /// Gets the export path associated with the failure.
        /// </summary>
        public string ExportPath { get; }

        /// <summary>
        /// Gets the native MOUNT v3 status value.
        /// </summary>
        public OpenNfsMountV3Status Status { get; }

        internal static OpenNfsErrorCategory ClassifyCategory(OpenNfsMountV3Status status)
        {
            switch (status)
            {
                case OpenNfsMountV3Status.NoEntry:
                    return OpenNfsErrorCategory.NotFound;

                case OpenNfsMountV3Status.PermissionDenied:
                case OpenNfsMountV3Status.AccessDenied:
                    return OpenNfsErrorCategory.AccessDenied;

                case OpenNfsMountV3Status.NotDirectory:
                case OpenNfsMountV3Status.InvalidArgument:
                case OpenNfsMountV3Status.NameTooLong:
                    return OpenNfsErrorCategory.Conflict;

                case OpenNfsMountV3Status.NotSupported:
                    return OpenNfsErrorCategory.Unsupported;

                case OpenNfsMountV3Status.Io:
                case OpenNfsMountV3Status.ServerFault:
                    return OpenNfsErrorCategory.IoError;

                default:
                    return OpenNfsErrorCategory.Unknown;
            }
        }

        private static string CreateMessage(string exportPath, OpenNfsMountV3Status status)
        {
            return "MOUNT v3 MNT failed for export '" + exportPath + "' with status " + status + ".";
        }
    }
}
