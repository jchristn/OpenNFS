namespace OpenNFS.Rpc.Telemetry
{
    /// <summary>
    /// Implemented by client components whose configuration is sampled as a gauge (connection pools).
    /// </summary>
    internal interface IOpenNfsClientStateSource
    {
        int MaxConnectionsPerEndpoint { get; }
    }
}
