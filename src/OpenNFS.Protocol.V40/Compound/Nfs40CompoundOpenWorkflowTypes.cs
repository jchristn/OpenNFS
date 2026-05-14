namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;

    internal sealed class Nfs40OpenExecutionOutcome
    {
        internal Nfs40OpenExecutionOutcome(
            nfsstat4 status,
            Nfs40OpenStateTransitionResult? transition,
            Nfs40CompoundResolvedHandle? openedHandle,
            NfsPathInfo beforeChangePathInfo,
            NfsPathInfo afterChangePathInfo)
        {
            Status = status;
            Transition = transition;
            OpenedHandle = openedHandle;
            BeforeChangePathInfo = beforeChangePathInfo;
            AfterChangePathInfo = afterChangePathInfo;
        }

        internal NfsPathInfo AfterChangePathInfo { get; }

        internal NfsPathInfo BeforeChangePathInfo { get; }

        internal Nfs40CompoundResolvedHandle? OpenedHandle { get; }

        internal nfsstat4 Status { get; }

        internal Nfs40OpenStateTransitionResult? Transition { get; }
    }

    internal sealed class Nfs40OpenDelegationOutcome
    {
        internal Nfs40OpenDelegationOutcome(
            nfsstat4 status,
            open_delegation4 delegation)
        {
            Status = status;
            Delegation = delegation;
        }

        internal open_delegation4 Delegation { get; }

        internal nfsstat4 Status { get; }
    }

    internal sealed class Nfs40NamedOpenResolutionResult
    {
        internal Nfs40NamedOpenResolutionResult(
            nfsstat4 status,
            Nfs40CompoundResolvedHandle? openedHandle,
            NfsPathInfo? afterChangePathInfo)
        {
            Status = status;
            OpenedHandle = openedHandle;
            AfterChangePathInfo = afterChangePathInfo;
        }

        internal NfsPathInfo? AfterChangePathInfo { get; }

        internal Nfs40CompoundResolvedHandle? OpenedHandle { get; }

        internal nfsstat4 Status { get; }
    }
}
