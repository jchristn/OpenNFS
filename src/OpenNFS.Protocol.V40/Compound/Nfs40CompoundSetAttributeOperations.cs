namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using static Nfs40CompoundMutationResults;

    internal sealed class Nfs40CompoundSetAttributeOperations
    {
        private readonly Nfs40CompoundHandleServices _handleServices;
        private readonly OpenNfsServer _server;

        internal Nfs40CompoundSetAttributeOperations(
            OpenNfsServer server,
            Nfs40CompoundHandleServices handleServices)
        {
            _server = server;
            _handleServices = handleServices;
        }

        internal async Task<Nfs40CompoundOperationResult> HandleSetAttributesAsync(
            nfs_argop4 operation,
            Nfs40CompoundState state,
            CancellationToken cancellationToken)
        {
            SETATTR4args? arguments = operation.opsetattr;
            if (arguments?.stateid is null || arguments.obj_attributes is null)
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_BADXDR);
            }

            if (!state.TryGetCurrentHandle(out Nfs40CompoundResolvedHandle? currentHandle))
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_NOFILEHANDLE);
            }

            ResolvedHandleStatus refreshResult =
                await _handleServices.TryRefreshResolvedHandleAsync(currentHandle!, cancellationToken).ConfigureAwait(false);
            if (refreshResult.Handle is null)
            {
                return CreateSetAttrResult(refreshResult.Status);
            }

            Nfs40CompoundResolvedHandle refreshedHandle = refreshResult.Handle;

            if (refreshedHandle.PathInfo.Kind == NfsPathKind.Other)
            {
                return CreateSetAttrResult(nfsstat4.NFS4ERR_NOTSUPP);
            }

            if (!Nfs40AttributeEncoder.TryReadSettableAttributes(
                arguments.obj_attributes,
                includeIdentityAttributes: _server.Capabilities.IdMapper is not null,
                includeAclAttributes: _server.Capabilities.Acls is not null,
                out Nfs40SetAttributeUpdate? update,
                out nfsstat4 decodeStatus))
            {
                return CreateSetAttrResult(decodeStatus);
            }

            List<int> updatedAttributeIds = new List<int>();
            try
            {
                if (update!.HasAclUpdate)
                {
                    await _server.Capabilities.Acls!.SetAclAsync(
                        new NfsSetAclRequest(
                            refreshedHandle.Target.SourcePath,
                            refreshedHandle.PathInfo.Kind,
                            update.AclEntries,
                            cancellationToken)).ConfigureAwait(false);
                    updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_ACL);
                }

                if (update.HasIdentityUpdate)
                {
                    NfsSetIdentityResponse identityResponse =
                        await _server.Capabilities.IdMapper!.SetIdentityAsync(
                            new NfsSetIdentityRequest(
                                refreshedHandle.Target.SourcePath,
                                refreshedHandle.PathInfo.Kind,
                                update.Owner,
                                update.OwnerGroup,
                                cancellationToken)).ConfigureAwait(false);

                    if (update.Owner is not null)
                    {
                        updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER);
                    }

                    if (update.OwnerGroup is not null)
                    {
                        updatedAttributeIds.Add((int)Nfs40Constants.FATTR4_OWNER_GROUP);
                    }

                    if (string.IsNullOrWhiteSpace(identityResponse.Owner)
                        || string.IsNullOrWhiteSpace(identityResponse.OwnerGroup))
                    {
                        return CreateSetAttrResult(nfsstat4.NFS4ERR_SERVERFAULT);
                    }
                }
            }
            catch (Exception exception)
            {
                return CreateSetAttrResult(
                    update!.HasIdentityUpdate && !update.HasAclUpdate
                        ? Nfs40MutationSupport.MapIdentityException(exception)
                        : update.HasAclUpdate && !update.HasIdentityUpdate
                            ? Nfs40MutationSupport.MapAclException(exception)
                            : Nfs40MutationSupport.MapCreateException(exception));
            }

            return CreateSetAttrResult(
                nfsstat4.NFS4_OK,
                Nfs40AttributeEncoder.CreateBitmap(updatedAttributeIds.ToArray()));
        }
    }
}
