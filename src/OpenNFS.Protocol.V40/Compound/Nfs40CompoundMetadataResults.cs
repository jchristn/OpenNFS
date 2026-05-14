namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundMetadataResults
    {
        internal static Nfs40CompoundOperationResult CreateAccessResult(
            nfsstat4 status,
            ACCESS4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_ACCESS,
                    opaccess = new ACCESS4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateGetAttrResult(
            nfsstat4 status,
            fattr4? attributes = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_GETATTR,
                    opgetattr = new GETATTR4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK
                            ? new GETATTR4resok
                            {
                                obj_attributes = attributes,
                            }
                            : null,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateNotVerifyResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_NVERIFY,
                    opnverify = new NVERIFY4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateVerifyResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_VERIFY,
                    opverify = new VERIFY4res
                    {
                        status = status,
                    },
                });
        }
    }
}
