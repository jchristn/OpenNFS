namespace OpenNFS.Client.Sessions
{
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Exception raised when an NFSv4.1 COMPOUND operation surfaces a non-success
    /// <see cref="nfsstat4"/> status, exposing both the native protocol code and a normalized
    /// <see cref="OpenNfsErrorCategory"/>.
    /// </summary>
    public sealed class OpenNfsV41StatusException : OpenNfsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41StatusException"/> class.
        /// </summary>
        /// <param name="operationName">Human-readable operation name.</param>
        /// <param name="status">The native NFSv4.1 status value.</param>
        /// <param name="failedOperationIndex">Zero-based index of the failing operation inside the COMPOUND result array, or <c>-1</c> when unknown.</param>
        public OpenNfsV41StatusException(string operationName, nfsstat4 status, int failedOperationIndex)
            : base(CreateMessage(operationName, status, failedOperationIndex), ClassifyCategory(status))
        {
            OperationName = operationName;
            Status = status;
            FailedOperationIndex = failedOperationIndex;
        }

        /// <summary>
        /// Gets the operation name associated with the failure.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the native NFSv4.1 status value.
        /// </summary>
        public nfsstat4 Status { get; }

        /// <summary>
        /// Gets the zero-based index of the failing operation in the COMPOUND result array, or <c>-1</c>
        /// when the COMPOUND-level status is set without a per-operation result attached.
        /// </summary>
        public int FailedOperationIndex { get; }

        /// <summary>
        /// Maps an NFSv4.1 native status to a normalized <see cref="OpenNfsErrorCategory"/>.
        /// </summary>
        /// <param name="status">The native NFSv4.1 status.</param>
        /// <returns>The normalized category.</returns>
        public static OpenNfsErrorCategory ClassifyCategory(nfsstat4 status)
        {
            switch (status)
            {
                case nfsstat4.NFS4_OK:
                    return OpenNfsErrorCategory.Unknown;

                case nfsstat4.NFS4ERR_NOENT:
                case nfsstat4.NFS4ERR_NXIO:
                case nfsstat4.NFS4ERR_BADHANDLE:
                case nfsstat4.NFS4ERR_STALE:
                case nfsstat4.NFS4ERR_NOFILEHANDLE:
                    return OpenNfsErrorCategory.NotFound;

                case nfsstat4.NFS4ERR_PERM:
                case nfsstat4.NFS4ERR_ACCESS:
                case nfsstat4.NFS4ERR_ROFS:
                case nfsstat4.NFS4ERR_DENIED:
                case nfsstat4.NFS4ERR_LOCKED:
                case nfsstat4.NFS4ERR_DELAY:
                case nfsstat4.NFS4ERR_OPENMODE:
                case nfsstat4.NFS4ERR_GRACE:
                case nfsstat4.NFS4ERR_NO_GRACE:
                case nfsstat4.NFS4ERR_RECLAIM_BAD:
                    return OpenNfsErrorCategory.AccessDenied;

                case nfsstat4.NFS4ERR_EXIST:
                case nfsstat4.NFS4ERR_XDEV:
                case nfsstat4.NFS4ERR_NOTDIR:
                case nfsstat4.NFS4ERR_ISDIR:
                case nfsstat4.NFS4ERR_INVAL:
                case nfsstat4.NFS4ERR_MLINK:
                case nfsstat4.NFS4ERR_NAMETOOLONG:
                case nfsstat4.NFS4ERR_NOTEMPTY:
                case nfsstat4.NFS4ERR_DQUOT:
                case nfsstat4.NFS4ERR_BADTYPE:
                case nfsstat4.NFS4ERR_SAME:
                case nfsstat4.NFS4ERR_NOT_SAME:
                    return OpenNfsErrorCategory.Conflict;

                case nfsstat4.NFS4ERR_NOTSUPP:
                case nfsstat4.NFS4ERR_ATTRNOTSUPP:
                case nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH:
                case nfsstat4.NFS4ERR_OP_ILLEGAL:
                case nfsstat4.NFS4ERR_TOOSMALL:
                    return OpenNfsErrorCategory.Unsupported;

                case nfsstat4.NFS4ERR_IO:
                case nfsstat4.NFS4ERR_FBIG:
                case nfsstat4.NFS4ERR_NOSPC:
                case nfsstat4.NFS4ERR_SERVERFAULT:
                case nfsstat4.NFS4ERR_FILE_OPEN:
                case nfsstat4.NFS4ERR_DEADLOCK:
                case nfsstat4.NFS4ERR_REP_TOO_BIG:
                case nfsstat4.NFS4ERR_REP_TOO_BIG_TO_CACHE:
                    return OpenNfsErrorCategory.IoError;

                case nfsstat4.NFS4ERR_BADXDR:
                case nfsstat4.NFS4ERR_BAD_SEQID:
                case nfsstat4.NFS4ERR_BADSESSION:
                case nfsstat4.NFS4ERR_BADSLOT:
                case nfsstat4.NFS4ERR_DEADSESSION:
                case nfsstat4.NFS4ERR_OP_NOT_IN_SESSION:
                case nfsstat4.NFS4ERR_SEQUENCE_POS:
                case nfsstat4.NFS4ERR_RETRY_UNCACHED_REP:
                case nfsstat4.NFS4ERR_BAD_STATEID:
                case nfsstat4.NFS4ERR_BAD_COOKIE:
                case nfsstat4.NFS4ERR_STALE_CLIENTID:
                case nfsstat4.NFS4ERR_STALE_STATEID:
                case nfsstat4.NFS4ERR_EXPIRED:
                case nfsstat4.NFS4ERR_CLIENTID_BUSY:
                    return OpenNfsErrorCategory.ProtocolError;

                default:
                    return OpenNfsErrorCategory.Unknown;
            }
        }

        private static string CreateMessage(string operationName, nfsstat4 status, int failedOperationIndex)
        {
            if (failedOperationIndex < 0)
            {
                return operationName + " failed with NFSv4.1 status " + status + ".";
            }

            return operationName + " failed at COMPOUND operation index " + failedOperationIndex
                + " with NFSv4.1 status " + status + ".";
        }
    }
}
