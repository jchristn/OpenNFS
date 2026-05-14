namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundClientStateResults
    {
        internal static Nfs40CompoundOperationResult CreateDelegationPurgeResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_DELEGPURGE,
                    opdelegpurge = new DELEGPURGE4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateCloseResult(
            nfsstat4 status,
            stateid4? stateId = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CLOSE,
                    opclose = new CLOSE4res
                    {
                        status = status,
                        open_stateid = status == nfsstat4.NFS4_OK ? stateId : null,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateOpenConfirmResult(
            nfsstat4 status,
            OPEN_CONFIRM4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN_CONFIRM,
                    opopen_confirm = new OPEN_CONFIRM4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateOpenDowngradeResult(
            nfsstat4 status,
            OPEN_DOWNGRADE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN_DOWNGRADE,
                    opopen_downgrade = new OPEN_DOWNGRADE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateReleaseLockOwnerResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RELEASE_LOCKOWNER,
                    oprelease_lockowner = new RELEASE_LOCKOWNER4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateRenewResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RENEW,
                    oprenew = new RENEW4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateSetClientIdConfirmResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                    opsetclientid_confirm = new SETCLIENTID_CONFIRM4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateSetClientIdResult(
            nfsstat4 status,
            SETCLIENTID4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETCLIENTID,
                    opsetclientid = new SETCLIENTID4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }
    }
}
