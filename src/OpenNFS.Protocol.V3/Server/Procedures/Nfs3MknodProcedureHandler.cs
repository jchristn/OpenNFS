namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3MknodProcedureHandler : Nfs3ProcedureHandlerBase<MKNOD3args, MKNOD3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3MknodProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_MKNOD, MKNOD3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<MKNOD3res> HandleCoreAsync(MKNOD3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.where?.dir, cancellationToken).ConfigureAwait(false);

            if (directoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(
                    directoryResolution.Status,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null));
            }

            wcc_data currentDirectoryWcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                directoryResolution.Target,
                directoryResolution.PathInfo,
                directoryResolution.PathInfo);

            if (directoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTDIR, currentDirectoryWcc);
            }

            string entryName = arguments.where?.name?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOENT, currentDirectoryWcc);
            }

            NfsPathInfo existingChildPathInfo =
                await Nfs3DirectoryMutationSupport.LookupChildPathInfoAsync(
                    _server,
                    directoryResolution.Target!,
                    entryName,
                    cancellationToken).ConfigureAwait(false);

            if (existingChildPathInfo.Exists)
            {
                return CreateFailureResult(
                    existingChildPathInfo.Kind == NfsPathKind.Other ? nfsstat3.NFS3ERR_NOTSUPP : nfsstat3.NFS3ERR_EXIST,
                    currentDirectoryWcc);
            }

            ftype3? requestedType = arguments.what?.type;
            if (requestedType is null
                || (requestedType != ftype3.NF3CHR
                    && requestedType != ftype3.NF3BLK
                    && requestedType != ftype3.NF3SOCK
                    && requestedType != ftype3.NF3FIFO))
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_BADTYPE, currentDirectoryWcc);
            }

            return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, currentDirectoryWcc);
        }

        protected override void WriteResult(MKNOD3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static MKNOD3res CreateFailureResult(nfsstat3 status, wcc_data directoryWeakCacheConsistency)
        {
            return new MKNOD3res
            {
                status = status,
                resfail = new MKNOD3resfail
                {
                    dir_wcc = directoryWeakCacheConsistency,
                },
            };
        }
    }
}
