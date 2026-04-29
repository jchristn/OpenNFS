namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3PathConfProcedureHandler : Nfs3ProcedureHandlerBase<PATHCONF3args, PATHCONF3res>
    {
        private const uint DefaultLinkMaximum = 1024;
        private const uint DefaultNameMaximum = 255;

        private readonly OpenNfsServer _server;

        internal Nfs3PathConfProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_PATHCONF, PATHCONF3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<PATHCONF3res> HandleCoreAsync(PATHCONF3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.@object, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new PATHCONF3res
                {
                    status = resolution.Status,
                    resfail = new PATHCONF3resfail
                    {
                        obj_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            return new PATHCONF3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new PATHCONF3resok
                {
                    obj_attributes = resolution.PostOperationAttributes,
                    linkmax = Nfs3MetadataResolver.CreateUInt32(DefaultLinkMaximum),
                    name_max = Nfs3MetadataResolver.CreateUInt32(DefaultNameMaximum),
                    no_trunc = true,
                    chown_restricted = true,
                    case_insensitive = false,
                    case_preserving = true,
                },
            };
        }

        protected override void WriteResult(PATHCONF3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
