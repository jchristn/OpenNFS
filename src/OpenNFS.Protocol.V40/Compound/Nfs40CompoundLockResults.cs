namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundLockResults
    {
        internal static Nfs40CompoundOperationResult CreateLockResult(
            nfsstat4 status,
            LOCK4resok? successPayload = null,
            LOCK4denied? deniedPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCK,
                    oplock = new LOCK4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK ? successPayload : null,
                        denied = status == nfsstat4.NFS4ERR_DENIED ? deniedPayload : null,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateLockTestResult(
            nfsstat4 status,
            LOCK4denied? deniedPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCKT,
                    oplockt = new LOCKT4res
                    {
                        status = status,
                        denied = status == nfsstat4.NFS4ERR_DENIED ? deniedPayload : null,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateLockUnlockResult(
            nfsstat4 status,
            stateid4? stateId = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOCKU,
                    oplocku = new LOCKU4res
                    {
                        status = status,
                        lock_stateid = status == nfsstat4.NFS4_OK ? stateId : null,
                    },
                });
        }
    }
}
