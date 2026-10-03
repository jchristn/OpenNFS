namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundOpenResults;
    using static Nfs40CompoundOpenSupport;

    internal sealed class Nfs40CompoundDelegationOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundDelegationOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _stateManager = stateManager;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleDelegationReturnAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            DELEGRETURN4args? arguments = operation.opdelegreturn;
            if (arguments?.deleg_stateid is null)
            {
                return CreateDelegationReturnResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateDelegationReturnResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateDelegationReturnResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                refreshedHandle.Target.ExportPath,
                refreshedHandle.Target.SourcePath);
            Nfs40DelegationTransitionResult transition =
                _stateManager.ReturnDelegation(arguments.deleg_stateid, fileKey);
            if (transition.Status != nfsstat4.NFS4_OK)
            {
                return CreateDelegationReturnResult(transition.Status);
            }

            if (_server.Capabilities.Delegations is not null && transition.DelegationState is not null)
            {
                await _server.Capabilities.TrackedDelegations!.ReturnDelegationAsync(
                    new NfsReturnDelegationRequest(
                        refreshedHandle.Target.ExportPath,
                        refreshedHandle.Target.SourcePath,
                        transition.DelegationState.ClientId,
                        transition.DelegationState.DelegationKind,
                        transition.DelegationState.StateId.other ?? Array.Empty<byte>(),
                        cancellationToken)).ConfigureAwait(false);
            }

            return CreateDelegationReturnResult(nfsstat4.NFS4_OK);
        }

        internal async Task NotifyOpenConflictRecallsAsync(
            string fileKey,
            ulong clientId,
            uint shareAccess,
            uint shareDeny,
            string exportPath,
            string sourcePath,
            IReadOnlyList<Nfs40DelegationRecallInfo>? recallRequests,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Nfs40DelegationRecallInfo> effectiveRecalls =
                recallRequests is not null && recallRequests.Count > 0
                    ? recallRequests
                    : _stateManager.GetConflictingDelegationRecalls(
                        fileKey,
                        clientId,
                        shareAccess,
                        shareDeny);
            if (effectiveRecalls.Count == 0)
            {
                effectiveRecalls = _stateManager.GetDelegationsForFile(fileKey, clientId);
            }

            await _handleServices.NotifyDelegationRecallsAsync(
                effectiveRecalls,
                exportPath,
                sourcePath,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40OpenDelegationOutcome> TryAcquireOpenDelegationAsync(
            open_claim_type4 claimType,
            OPEN4args arguments,
            Nfs40OpenStateTransitionResult transition,
            Nfs40CompoundResolvedHandle openedHandle,
            CancellationToken cancellationToken)
        {
            if (claimType == open_claim_type4.CLAIM_PREVIOUS
                || _server.Capabilities.Delegations is null)
            {
                return new Nfs40OpenDelegationOutcome(nfsstat4.NFS4_OK, CreateNoDelegation());
            }

            NfsAcquireDelegationResponse delegationDecision =
                await _server.Capabilities.TrackedDelegations!.AcquireDelegationAsync(
                    new NfsAcquireDelegationRequest(
                        openedHandle.Target.ExportPath,
                        openedHandle.Target.SourcePath,
                        openedHandle.PathInfo.Kind,
                        GetClientId(arguments),
                        arguments.share_access,
                        arguments.share_deny,
                        cancellationToken)).ConfigureAwait(false);

            if (delegationDecision.DelegationKind == NfsDelegationKind.None)
            {
                return new Nfs40OpenDelegationOutcome(nfsstat4.NFS4_OK, CreateNoDelegation());
            }

            Nfs40DelegationTransitionResult delegationTransition =
                _stateManager.TryGrantDelegation(transition.StateId, delegationDecision.DelegationKind);
            if (delegationTransition.Status != nfsstat4.NFS4_OK)
            {
                return new Nfs40OpenDelegationOutcome(delegationTransition.Status, CreateNoDelegation());
            }

            if (delegationTransition.DelegationState is null)
            {
                return new Nfs40OpenDelegationOutcome(nfsstat4.NFS4_OK, CreateNoDelegation());
            }

            return new Nfs40OpenDelegationOutcome(
                nfsstat4.NFS4_OK,
                CreateDelegation(delegationTransition.DelegationState));
        }
    }
}
