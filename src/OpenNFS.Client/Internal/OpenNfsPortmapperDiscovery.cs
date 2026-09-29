namespace OpenNFS.Client.Internal
{
    using System;

    /// <summary>
    /// Cached outcome of portmapper discovery for one client instance.
    /// </summary>
    internal sealed class OpenNfsPortmapperDiscovery
    {
        internal OpenNfsPortmapperDiscovery(OpenNfsEndpoint? mountEndpoint, OpenNfsEndpoint? nfsEndpoint, string failureReason)
        {
            ArgumentNullException.ThrowIfNull(failureReason);
            MountEndpoint = mountEndpoint;
            NfsEndpoint = nfsEndpoint;
            FailureReason = failureReason;
        }

        internal OpenNfsEndpoint? MountEndpoint { get; }

        internal OpenNfsEndpoint? NfsEndpoint { get; }

        internal string FailureReason { get; }
    }
}
