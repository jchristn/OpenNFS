namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs3ReadLinkProcedureHandler : Nfs3ProcedureHandlerBase<READLINK3args, READLINK3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3ReadLinkProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READLINK, READLINK3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<READLINK3res> HandleCoreAsync(READLINK3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.symlink, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(resolution.Status, resolution.PostOperationAttributes);
            }

            if (resolution.PathInfo!.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, resolution.PostOperationAttributes);
            }

            NfsReadSymbolicLinkResponse readResponse;
            try
            {
                readResponse =
                    await _server.Settings.FileSystem.ReadSymbolicLinkAsync(
                        new NfsReadSymbolicLinkRequest(
                            resolution.Target!.SourcePath,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ACCES, resolution.PostOperationAttributes);
            }
            catch (IOException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_IO, resolution.PostOperationAttributes);
            }
            catch (NotSupportedException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_NOTSUPP, resolution.PostOperationAttributes);
            }
            catch (ArgumentException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, resolution.PostOperationAttributes);
            }

            post_op_attr currentAttributes = Nfs3MetadataResolver.CreatePostOperationAttributes(resolution.Target, readResponse.PathInfo);
            if (!readResponse.PathInfo.Exists)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_STALE, currentAttributes);
            }

            if (readResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentAttributes);
            }

            return new READLINK3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new READLINK3resok
                {
                    symlink_attributes = currentAttributes,
                    data = new nfspath3
                    {
                        Value = readResponse.TargetPath,
                    },
                },
            };
        }

        protected override void WriteResult(READLINK3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static READLINK3res CreateFailureResult(nfsstat3 status, post_op_attr symlinkAttributes)
        {
            return new READLINK3res
            {
                status = status,
                resfail = new READLINK3resfail
                {
                    symlink_attributes = symlinkAttributes,
                },
            };
        }
    }
}
