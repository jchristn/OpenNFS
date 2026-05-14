namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using static Nfs40CompoundClientStateResults;

    internal sealed class Nfs40CompoundClientStateOperations
    {
        private readonly Nfs40StateManager _stateManager;

        internal Nfs40CompoundClientStateOperations(Nfs40StateManager stateManager)
        {
            _stateManager = stateManager;
        }

        internal Nfs40CompoundOperationResult HandleClose(nfs_argop4 operation)
        {
            CLOSE4args? arguments = operation.opclose;
            if (arguments is null)
            {
                return CreateCloseResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40OpenStateTransitionResult transition =
                _stateManager.CloseOpen(arguments.open_stateid, arguments.seqid);
            return CreateCloseResult(transition.Status, transition.StateId);
        }

        internal Nfs40CompoundOperationResult HandleDelegationPurge(nfs_argop4 operation)
        {
            if (operation.opdelegpurge is null)
            {
                return CreateDelegationPurgeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateDelegationPurgeResult(nfsstat4.NFS4ERR_NOTSUPP);
        }

        internal Nfs40CompoundOperationResult HandleOpenConfirm(nfs_argop4 operation)
        {
            OPEN_CONFIRM4args? arguments = operation.opopen_confirm;
            if (arguments is null)
            {
                return CreateOpenConfirmResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40OpenStateTransitionResult transition =
                _stateManager.ConfirmOpen(arguments.open_stateid, arguments.seqid);
            return CreateOpenConfirmResult(
                transition.Status,
                transition.Status == nfsstat4.NFS4_OK
                    ? new OPEN_CONFIRM4resok
                    {
                        open_stateid = transition.StateId,
                    }
                    : null);
        }

        internal Nfs40CompoundOperationResult HandleOpenDowngrade(nfs_argop4 operation)
        {
            OPEN_DOWNGRADE4args? arguments = operation.opopen_downgrade;
            if (arguments is null)
            {
                return CreateOpenDowngradeResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40OpenStateTransitionResult transition =
                _stateManager.DowngradeOpen(
                    arguments.open_stateid,
                    arguments.seqid,
                    arguments.share_access,
                    arguments.share_deny);
            return CreateOpenDowngradeResult(
                transition.Status,
                transition.Status == nfsstat4.NFS4_OK
                    ? new OPEN_DOWNGRADE4resok
                    {
                        open_stateid = transition.StateId,
                    }
                    : null);
        }

        internal Nfs40CompoundOperationResult HandleReleaseLockOwner(nfs_argop4 operation)
        {
            if (operation.oprelease_lockowner?.lock_owner is null)
            {
                return CreateReleaseLockOwnerResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateReleaseLockOwnerResult(nfsstat4.NFS4ERR_NOTSUPP);
        }

        internal Nfs40CompoundOperationResult HandleRenew(nfs_argop4 operation)
        {
            RENEW4args? arguments = operation.oprenew;
            if (arguments is null)
            {
                return CreateRenewResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateRenewResult(_stateManager.RenewClient(arguments.clientid));
        }

        internal Nfs40CompoundOperationResult HandleSetClientId(nfs_argop4 operation)
        {
            SETCLIENTID4args? arguments = operation.opsetclientid;
            if (arguments is null)
            {
                return CreateSetClientIdResult(nfsstat4.NFS4ERR_BADXDR);
            }

            Nfs40ClientRegistrationResult registration =
                _stateManager.RegisterClient(arguments.client, arguments.callback, arguments.callback_ident);
            return CreateSetClientIdResult(
                registration.Status,
                registration.Status == nfsstat4.NFS4_OK
                    ? new SETCLIENTID4resok
                    {
                        clientid = new clientid4
                        {
                            Value = registration.ClientId,
                        },
                        setclientid_confirm = new verifier4
                        {
                            Value = registration.ConfirmationVerifier,
                        },
                    }
                    : null);
        }

        internal Nfs40CompoundOperationResult HandleSetClientIdConfirm(nfs_argop4 operation)
        {
            SETCLIENTID_CONFIRM4args? arguments = operation.opsetclientid_confirm;
            if (arguments is null)
            {
                return CreateSetClientIdConfirmResult(nfsstat4.NFS4ERR_BADXDR);
            }

            return CreateSetClientIdConfirmResult(
                _stateManager.ConfirmClient(arguments.clientid, arguments.setclientid_confirm));
        }
    }
}
