namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3GetAttrProcedureHandler : Nfs3ProcedureHandlerBase<GETATTR3args, GETATTR3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3GetAttrProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_GETATTR, GETATTR3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<GETATTR3res> HandleCoreAsync(GETATTR3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new GETATTR3res
                {
                    status = resolution.Status,
                };
            }

            return new GETATTR3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new GETATTR3resok
                {
                    obj_attributes = resolution.Attributes,
                },
            };
        }

        protected override void WriteResult(GETATTR3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
