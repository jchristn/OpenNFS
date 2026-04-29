namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing a created or reused stable server-side filehandle.
    /// </summary>
    public sealed class NfsCreateFileHandleResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateFileHandleResponse"/> class.
        /// </summary>
        /// <param name="fileHandle">Created or reused filehandle.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileHandle"/> is null.</exception>
        public NfsCreateFileHandleResponse(NfsFileHandle fileHandle)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);
            FileHandle = fileHandle;
        }

        /// <summary>
        /// Gets the created or reused filehandle.
        /// </summary>
        public NfsFileHandle FileHandle { get; }
    }
}
