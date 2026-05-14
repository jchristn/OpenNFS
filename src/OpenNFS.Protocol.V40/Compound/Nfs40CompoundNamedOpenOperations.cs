namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using static Nfs40CompoundOpenSupport;

    internal sealed class Nfs40CompoundNamedOpenOperations
    {
        private readonly Nfs40CompoundDelegationOperations _delegationOperations;
        private readonly Nfs40CompoundNamedOpenTargetOperations _targetOperations;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundNamedOpenOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices,
            Nfs40CompoundDelegationOperations delegationOperations)
        {
            _server = server;
            _stateManager = stateManager;
            _delegationOperations = delegationOperations;
            _targetOperations = new Nfs40CompoundNamedOpenTargetOperations(server, handleServices);
        }

        internal async Task<Nfs40OpenExecutionOutcome> HandleNamedOpenAsync(
            OPEN4args arguments,
            Nfs40CompoundResolvedHandle refreshedHandle,
            string entryName,
            opentype4 openType,
            CancellationToken cancellationToken)
        {
            NfsPathInfo beforeChangePathInfo = refreshedHandle.PathInfo;
            NfsPathInfo afterChangePathInfo = beforeChangePathInfo;

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateFailedOutcome(
                    nfsstat4.NFS4ERR_NOTDIR,
                    beforeChangePathInfo,
                    afterChangePathInfo);
            }

            string targetSourcePath = OpenNFS.Server.Internal.NfsSourcePath.Combine(
                refreshedHandle.Target.SourcePath,
                entryName);
            string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                targetSourcePath);

            nfsstat4 validationStatus = _stateManager.ValidateOpen(
                arguments.owner?.clientid,
                arguments.owner?.owner ?? Array.Empty<byte>(),
                arguments.seqid,
                fileKey,
                arguments.share_access,
                arguments.share_deny);
            if (validationStatus != nfsstat4.NFS4_OK)
            {
                if (validationStatus == nfsstat4.NFS4ERR_DELAY
                    && _server.Capabilities.Delegations is not null)
                {
                    await _delegationOperations.NotifyOpenConflictRecallsAsync(
                        fileKey,
                        GetClientId(arguments),
                        arguments.share_access,
                        arguments.share_deny,
                        refreshedHandle.Target.ExportPath,
                        targetSourcePath,
                        null,
                        cancellationToken).ConfigureAwait(false);
                }

                return CreateFailedOutcome(
                    validationStatus,
                    beforeChangePathInfo,
                    afterChangePathInfo);
            }

            Nfs40NamedOpenResolutionResult targetResolution =
                await _targetOperations.ResolveNamedOpenTargetAsync(
                    arguments,
                    refreshedHandle,
                    entryName,
                    openType,
                    cancellationToken).ConfigureAwait(false);
            if (targetResolution.Status != nfsstat4.NFS4_OK || targetResolution.OpenedHandle is null)
            {
                return CreateFailedOutcome(
                    targetResolution.Status,
                    beforeChangePathInfo,
                    targetResolution.AfterChangePathInfo ?? afterChangePathInfo);
            }

            Nfs40CompoundResolvedHandle openedHandle = targetResolution.OpenedHandle;
            afterChangePathInfo = targetResolution.AfterChangePathInfo ?? afterChangePathInfo;

            Nfs40OpenStateTransitionResult transition = _stateManager.Open(
                arguments.owner?.clientid,
                arguments.owner?.owner ?? Array.Empty<byte>(),
                arguments.seqid,
                fileKey,
                arguments.share_access,
                arguments.share_deny);
            if (transition.Status != nfsstat4.NFS4_OK)
            {
                if (transition.Status == nfsstat4.NFS4ERR_DELAY
                    && openedHandle is not null
                    && _server.Capabilities.Delegations is not null)
                {
                    await _delegationOperations.NotifyOpenConflictRecallsAsync(
                        fileKey,
                        GetClientId(arguments),
                        arguments.share_access,
                        arguments.share_deny,
                        openedHandle.Target.ExportPath,
                        openedHandle.Target.SourcePath,
                        transition.RecallRequests,
                        cancellationToken).ConfigureAwait(false);
                }

                return CreateFailedOutcome(
                    transition.Status,
                    beforeChangePathInfo,
                    afterChangePathInfo);
            }

            return new Nfs40OpenExecutionOutcome(
                nfsstat4.NFS4_OK,
                transition,
                openedHandle,
                beforeChangePathInfo,
                afterChangePathInfo);
        }

        private static Nfs40OpenExecutionOutcome CreateFailedOutcome(
            nfsstat4 status,
            NfsPathInfo beforeChangePathInfo,
            NfsPathInfo afterChangePathInfo)
        {
            return new Nfs40OpenExecutionOutcome(
                status,
                null,
                null,
                beforeChangePathInfo,
                afterChangePathInfo);
        }
    }
}
