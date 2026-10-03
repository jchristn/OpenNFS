namespace OpenNFS.Rpc.Telemetry
{
    /// <summary>
    /// Implemented by server components that hold state worth sampling as a gauge (NFSv4 state managers, NFSv4.1
    /// session registries, running server applications). Implementations add their current counts to the supplied
    /// accumulator and must not throw or block for long.
    /// </summary>
    internal interface IOpenNfsServerStateSource
    {
        void ReadState(OpenNfsServerStateCounts counts);
    }
}
