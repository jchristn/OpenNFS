namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Server;
    using static Nfs40CompoundNavigationResults;

    internal sealed class Nfs40CompoundNamespaceLookupOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundNamespaceLookupOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleSecurityInfoAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            SECINFO4args? arguments = operation.opsecinfo;
            if (arguments is null)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateSecurityInfoResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            switch (refreshedHandle.PathInfo.Kind)
            {
                case NfsPathKind.SymbolicLink:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_SYMLINK);
                case NfsPathKind.Other:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTSUPP);
                case not NfsPathKind.Directory:
                    return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(
                arguments.name,
                out string entryName,
                out nfsstat4 entryNameStatus))
            {
                return CreateSecurityInfoResult(entryNameStatus);
            }

            NfsPathInfo childPathInfo;
            try
            {
                childPathInfo =
                    await _handleServices.LookupChildPathInfoAsync(
                        refreshedHandle.Target,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }
            catch (DirectoryNotFoundException)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (!childPathInfo.Exists)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateSecurityInfoResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            return CreateSecurityInfoResult(
                nfsstat4.NFS4_OK,
                new SECINFO4resok
                {
                    Value = CreateSupportedSecurityInfos(),
                });
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLookupAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            LOOKUP4args? arguments = operation.oplookup;
            if (arguments is null)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateLookupResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLookupResult(
                    refreshedHandle.PathInfo.Kind == NfsPathKind.SymbolicLink
                        ? nfsstat4.NFS4ERR_SYMLINK
                        : refreshedHandle.PathInfo.Kind == NfsPathKind.Other
                            ? nfsstat4.NFS4ERR_NOTSUPP
                            : nfsstat4.NFS4ERR_NOTDIR);
            }

            if (!Nfs40MutationSupport.TryReadComponent(arguments.objname, out string entryName, out nfsstat4 nameStatus))
            {
                return CreateLookupResult(nameStatus);
            }

            NfsPathInfo childPathInfo =
                await _handleServices.LookupChildPathInfoAsync(
                    refreshedHandle.Target,
                    entryName,
                    cancellationToken).ConfigureAwait(false);
            if (!childPathInfo.Exists)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOENT);
            }

            if (childPathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLookupResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            NfsFileHandleTarget childTarget = new NfsFileHandleTarget(
                refreshedHandle.Target.ExportPath,
                childPathInfo.Path);
            NfsFileHandle childFileHandle =
                await _server.CreateFileHandleAsync(childTarget, cancellationToken).ConfigureAwait(false);
            state.SetCurrentHandle(new Nfs40CompoundResolvedHandle(childFileHandle, childTarget, childPathInfo));
            return CreateLookupResult(nfsstat4.NFS4_OK);
        }

        internal async Task<Nfs40CompoundOperationResult> HandleLookupParentAsync(
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateLookupParentResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (refreshedHandle.PathInfo.Kind != NfsPathKind.Directory)
            {
                return CreateLookupParentResult(nfsstat4.NFS4ERR_NOTDIR);
            }

            ResolvedHandleStatus parentResult =
                await _handleServices.TryResolveParentHandleAsync(refreshedHandle, cancellationToken).ConfigureAwait(false);
            if (parentResult.Handle is null)
            {
                return CreateLookupParentResult(parentResult.Status);
            }

            Nfs40CompoundResolvedHandle parentHandle = parentResult.Handle;

            state.SetCurrentHandle(parentHandle);
            return CreateLookupParentResult(nfsstat4.NFS4_OK);
        }

        private static secinfo4[] CreateSupportedSecurityInfos()
        {
            return new[]
            {
                new secinfo4
                {
                    flavor = (uint)auth_flavor.AUTH_NONE,
                },
                new secinfo4
                {
                    flavor = (uint)auth_flavor.AUTH_SYS,
                },
            };
        }
    }
}
