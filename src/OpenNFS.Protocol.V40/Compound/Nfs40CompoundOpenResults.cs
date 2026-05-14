namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundOpenResults
    {
        internal static Nfs40CompoundOperationResult CreateDelegationReturnResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_DELEGRETURN,
                    opdelegreturn = new DELEGRETURN4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateOpenAttributeResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPENATTR,
                    opopenattr = new OPENATTR4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateOpenResult(
            nfsstat4 status,
            OPEN4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_OPEN,
                    opopen = new OPEN4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }
    }
}
