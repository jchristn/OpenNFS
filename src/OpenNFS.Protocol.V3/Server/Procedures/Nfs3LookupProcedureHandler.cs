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

    internal sealed class Nfs3LookupProcedureHandler : Nfs3ProcedureHandlerBase<LOOKUP3args, LOOKUP3res>
    {
        private readonly OpenNfsServer _server;

        internal Nfs3LookupProcedureHandler(OpenNfsServer server)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_LOOKUP, LOOKUP3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            _server = server;
        }

        protected override async Task<LOOKUP3res> HandleCoreAsync(LOOKUP3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution directoryResolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.what?.dir, cancellationToken).ConfigureAwait(false);

            if (directoryResolution.Status != nfsstat3.NFS3_OK)
            {
                return new LOOKUP3res
                {
                    status = directoryResolution.Status,
                    resfail = new LOOKUP3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            if (directoryResolution.PathInfo!.Kind != NfsPathKind.Directory)
            {
                return new LOOKUP3res
                {
                    status = nfsstat3.NFS3ERR_NOTDIR,
                    resfail = new LOOKUP3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            string entryName = arguments.what?.name?.Value ?? string.Empty;
            if (entryName.Length < 1)
            {
                return new LOOKUP3res
                {
                    status = nfsstat3.NFS3ERR_NOENT,
                    resfail = new LOOKUP3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            NfsLookupPathResponse lookupResponse =
                await _server.Settings.FileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(
                        directoryResolution.Target!.SourcePath,
                        entryName,
                        cancellationToken)).ConfigureAwait(false);

            NfsPathInfo childPathInfo = lookupResponse.PathInfo;
            if (!childPathInfo.Exists)
            {
                return new LOOKUP3res
                {
                    status = nfsstat3.NFS3ERR_NOENT,
                    resfail = new LOOKUP3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return new LOOKUP3res
                {
                    status = nfsstat3.NFS3ERR_NOTSUPP,
                    resfail = new LOOKUP3resfail
                    {
                        dir_attributes = directoryResolution.PostOperationAttributes,
                    },
                };
            }

            NfsFileHandleTarget childTarget = new NfsFileHandleTarget(
                directoryResolution.Target.ExportPath,
                childPathInfo.Path);
            NfsFileHandle childFileHandle =
                await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
            fattr3 childAttributes = Nfs3MetadataResolver.CreateAttributes(childTarget, childPathInfo);

            return new LOOKUP3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new LOOKUP3resok
                {
                    @object = new nfs_fh3
                    {
                        data = childFileHandle.ToArray(),
                    },
                    obj_attributes = new post_op_attr
                    {
                        attributes_follow = true,
                        attributes = childAttributes,
                    },
                    dir_attributes = directoryResolution.PostOperationAttributes,
                },
            };
        }

        protected override void WriteResult(LOOKUP3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }
    }
}
