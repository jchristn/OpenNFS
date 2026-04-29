namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3AccessProcedureHandler : Nfs3ProcedureHandlerBase<ACCESS3args, ACCESS3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3AccessProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_ACCESS, ACCESS3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<ACCESS3res> HandleCoreAsync(ACCESS3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new ACCESS3res
                {
                    status = resolution.Status,
                    resfail = new ACCESS3resfail
                    {
                        obj_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            uint requestedAccess = arguments.access?.Value ?? 0;
            uint grantedAccess =
                requestedAccess & Nfs3MetadataResolver.GetSupportedAccessMask(resolution.PathInfo!.Kind);

            return new ACCESS3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new ACCESS3resok
                {
                    obj_attributes = resolution.PostOperationAttributes,
                    access = Nfs3MetadataResolver.CreateUInt32(grantedAccess),
                },
            };
        }

        protected override void WriteResult(ACCESS3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
