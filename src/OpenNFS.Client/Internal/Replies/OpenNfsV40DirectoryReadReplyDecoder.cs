namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyNamespaceMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40DirectoryReadReplyDecoder
    {
        internal static OpenNfsV40ReadDirectoryResult ReadReadDirectoryResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READDIR");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READDIR"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadDirectoryResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READDIR");
            READDIR4res readDirectoryResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READDIR, "NFSv4.0 READDIR").opreaddir
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the READDIR result arm.");
            READDIR4resok resok = readDirectoryResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the READDIR success arm.");
            dirlist4 reply = resok.reply
                ?? throw new InvalidDataException("The successful NFSv4.0 READDIR reply omitted the directory listing payload.");

            return new OpenNfsV40ReadDirectoryResult(
                overallStatus,
                ReadRequiredFixedOpaque(resok.cookieverf?.Value, "READDIR4resok.cookieverf", 8),
                MapDirectoryEntries(reply.entries),
                reply.eof);
        }
    }
}
