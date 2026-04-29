namespace OpenNFS.Client.Raw
{
    /// <summary>
    /// Retry mode exposed by the public raw-client surface.
    /// </summary>
    public enum OpenNfsRetryMode
    {
        /// <summary>
        /// Issue only the initial attempt.
        /// </summary>
        SingleAttemptOnly = 0,

        /// <summary>
        /// Use the client-level retry policy.
        /// </summary>
        UseClientPolicy = 1,
    }
}
