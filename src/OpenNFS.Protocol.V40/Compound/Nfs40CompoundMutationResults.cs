namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundMutationResults
    {
        internal static Nfs40CompoundOperationResult CreateCreateResult(
            nfsstat4 status,
            CREATE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_CREATE,
                    opcreate = new CREATE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateSetAttrResult(
            nfsstat4 status,
            bitmap4? attributesSet = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SETATTR,
                    opsetattr = new SETATTR4res
                    {
                        status = status,
                        attrsset = attributesSet ?? Nfs40MutationSupport.CreateEmptyAttributeSet(),
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateLinkResult(
            nfsstat4 status,
            LINK4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LINK,
                    oplink = new LINK4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateRemoveResult(
            nfsstat4 status,
            REMOVE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_REMOVE,
                    opremove = new REMOVE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateRenameResult(
            nfsstat4 status,
            RENAME4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RENAME,
                    oprename = new RENAME4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }
    }
}
