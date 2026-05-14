namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40RequestSupport;

    internal static class OpenNfsFileApiV40MetadataRequests
    {
        internal static OpenNfsCompoundRequest CreateAccessV40Request(
            byte[] fileHandle,
            OpenNfsV40AccessMask requestedAccess)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "access",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_ACCESS,
                        EncodeV40Payload(
                            new ACCESS4args
                            {
                                access = (uint)requestedAccess,
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateGetAttributesV40Request(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AttributeKind>? requestedAttributes)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "getattr",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = requestedAttributes is null
                                    ? CreateDefaultV40AttributeRequest()
                                    : CreateV40AttributeRequest(requestedAttributes),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateGetAclV40Request(byte[] fileHandle)
        {
            return CreateGetAttributesV40Request(
                fileHandle,
                new[]
                {
                    OpenNfsV40AttributeKind.AclSupport,
                    OpenNfsV40AttributeKind.Acl,
                });
        }

        internal static OpenNfsCompoundRequest CreateSetAclV40Request(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "setacl",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETATTR,
                        EncodeV40Payload(
                            new SETATTR4args
                            {
                                stateid = CreateAnonymousStateId(),
                                obj_attributes = CreateV40AclAttributes(entries),
                            }.WriteTo)),
                });
        }

        internal static OpenNfsCompoundRequest CreateSetOwnerAndGroupV40Request(
            byte[] fileHandle,
            string? owner,
            string? ownerGroup)
        {
            fattr4 identityAttributes = CreateV40IdentityAttributes(owner, ownerGroup);
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "set-identity",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETATTR,
                        EncodeV40Payload(
                            new SETATTR4args
                            {
                                stateid = CreateAnonymousStateId(),
                                obj_attributes = identityAttributes,
                            }.WriteTo)),
                });
        }
    }
}
