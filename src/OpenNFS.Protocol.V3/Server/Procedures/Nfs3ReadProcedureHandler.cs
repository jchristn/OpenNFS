namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs3ReadProcedureHandler : Nfs3ProcedureHandlerBase<READ3args, READ3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3ReadProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ, READ3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<READ3res> HandleCoreAsync(READ3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.file, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return new READ3res
                {
                    status = resolution.Status,
                    resfail = new READ3resfail
                    {
                        file_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            if (resolution.PathInfo!.Kind == NfsPathKind.Directory)
            {
                return new READ3res
                {
                    status = nfsstat3.NFS3ERR_ISDIR,
                    resfail = new READ3resfail
                    {
                        file_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            if (resolution.PathInfo.Kind != NfsPathKind.File)
            {
                return new READ3res
                {
                    status = nfsstat3.NFS3ERR_INVAL,
                    resfail = new READ3resfail
                    {
                        file_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            ulong offset = arguments.offset?.Value?.Value ?? 0UL;
            uint count = arguments.count?.Value?.Value ?? 0U;

            NfsReadFileResponse readResponse =
                await _server.Settings.FileSystem.ReadFileAsync(
                    new NfsReadFileRequest(
                        resolution.Target!.SourcePath,
                        offset,
                        count,
                        cancellationToken)).ConfigureAwait(false);

            if (!readResponse.Found)
            {
                return new READ3res
                {
                    status = nfsstat3.NFS3ERR_STALE,
                    resfail = new READ3resfail
                    {
                        file_attributes = resolution.PostOperationAttributes,
                    },
                };
            }

            byte[] data = readResponse.Data.ToArray();
            return new READ3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new READ3resok
                {
                    file_attributes = resolution.PostOperationAttributes,
                    count = new count3
                    {
                        Value = Nfs3MetadataResolver.CreateUInt32((uint)data.Length),
                    },
                    eof = readResponse.EndOfFile,
                    data = data,
                },
            };
        }

        protected override void WriteResult(READ3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
