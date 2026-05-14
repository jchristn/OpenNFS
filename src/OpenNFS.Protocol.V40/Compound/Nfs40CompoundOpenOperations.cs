namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using static Nfs40CompoundOpenResults;

    internal sealed class Nfs40CompoundOpenOperations
    {
        private readonly Nfs40CompoundDelegationOperations _delegationOperations;
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly Nfs40CompoundNamedOpenOperations _namedOpenOperations;
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundOpenOperations(
            OpenNfsServer server,
            Nfs40StateManager stateManager,
            Nfs40CompoundHandleServices handleServices)
        {
            _stateManager = stateManager;
            _handleServices = handleServices;
            _delegationOperations = new Nfs40CompoundDelegationOperations(server, stateManager, handleServices);
            _namedOpenOperations = new Nfs40CompoundNamedOpenOperations(
                server,
                stateManager,
                handleServices,
                _delegationOperations);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleDelegationReturnAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            return await _delegationOperations.HandleDelegationReturnAsync(
                operation,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleOpenAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            OPEN4args? arguments = operation.opopen;
            if (arguments is null)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateOpenResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            open_claim_type4? claimType = arguments.claim?.claim;
            if (!claimType.HasValue)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            string entryName;
            switch (claimType.Value)
            {
                case open_claim_type4.CLAIM_NULL:
                    if (!Nfs40MutationSupport.TryReadComponent(arguments.claim?.file, out entryName, out nfsstat4 claimStatus))
                    {
                        return CreateOpenResult(claimStatus);
                    }

                    break;

                case open_claim_type4.CLAIM_PREVIOUS:
                    entryName = string.Empty;
                    break;

                case open_claim_type4.CLAIM_DELEGATE_CUR:
                case open_claim_type4.CLAIM_DELEGATE_PREV:
                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);

                default:
                    return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            opentype4? openType = arguments.openhow?.opentype;
            if (!openType.HasValue)
            {
                return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40OpenExecutionOutcome openOutcome;

            switch (claimType.Value)
            {
                case open_claim_type4.CLAIM_NULL:
                    openOutcome = await _namedOpenOperations.HandleNamedOpenAsync(
                        arguments,
                        refreshedHandle,
                        entryName,
                        openType.Value,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case open_claim_type4.CLAIM_PREVIOUS:
                {
                    if (arguments.claim?.delegate_type is not open_delegation_type4.OPEN_DELEGATE_NONE)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                    }

                    if (openType.Value != opentype4.OPEN4_NOCREATE)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_INVAL);
                    }

                    if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_ISDIR);
                    }

                    if (refreshedHandle.PathInfo.Kind == NfsPathKind.SymbolicLink)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_SYMLINK);
                    }

                    if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
                    {
                        return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);
                    }

                    string fileKey = Nfs40CompoundFileKeySupport.BuildOpenFileKey(
                        refreshedHandle.Target.ExportPath,
                        refreshedHandle.Target.SourcePath);
                    Nfs40OpenStateTransitionResult transition = _stateManager.ReclaimOpen(
                        arguments.owner?.clientid,
                        arguments.owner?.owner ?? Array.Empty<byte>(),
                        arguments.seqid,
                        fileKey,
                        arguments.share_access,
                        arguments.share_deny);
                    if (transition.Status != nfsstat4.NFS4_OK)
                    {
                        return CreateOpenResult(transition.Status);
                    }

                    openOutcome = new Nfs40OpenExecutionOutcome(
                        nfsstat4.NFS4_OK,
                        transition,
                        refreshedHandle,
                        refreshedHandle.PathInfo,
                        refreshedHandle.PathInfo);
                    break;
                }

                case open_claim_type4.CLAIM_DELEGATE_CUR:
                case open_claim_type4.CLAIM_DELEGATE_PREV:
                    return CreateOpenResult(nfsstat4.NFS4ERR_NOTSUPP);

                default:
                    return CreateOpenResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (openOutcome.Status != nfsstat4.NFS4_OK
                || openOutcome.OpenedHandle is null
                || openOutcome.Transition is null)
            {
                return CreateOpenResult(openOutcome.Status);
            }

            Nfs40OpenDelegationOutcome delegationOutcome =
                await _delegationOperations.TryAcquireOpenDelegationAsync(
                    claimType.Value,
                    arguments,
                    openOutcome.Transition,
                    openOutcome.OpenedHandle,
                    cancellationToken).ConfigureAwait(false);
            if (delegationOutcome.Status != nfsstat4.NFS4_OK)
            {
                return CreateOpenResult(delegationOutcome.Status);
            }

            state.SetCurrentHandle(openOutcome.OpenedHandle);
            return CreateOpenResult(
                nfsstat4.NFS4_OK,
                new OPEN4resok
                {
                    stateid = openOutcome.Transition.StateId,
                    cinfo = Nfs40MutationSupport.CreateChangeInfo(
                        openOutcome.BeforeChangePathInfo,
                        openOutcome.AfterChangePathInfo),
                    rflags = openOutcome.Transition.RequiresConfirmation
                        ? (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM
                        : 0U,
                    attrset = Nfs40MutationSupport.CreateEmptyAttributeSet(),
                    delegation = delegationOutcome.Delegation,
                });
        }

        internal async Task<Nfs40CompoundOperationResult> HandleOpenAttributeAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (operation.opopenattr is null)
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateOpenAttributeResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateOpenAttributeResult(nfsstat4.NFS4ERR_BADTYPE);
            }

            return CreateOpenAttributeResult(nfsstat4.NFS4ERR_NOTSUPP);
        }
    }
}
