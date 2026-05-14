namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyAttributeMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyStateMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40NamespaceMutationReplyDecoder
    {
        internal static OpenNfsV40CreateResult ReadCreateResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 CREATE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 CREATE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40CreateResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 CREATE");
            CREATE4res createResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_CREATE, "NFSv4.0 CREATE").opcreate
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the CREATE result arm.");
            CREATE4resok createResok = createResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the CREATE success arm.");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 CREATE").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 CREATE").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 CREATE reply omitted the GETATTR success arm.");

            return new OpenNfsV40CreateResult(
                overallStatus,
                MapChangeInfo(createResok.cinfo),
                ReadBitmapWords(createResok.attrset, "CREATE4resok.attrset"),
                ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                MapAttributes(getattrResok.obj_attributes));
        }

        internal static OpenNfsV40LinkResult ReadLinkResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LINK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LINK"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LinkResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 LINK");
            LINK4res linkResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_LINK, "NFSv4.0 LINK").oplink
                ?? throw new InvalidDataException("The successful NFSv4.0 LINK reply omitted the LINK result arm.");
            LINK4resok resok = linkResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 LINK reply omitted the LINK success arm.");
            return new OpenNfsV40LinkResult(overallStatus, MapChangeInfo(resok.cinfo));
        }

        internal static OpenNfsV40DirectoryMutationResult ReadRemoveResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 REMOVE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 REMOVE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40DirectoryMutationResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 REMOVE");
            REMOVE4res removeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_REMOVE, "NFSv4.0 REMOVE").opremove
                ?? throw new InvalidDataException("The successful NFSv4.0 REMOVE reply omitted the REMOVE result arm.");
            REMOVE4resok resok = removeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 REMOVE reply omitted the REMOVE success arm.");
            return new OpenNfsV40DirectoryMutationResult(overallStatus, MapChangeInfo(resok.cinfo));
        }

        internal static OpenNfsV40RenameResult ReadRenameResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 RENAME");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 RENAME"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40RenameResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 RENAME");
            RENAME4res renameResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_RENAME, "NFSv4.0 RENAME").oprename
                ?? throw new InvalidDataException("The successful NFSv4.0 RENAME reply omitted the RENAME result arm.");
            RENAME4resok resok = renameResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 RENAME reply omitted the RENAME success arm.");
            return new OpenNfsV40RenameResult(
                overallStatus,
                MapChangeInfo(resok.source_cinfo),
                MapChangeInfo(resok.target_cinfo));
        }
    }
}
