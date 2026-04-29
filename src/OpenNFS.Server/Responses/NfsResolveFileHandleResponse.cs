namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing the result of resolving a server-side filehandle.
    /// </summary>
    public sealed class NfsResolveFileHandleResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsResolveFileHandleResponse"/> class.
        /// </summary>
        /// <param name="resolution">Resolution result for the requested filehandle.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolution"/> is null.</exception>
        public NfsResolveFileHandleResponse(NfsFileHandleResolution resolution)
        {
            ArgumentNullException.ThrowIfNull(resolution);
            Resolution = resolution;
        }

        /// <summary>
        /// Gets the resolution result for the requested filehandle.
        /// </summary>
        public NfsFileHandleResolution Resolution { get; }
    }
}
