namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundIoResults;

    internal sealed class Nfs40CompoundReadIoOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundReadIoOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleReadAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            READ4args? arguments = operation.opread;
            if (arguments?.count is null || arguments.offset is null || arguments.stateid?.other is null)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateReadResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Directory)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_ISDIR);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.File)
            {
                return CreateReadResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsReadFileResponse readResponse =
                await _server.Settings.FileSystem.ReadFileAsync(
                    new NfsReadFileRequest(
                        refreshedHandle.Target.SourcePath,
                        arguments.offset.Value,
                        arguments.count.Value,
                        cancellationToken)).ConfigureAwait(false);

            if (!readResponse.Found)
            {
                return CreateReadResult(nfsstat4.NFS4ERR_STALE);
            }

            byte[] data = readResponse.Data.ToArray();
            return CreateReadResult(
                nfsstat4.NFS4_OK,
                new READ4resok
                {
                    eof = readResponse.EndOfFile,
                    data = data,
                });
        }

        internal async Task<Nfs40CompoundOperationResult> HandleReadLinkAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateReadLinkResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateReadLinkResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_INVAL);
            }

            NfsReadSymbolicLinkResponse readResponse;
            try
            {
                readResponse =
                    await _server.Settings.FileSystem.ReadSymbolicLinkAsync(
                        new NfsReadSymbolicLinkRequest(
                            refreshedHandle.Target.SourcePath,
                            cancellationToken)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_ACCESS);
            }
            catch (IOException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_IO);
            }
            catch (NotSupportedException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_NOTSUPP);
            }
            catch (ArgumentException)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_INVAL);
            }

            if (!readResponse.PathInfo.Exists)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_STALE);
            }

            if (readResponse.PathInfo.Kind != NfsPathKind.SymbolicLink)
            {
                return CreateReadLinkResult(nfsstat4.NFS4ERR_INVAL);
            }

            return CreateReadLinkResult(
                nfsstat4.NFS4_OK,
                new READLINK4resok
                {
                    link = new linktext4
                    {
                        Value = Encoding.UTF8.GetBytes(readResponse.TargetPath),
                    },
                });
        }
    }
}
