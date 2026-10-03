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

    internal sealed class Nfs3CommitProcedureHandler : Nfs3ProcedureHandlerBase<COMMIT3args, COMMIT3res>
    {
        private readonly OpenNfsServer _server;
        private readonly Nfs3WriteStateTracker _writeState;

        internal Nfs3CommitProcedureHandler(OpenNfsServer server, Nfs3WriteStateTracker writeState)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_COMMIT, COMMIT3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(writeState);
            _server = server;
            _writeState = writeState;
        }

        protected override async Task<COMMIT3res> HandleCoreAsync(COMMIT3args arguments, CancellationToken cancellationToken)
        {
            Nfs3ObjectResolution resolution =
                await Nfs3MetadataResolver.ResolveAsync(_server, arguments.file, cancellationToken).ConfigureAwait(false);

            if (resolution.Status != nfsstat3.NFS3_OK)
            {
                return CreateFailureResult(
                    resolution.Status,
                    Nfs3MetadataResolver.CreateWeakCacheConsistencyData(null, null, null));
            }

            wcc_data currentWcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                resolution.Target,
                resolution.PathInfo,
                resolution.PathInfo);

            if (resolution.PathInfo!.Kind == NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, currentWcc);
            }

            if (resolution.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentWcc);
            }

            ulong offset = arguments.offset?.Value?.Value ?? 0UL;
            uint count = arguments.count?.Value?.Value ?? 0U;

            NfsCommitFileResponse commitResponse;
            try
            {
                commitResponse =
                    await _server.Settings.InstrumentedFileSystem.CommitFileAsync(
                        new NfsCommitFileRequest(
                            resolution.Target!.SourcePath,
                            offset,
                            count,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ACCES, currentWcc);
            }
            catch (IOException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_IO, currentWcc);
            }
            catch (NotSupportedException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentWcc);
            }
            catch (OverflowException)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_FBIG, currentWcc);
            }

            wcc_data resultWcc = Nfs3MetadataResolver.CreateWeakCacheConsistencyData(
                resolution.Target,
                resolution.PathInfo,
                commitResponse.PathInfo);

            if (!commitResponse.PathInfo.Exists)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_STALE, resultWcc);
            }

            if (commitResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, resultWcc);
            }

            if (commitResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, resultWcc);
            }

            return new COMMIT3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new COMMIT3resok
                {
                    file_wcc = resultWcc,
                    verf = Nfs3MetadataResolver.CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                },
            };
        }

        protected override void WriteResult(COMMIT3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static COMMIT3res CreateFailureResult(nfsstat3 status, wcc_data weakCacheData)
        {
            return new COMMIT3res
            {
                status = status,
                resfail = new COMMIT3resfail
                {
                    file_wcc = weakCacheData,
                },
            };
        }
    }
}
