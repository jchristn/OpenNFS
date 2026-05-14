namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundCommonResults
    {
        internal static Nfs40CompoundOperationResult CreateIllegalOperationResult()
        {
            return new Nfs40CompoundOperationResult(
                nfsstat4.NFS4ERR_OP_ILLEGAL,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_ILLEGAL,
                    opillegal = new ILLEGAL4res
                    {
                        status = nfsstat4.NFS4ERR_OP_ILLEGAL,
                    },
                });
        }
    }
}
