namespace OpenNFS.Client
{
    /// <summary>
    /// Exception raised when an NFSv3 operation on the managed client surface returns a non-success NFS status.
    /// </summary>
    public sealed class OpenNfsV3StatusException : OpenNfsClientException
    {
        /// <summary>
        /// Initialize an NFSv3 status exception.
        /// </summary>
        /// <param name="operationName">Human-readable operation name.</param>
        /// <param name="path">Optional export-relative path associated with the failure.</param>
        /// <param name="status">Returned NFSv3 status.</param>
        public OpenNfsV3StatusException(string operationName, string? path, OpenNfsV3Status status)
            : base(CreateMessage(operationName, path, status), ClassifyCategory(status))
        {
            OperationName = operationName;
            Path = path ?? string.Empty;
            Status = status;
        }

        /// <summary>
        /// Gets the operation name associated with the failure.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the export-relative path associated with the failure when available.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the native NFSv3 status value.
        /// </summary>
        public OpenNfsV3Status Status { get; }

        internal static OpenNfsErrorCategory ClassifyCategory(OpenNfsV3Status status)
        {
            switch (status)
            {
                case OpenNfsV3Status.NoEntry:
                case OpenNfsV3Status.NoSuchDevice:
                case OpenNfsV3Status.NoSuchDeviceOrAddress:
                    return OpenNfsErrorCategory.NotFound;

                case OpenNfsV3Status.PermissionDenied:
                case OpenNfsV3Status.AccessDenied:
                case OpenNfsV3Status.ReadOnlyFileSystem:
                    return OpenNfsErrorCategory.AccessDenied;

                case OpenNfsV3Status.AlreadyExists:
                case OpenNfsV3Status.CrossDevice:
                case OpenNfsV3Status.NotDirectory:
                case OpenNfsV3Status.IsDirectory:
                case OpenNfsV3Status.InvalidArgument:
                case OpenNfsV3Status.TooManyLinks:
                case OpenNfsV3Status.NameTooLong:
                case OpenNfsV3Status.NotEmpty:
                case OpenNfsV3Status.QuotaExceeded:
                case OpenNfsV3Status.Remote:
                case OpenNfsV3Status.Jukebox:
                    return OpenNfsErrorCategory.Conflict;

                case OpenNfsV3Status.NotSupported:
                case OpenNfsV3Status.BadType:
                    return OpenNfsErrorCategory.Unsupported;

                case OpenNfsV3Status.Io:
                case OpenNfsV3Status.FileTooLarge:
                case OpenNfsV3Status.NoSpace:
                case OpenNfsV3Status.Stale:
                case OpenNfsV3Status.ServerFault:
                    return OpenNfsErrorCategory.IoError;

                case OpenNfsV3Status.BadHandle:
                case OpenNfsV3Status.NotSynchronized:
                case OpenNfsV3Status.BadCookie:
                case OpenNfsV3Status.TooSmall:
                    return OpenNfsErrorCategory.ProtocolError;

                default:
                    return OpenNfsErrorCategory.Unknown;
            }
        }

        private static string CreateMessage(string operationName, string? path, OpenNfsV3Status status)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return operationName + " failed with NFSv3 status " + status + ".";
            }

            return operationName + " failed for path '" + path + "' with NFSv3 status " + status + ".";
        }
    }
}
