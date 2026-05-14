namespace OpenNFS.Protocol.V40.Compound
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundIoResults;

    internal sealed class Nfs40CompoundDirectoryReadOperations
    {
        private readonly Nfs40CompoundDirectoryReadResponseBuilder _responseBuilder;
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundDirectoryReadOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
            _responseBuilder = new Nfs40CompoundDirectoryReadResponseBuilder(server);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleReadDirectoryAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            READDIR4args? arguments = operation.opreaddir;
            if (arguments?.cookieverf?.Value is null || arguments.maxcount is null)
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateReadDirectoryResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateReadDirectoryResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                        ? nfsstat4.NFS4ERR_NOTSUPP
                        : nfsstat4.NFS4ERR_NOTDIR);
            }

            NfsReadDirectoryResponse directoryResponse =
                await _server.Settings.FileSystem.ReadDirectoryAsync(
                    new NfsReadDirectoryRequest(
                        refreshedHandle.Target.SourcePath,
                        cancellationToken)).ConfigureAwait(false);

            IReadOnlyList<NfsDirectoryEntryInfo> directoryEntries = directoryResponse.Entries;
            byte[] currentVerifier = Nfs40DirectoryListingSupport.CreateCookieVerifier(
                refreshedHandle.Target.SourcePath,
                directoryEntries);
            ulong requestedCookie = Nfs40DirectoryListingSupport.ReadCookie(arguments.cookie);
            byte[] suppliedVerifier = Nfs40DirectoryListingSupport.ReadCookieVerifier(arguments.cookieverf);

            if (!Nfs40DirectoryListingSupport.IsCookieValid(
                requestedCookie,
                suppliedVerifier,
                currentVerifier,
                directoryEntries.Count))
            {
                return CreateReadDirectoryResult(nfsstat4.NFS4ERR_BAD_COOKIE);
            }

            Nfs40DirectoryReadPayloadResult payloadResult =
                await _responseBuilder.TryBuildPayloadAsync(
                    refreshedHandle.Target.ExportPath,
                    arguments.attr_request,
                    directoryEntries,
                    currentVerifier,
                    requestedCookie,
                    arguments.maxcount.Value,
                    cancellationToken).ConfigureAwait(false);
            if (payloadResult.Payload is null)
            {
                return CreateReadDirectoryResult(payloadResult.Status);
            }

            return CreateReadDirectoryResult(nfsstat4.NFS4_OK, payloadResult.Payload);
        }
    }
}
