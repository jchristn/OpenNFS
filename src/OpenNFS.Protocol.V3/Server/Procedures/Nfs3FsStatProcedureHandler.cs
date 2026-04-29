namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3FsStatProcedureHandler : Nfs3ProcedureHandlerBase<FSSTAT3args, FSSTAT3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3FsStatProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSSTAT, FSSTAT3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<FSSTAT3res> HandleCoreAsync(FSSTAT3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.fsroot, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new FSSTAT3res
                {
                    status = resolution.Status,
                    resfail = new FSSTAT3resfail
                    {
                        obj_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            return new FSSTAT3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new FSSTAT3resok
                {
                    obj_attributes = resolution.PostOperationAttributes,
                    tbytes = Nfs3MetadataResolver.CreateSize(0),
                    fbytes = Nfs3MetadataResolver.CreateSize(0),
                    abytes = Nfs3MetadataResolver.CreateSize(0),
                    tfiles = Nfs3MetadataResolver.CreateSize(0),
                    ffiles = Nfs3MetadataResolver.CreateSize(0),
                    afiles = Nfs3MetadataResolver.CreateSize(0),
                    invarsec = Nfs3MetadataResolver.CreateUInt32(0),
                },
            };
        }

        protected override void WriteResult(FSSTAT3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
