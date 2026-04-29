namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3SetAttrProcedureHandler : Nfs3ProcedureHandlerBase<SETATTR3args, SETATTR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3SetAttrProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_SETATTR, SETATTR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<SETATTR3res> HandleCoreAsync(SETATTR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(
                    resolution.Status,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null));
            }

            return CreateFailureResult(
                nfsstat3.NFS3ERR_NOTSUPP,
                Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                    resolution.Target,
                    resolution.PathInfo,
                    resolution.PathInfo));
        }

        protected override void WriteResult(SETATTR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static SETATTR3res CreateFailureResult(nfsstat3 status, wcc_data objectWeakCacheConsistency)
        {
            return new SETATTR3res
            {
                status = status,
                resfail = new SETATTR3resfail
                {
                    obj_wcc = objectWeakCacheConsistency,
                },
            };
        }
    }
}
