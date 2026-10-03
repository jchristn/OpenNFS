namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Protocol.V40.State;
    using OpenNFS.Server;
    using OpenNFS.Server.Delegations;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class Nfs40CompoundHandleServices
    {
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundHandleServices(OpenNfsServer server)
        {
            _server = server;
        }

        internal async Task<Nfs40CompoundResolvedHandle> CreateResolvedHandleAsync(
            NfsFileHandleTarget target,
            CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await _server.CreateFileHandleAsync(target, cancellationToken).ConfigureAwait(false);
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(target.SourcePath, cancellationToken)).ConfigureAwait(false);

            return new Nfs40CompoundResolvedHandle(fileHandle, target, pathInfoResponse.PathInfo);
        }

        internal async Task<NfsPathInfo> GetPathInfoAsync(string sourcePath, CancellationToken cancellationToken)
        {
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(sourcePath, cancellationToken)).ConfigureAwait(false);
            return pathInfoResponse.PathInfo;
        }

        internal async Task<NfsPathInfo> LookupChildPathInfoAsync(
            NfsFileHandleTarget directoryTarget,
            string entryName,
            CancellationToken cancellationToken)
        {
            NfsLookupPathResponse lookupResponse =
                await _server.Settings.InstrumentedFileSystem.LookupPathAsync(
                    new NfsLookupPathRequest(
                        directoryTarget.SourcePath,
                        entryName,
                        cancellationToken)).ConfigureAwait(false);
            return lookupResponse.PathInfo;
        }

        internal async Task NotifyDelegationRecallsAsync(
            IReadOnlyList<Nfs40DelegationRecallInfo> recallRequests,
            string exportPath,
            string sourcePath,
            CancellationToken cancellationToken)
        {
            if (_server.Capabilities.Delegations is null || recallRequests.Count == 0)
            {
                return;
            }

            for (int index = 0; index < recallRequests.Count; index++)
            {
                Nfs40DelegationRecallInfo recall = recallRequests[index];
                await _server.Capabilities.TrackedDelegations!.RecallDelegationAsync(
                    new NfsRecallDelegationRequest(
                        exportPath,
                        sourcePath,
                        recall.ClientId,
                        recall.DelegationKind,
                        recall.StateId.other ?? Array.Empty<byte>(),
                        NfsDelegationRecallReason.OpenConflict,
                        cancellationToken)).ConfigureAwait(false);
            }
        }

        internal async Task<Nfs40CompoundResolvedHandle> ResolveExistingFileHandleAsync(
            byte[] fileHandleBytes,
            CancellationToken cancellationToken)
        {
            NfsResolveFileHandleResponse resolutionResponse =
                await _server.ResolveFileHandleAsync(
                    new NfsResolveFileHandleRequest(new NfsFileHandle(fileHandleBytes), cancellationToken)).ConfigureAwait(false);

            if (!resolutionResponse.Resolution.Found || resolutionResponse.Resolution.Target is null)
            {
                throw new FileNotFoundException("The supplied NFSv4 filehandle could not be resolved.");
            }

            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(resolutionResponse.Resolution.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                throw new FileNotFoundException(
                    "The resolved NFSv4 filehandle target no longer exists.",
                    resolutionResponse.Resolution.Target.SourcePath);
            }

            return new Nfs40CompoundResolvedHandle(
                new NfsFileHandle(fileHandleBytes),
                resolutionResponse.Resolution.Target,
                pathInfoResponse.PathInfo);
        }

        internal async Task<ResolvedHandleStatus> TryRefreshResolvedHandleAsync(
            Nfs40CompoundResolvedHandle resolvedHandle,
            CancellationToken cancellationToken)
        {
            try
            {
                return new ResolvedHandleStatus(
                    await RefreshResolvedHandleAsync(resolvedHandle, cancellationToken).ConfigureAwait(false),
                    nfsstat4.NFS4_OK);
            }
            catch (FileNotFoundException)
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_STALE);
            }
        }

        internal async Task<ResolvedHandleStatus> TryResolveParentHandleAsync(
            Nfs40CompoundResolvedHandle currentHandle,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<OpenNfsExportDefinition> exports =
                await _server.GetExportsAsync(cancellationToken).ConfigureAwait(false);
            OpenNfsExportDefinition? owningExport = null;

            for (int index = 0; index < exports.Count; index++)
            {
                if (string.Equals(exports[index].ExportPath, currentHandle.Target.ExportPath, StringComparison.Ordinal))
                {
                    owningExport = exports[index];
                    break;
                }
            }

            if (owningExport is null)
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_STALE);
            }

            if (!Nfs40MutationSupport.IsSamePath(currentHandle.Target.SourcePath, owningExport.SourcePath)
                && !Nfs40MutationSupport.IsDescendantPath(currentHandle.Target.SourcePath, owningExport.SourcePath))
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_STALE);
            }

            if (Nfs40MutationSupport.IsSamePath(currentHandle.Target.SourcePath, owningExport.SourcePath))
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_NOENT);
            }

            string? parentSourcePath = OpenNFS.Server.Internal.NfsSourcePath.GetDirectoryName(currentHandle.Target.SourcePath);
            if (string.IsNullOrWhiteSpace(parentSourcePath))
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_NOENT);
            }

            if (!Nfs40MutationSupport.IsSamePath(parentSourcePath, owningExport.SourcePath)
                && !Nfs40MutationSupport.IsDescendantPath(parentSourcePath, owningExport.SourcePath))
            {
                return new ResolvedHandleStatus(null, nfsstat4.NFS4ERR_NOENT);
            }

            Nfs40CompoundResolvedHandle parentHandle =
                await CreateResolvedHandleAsync(
                    new NfsFileHandleTarget(currentHandle.Target.ExportPath, parentSourcePath),
                    cancellationToken).ConfigureAwait(false);
            return new ResolvedHandleStatus(parentHandle, nfsstat4.NFS4_OK);
        }

        private async Task<Nfs40CompoundResolvedHandle> RefreshResolvedHandleAsync(
            Nfs40CompoundResolvedHandle resolvedHandle,
            CancellationToken cancellationToken)
        {
            NfsGetPathInfoResponse pathInfoResponse =
                await _server.Settings.InstrumentedFileSystem.GetPathInfoAsync(
                    new NfsGetPathInfoRequest(resolvedHandle.Target.SourcePath, cancellationToken)).ConfigureAwait(false);

            if (!pathInfoResponse.PathInfo.Exists)
            {
                throw new FileNotFoundException("The resolved filehandle target no longer exists.", resolvedHandle.Target.SourcePath);
            }

            return new Nfs40CompoundResolvedHandle(
                resolvedHandle.FileHandle,
                resolvedHandle.Target,
                pathInfoResponse.PathInfo);
        }
    }
}
