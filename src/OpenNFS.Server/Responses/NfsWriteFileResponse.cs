namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of a host-local file write operation.
    /// </summary>
    public sealed class NfsWriteFileResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsWriteFileResponse"/> class.
        /// </summary>
        /// <param name="pathInfo">Resolved path information after the write operation completed.</param>
        /// <param name="bytesWritten">Number of bytes written.</param>
        /// <param name="committedStability">Durability level actually achieved for the write.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsWriteFileResponse(
            NfsPathInfo pathInfo,
            uint bytesWritten,
            NfsWriteStability committedStability)
        {
            ArgumentNullException.ThrowIfNull(pathInfo);
            PathInfo = pathInfo;
            BytesWritten = bytesWritten;
            CommittedStability = committedStability;
        }

        /// <summary>
        /// Gets the resolved path information after the write operation completed.
        /// </summary>
        public NfsPathInfo PathInfo { get; }

        /// <summary>
        /// Gets the number of bytes written.
        /// </summary>
        public uint BytesWritten { get; }

        /// <summary>
        /// Gets the durability level actually achieved for the write.
        /// </summary>
        public NfsWriteStability CommittedStability { get; }
    }
}
