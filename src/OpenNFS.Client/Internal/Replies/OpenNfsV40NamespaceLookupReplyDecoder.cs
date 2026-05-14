namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyAttributeMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyNamespaceMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40NamespaceLookupReplyDecoder
    {
        internal static OpenNfsV40LookupResult ReadGetRootResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 PUTROOTFH");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 PUTROOTFH"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 3, "NFSv4.0 PUTROOTFH");
            GETFH4res getfhResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_GETFH, "NFSv4.0 PUTROOTFH").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETATTR, "NFSv4.0 PUTROOTFH").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 PUTROOTFH reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40LookupResult ReadLookupResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOOKUP");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOOKUP"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LOOKUP");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 LOOKUP").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 LOOKUP").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUP reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40LookupResult ReadLookupParentResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOOKUPP");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOOKUPP"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LookupResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LOOKUPP");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 LOOKUPP").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 LOOKUPP").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LOOKUPP reply omitted the GETATTR success arm.");

            return new OpenNfsV40LookupResult(
                overallStatus,
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40SecurityInfoResult ReadSecurityInfoResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SECINFO");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SECINFO"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SecurityInfoResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 SECINFO");
            SECINFO4res securityInfoResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_SECINFO, "NFSv4.0 SECINFO").opsecinfo
                ?? throw new InvalidDataException("The successful NFSv4.0 SECINFO reply omitted the SECINFO result arm.");
            SECINFO4resok resok = securityInfoResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 SECINFO reply omitted the SECINFO success arm.");

            return new OpenNfsV40SecurityInfoResult(overallStatus, MapSecurityFlavors(resok.Value));
        }

        internal static OpenNfsV40ReadLinkResult ReadReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READLINK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READLINK"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadLinkResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READLINK");
            READLINK4res readLinkResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READLINK, "NFSv4.0 READLINK").opreadlink
                ?? throw new InvalidDataException("The successful NFSv4.0 READLINK reply omitted the READLINK result arm.");
            READLINK4resok resok = readLinkResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READLINK reply omitted the READLINK success arm.");
            return new OpenNfsV40ReadLinkResult(overallStatus, ReadRequiredUtf8(resok.link?.Value, "READLINK4resok.link"));
        }
    }
}
