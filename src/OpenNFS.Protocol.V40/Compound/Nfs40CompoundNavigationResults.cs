namespace OpenNFS.Protocol.V40.Compound
{
    using OpenNFS.Protocol.V40.Generated;

    internal static class Nfs40CompoundNavigationResults
    {
        internal static Nfs40CompoundOperationResult CreateGetFileHandleResult(
            nfsstat4 status,
            Nfs40CompoundResolvedHandle? resolvedHandle = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_GETFH,
                    opgetfh = new GETFH4res
                    {
                        status = status,
                        resok4 = status == nfsstat4.NFS4_OK && resolvedHandle is not null
                            ? new GETFH4resok
                            {
                                @object = new nfs_fh4
                                {
                                    Value = resolvedHandle.FileHandle.ToArray(),
                                },
                            }
                            : null,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateLookupParentResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUPP,
                    oplookupp = new LOOKUPP4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateLookupResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_LOOKUP,
                    oplookup = new LOOKUP4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreatePutFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTFH,
                    opputfh = new PUTFH4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreatePutPublicFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTPUBFH,
                    opputpubfh = new PUTPUBFH4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreatePutRootFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_PUTROOTFH,
                    opputrootfh = new PUTROOTFH4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateRestoreFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_RESTOREFH,
                    oprestorefh = new RESTOREFH4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateSaveFileHandleResult(nfsstat4 status)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SAVEFH,
                    opsavefh = new SAVEFH4res
                    {
                        status = status,
                    },
                });
        }

        internal static Nfs40CompoundOperationResult CreateSecurityInfoResult(
            nfsstat4 status,
            SECINFO4resok? successPayload = null)
        {
            return new Nfs40CompoundOperationResult(
                status,
                new nfs_resop4
                {
                    resop = nfs_opnum4.OP_SECINFO,
                    opsecinfo = new SECINFO4res
                    {
                        status = status,
                        resok4 = successPayload,
                    },
                });
        }
    }
}
