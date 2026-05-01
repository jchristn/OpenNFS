namespace OpenNFS.Server.Responses
{
    /// <summary>
    /// Response for <see cref="OpenNFS.Server.Requests.NfsAllocateRequest"/>.
    /// </summary>
    public sealed class NfsAllocateResponse
    {
        /// <summary>
        /// Gets the singleton successful response.
        /// </summary>
        public static NfsAllocateResponse Success { get; } = new NfsAllocateResponse();

        private NfsAllocateResponse()
        {
        }
    }
}
