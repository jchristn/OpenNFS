namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsSeekRequest"/>.
    /// </summary>
    public sealed class NfsSeekResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSeekResponse"/> class.
        /// </summary>
        /// <param name="offset">The offset at which the next requested boundary begins.</param>
        /// <param name="endOfFile">Indicates whether <paramref name="offset"/> is at end-of-file.</param>
        public NfsSeekResponse(ulong offset, bool endOfFile)
        {
            Offset = offset;
            EndOfFile = endOfFile;
        }

        /// <summary>
        /// Gets the offset at which the next requested boundary begins.
        /// </summary>
        public ulong Offset { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Offset"/> is at end-of-file.
        /// </summary>
        public bool EndOfFile { get; }
    }
}
