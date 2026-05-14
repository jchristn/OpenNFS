namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyAttributeMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40MetadataReplyDecoder
    {
        internal static OpenNfsV40AccessResult ReadAccessResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 ACCESS");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 ACCESS"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40AccessResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 ACCESS");
            ACCESS4res accessResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_ACCESS, "NFSv4.0 ACCESS").opaccess
                ?? throw new InvalidDataException("The successful NFSv4.0 ACCESS reply omitted the ACCESS result arm.");
            ACCESS4resok resok = accessResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 ACCESS reply omitted the ACCESS success arm.");
            return new OpenNfsV40AccessResult(
                overallStatus,
                (OpenNfsV40AccessMask)resok.supported,
                (OpenNfsV40AccessMask)resok.access);
        }

        internal static OpenNfsV40GetAttributesResult ReadGetAttributesResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 GETATTR");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 GETATTR"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40GetAttributesResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 GETATTR");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_GETATTR, "NFSv4.0 GETATTR").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 GETATTR reply omitted the GETATTR result arm.");
            GETATTR4resok resok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 GETATTR reply omitted the GETATTR success arm.");
            return new OpenNfsV40GetAttributesResult(overallStatus, MapAttributes(resok.obj_attributes));
        }

        internal static OpenNfsV40GetAclResult ReadGetAclResult(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsV40GetAttributesResult attributesResult = ReadGetAttributesResult(encodedReply);
            if (!attributesResult.IsSuccess)
            {
                return new OpenNfsV40GetAclResult(attributesResult.Status);
            }

            OpenNfsV40Attributes attributes = attributesResult.Attributes
                ?? throw new InvalidDataException("The successful NFSv4.0 GETACL helper reply omitted the decoded attribute envelope.");

            if (!attributes.AclSupport.HasValue)
            {
                throw new InvalidDataException("The successful NFSv4.0 GETACL helper reply omitted the ACL support attribute.");
            }

            return new OpenNfsV40GetAclResult(
                attributesResult.Status,
                attributes.AclSupport.Value,
                attributes.AclEntries);
        }

        internal static OpenNfsV40SetAclResult ReadSetAclResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETATTR ACL");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETATTR ACL"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetAclResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SETATTR ACL");
            SETATTR4res setAttributeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SETATTR, "NFSv4.0 SETATTR ACL").opsetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 SETATTR reply omitted the SETATTR result arm.");

            return new OpenNfsV40SetAclResult(
                overallStatus,
                ReadBitmapWords(setAttributeResult.attrsset, "SETATTR4res.attrsset"));
        }

        internal static OpenNfsV40SetIdentityResult ReadSetIdentityResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETATTR owner/owner_group");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETATTR owner/owner_group"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetIdentityResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SETATTR owner/owner_group");
            SETATTR4res setAttributeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SETATTR, "NFSv4.0 SETATTR owner/owner_group").opsetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 SETATTR reply omitted the SETATTR result arm.");

            return new OpenNfsV40SetIdentityResult(
                overallStatus,
                ReadBitmapWords(setAttributeResult.attrsset, "SETATTR4res.attrsset"));
        }
    }
}
