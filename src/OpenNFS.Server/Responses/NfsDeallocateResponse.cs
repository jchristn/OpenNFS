namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsDeallocateRequest"/>.
    /// </summary>
    public sealed class NfsDeallocateResponse
    {
        /// <summary>
        /// Gets the singleton successful response.
        /// </summary>
        public static NfsDeallocateResponse Success { get; } = new NfsDeallocateResponse();

        private NfsDeallocateResponse()
        {
        }
    }
}
