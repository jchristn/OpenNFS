namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsCloneRequest"/>.
    /// </summary>
    public sealed class NfsCloneResponse
    {
        /// <summary>
        /// Gets the singleton successful response.
        /// </summary>
        public static NfsCloneResponse Success { get; } = new NfsCloneResponse();

        private NfsCloneResponse()
        {
        }
    }
}
