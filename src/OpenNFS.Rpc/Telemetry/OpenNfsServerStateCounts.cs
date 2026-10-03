namespace OpenNFS.Rpc.Telemetry
{
    /// <summary>
    /// Accumulator filled by every registered <see cref="IOpenNfsServerStateSource"/> when an observable gauge is sampled.
    /// </summary>
    /// <remarks>Not thread safe; one instance is created per gauge callback.</remarks>
    internal sealed class OpenNfsServerStateCounts
    {
        internal long Nfs4Clients { get; set; }

        internal long Nfs4Opens { get; set; }

        internal long Nfs4Locks { get; set; }

        internal long Nfs4Delegations { get; set; }

        internal long Nfs4GracePeriodsActive { get; set; }

        internal long Nfs41Sessions { get; set; }

        internal long RunningApplications { get; set; }

        internal long MaximumConnections { get; set; }
    }
}
