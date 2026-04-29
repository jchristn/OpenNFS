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

    internal sealed class Nfs3WriteProcedureHandler : Nfs3ProcedureHandlerBase<WRITE3args, WRITE3res>
    {
        private readonly OpenNfsServer _server;
        private readonly Nfs3WriteStateTracker _writeState;

        internal Nfs3WriteProcedureHandler(OpenNfsServer server, Nfs3WriteStateTracker writeState)
            : base((uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE, WRITE3args.ReadFrom)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(writeState);
            _server = server;
            _writeState = writeState;
        }

        protected override async Task<WRITE3res> HandleCoreAsync(WRITE3args arguments, CancellationToken cancellationToken)
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
            byte[] data = arguments.data ?? Array.Empty<byte>();
            if (count != data.Length)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, currentWcc);
            }

            NfsWriteFileResponse writeResponse;
            try
            {
                writeResponse =
                    await _server.Settings.FileSystem.WriteFileAsync(
                        new NfsWriteFileRequest(
                            resolution.Target!.SourcePath,
                            offset,
                            data,
                            Nfs3MetadataResolver.MapWriteStability(arguments.stable ?? stable_how.UNSTABLE),
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
                writeResponse.PathInfo);

            if (!writeResponse.PathInfo.Exists)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_STALE, resultWcc);
            }

            if (writeResponse.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_ISDIR, resultWcc);
            }

            if (writeResponse.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateFailureResult(nfsstat3.NFS3ERR_INVAL, resultWcc);
            }

            return new WRITE3res
            {
                status = nfsstat3.NFS3_OK,
                resok = new WRITE3resok
                {
                    file_wcc = resultWcc,
                    count = new count3
                    {
                        Value = Nfs3MetadataResolver.CreateUInt32(writeResponse.BytesWritten),
                    },
                    committed = Nfs3MetadataResolver.MapWriteStability(writeResponse.CommittedStability),
                    verf = Nfs3MetadataResolver.CreateWriteVerifier(_writeState.GetWriteVerifierBytes()),
                },
            };
        }

        protected override void WriteResult(WRITE3res result, XdrWriter writer)
        {
            result.WriteTo(writer);
        }

        private static WRITE3res CreateFailureResult(nfsstat3 status, wcc_data weakCacheData)
        {
            return new WRITE3res
            {
                status = status,
                resfail = new WRITE3resfail
                {
                    file_wcc = weakCacheData,
                },
            };
        }
    }
}
