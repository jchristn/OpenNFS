namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal sealed class Nfs3FsInfoProcedureHandler : Nfs3ProcedureHandlerBase<FSINFO3args, FSINFO3res>
    {
        private const uint DefaultTransferMaximum = 1048576;
        private const uint DefaultTransferPreferred = 65536;
        private const uint DefaultTransferMultiple = 4096;
        private const uint DefaultDirectoryPreferred = 32768;

        private readonly OpenNfsServer _server;

        internal Nfs3FsInfoProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_FSINFO, FSINFO3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<FSINFO3res> HandleCoreAsync(FSINFO3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.fsroot, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new FSINFO3res
                {
                    status = resolution.Status,
                    resfail = new FSINFO3resfail
                    {
                        obj_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            return new FSINFO3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new FSINFO3resok
                {
                    obj_attributes = resolution.PostOperationAttributes,
                    rtmax = Nfs3MetadataResolver.CreateUInt32(DefaultTransferMaximum),
                    rtpref = Nfs3MetadataResolver.CreateUInt32(DefaultTransferPreferred),
                    rtmult = Nfs3MetadataResolver.CreateUInt32(DefaultTransferMultiple),
                    wtmax = Nfs3MetadataResolver.CreateUInt32(DefaultTransferMaximum),
                    wtpref = Nfs3MetadataResolver.CreateUInt32(DefaultTransferPreferred),
                    wtmult = Nfs3MetadataResolver.CreateUInt32(DefaultTransferMultiple),
                    dtpref = Nfs3MetadataResolver.CreateUInt32(DefaultDirectoryPreferred),
                    maxfilesize = Nfs3MetadataResolver.CreateSize(ulong.MaxValue),
                    time_delta = Nfs3MetadataResolver.CreateTime(0, 0),
                    properties = Nfs3MetadataResolver.CreateUInt32((uint)(
                        Nfs3Constants.FSF3_LINK
                        | Nfs3Constants.FSF3_SYMLINK
                        | Nfs3Constants.FSF3_HOMOGENEOUS)),
                },
            };
        }

        protected override void WriteResult(FSINFO3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
