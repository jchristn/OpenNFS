namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsCopyRequest"/>.
    /// </summary>
    public sealed class NfsCopyResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCopyResponse"/> class.
        /// </summary>
        /// <param name="bytesCopied">The number of bytes successfully copied.</param>
        /// <param name="committedToStableStorage">
        /// Indicates whether the destination bytes are durable on stable storage when the response is returned.
        /// </param>
        public NfsCopyResponse(ulong bytesCopied, bool committedToStableStorage)
        {
            BytesCopied = bytesCopied;
            CommittedToStableStorage = committedToStableStorage;
        }

        /// <summary>
        /// Gets the number of bytes successfully copied.
        /// </summary>
        public ulong BytesCopied { get; }

        /// <summary>
        /// Gets a value indicating whether the destination bytes are durable on stable storage.
        /// </summary>
        public bool CommittedToStableStorage { get; }
    }
}
