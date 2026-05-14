namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundIoResults
    {
        internal static Nfs40CompoundOperationResult CreateCommitResult(
            nfsstat4 status,
            COMMIT4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_COMMIT,
                    opcommit = new COMMIT4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateReadDirectoryResult(
            nfsstat4 status,
            READDIR4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READDIR,
                    opreaddir = new READDIR4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateReadLinkResult(
            nfsstat4 status,
            READLINK4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READLINK,
                    opreadlink = new READLINK4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateReadResult(
            nfsstat4 status,
            READ4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_READ,
                    opread = new READ4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateWriteResult(
            nfsstat4 status,
            WRITE4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_WRITE,
                    opwrite = new WRITE4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }
    }
}
