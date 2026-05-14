namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyStateMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40IoReplyDecoder
    {
        internal static OpenNfsV40ReadResult ReadReadResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 READ");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 READ"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40ReadResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 READ");
            READ4res readResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_READ, "NFSv4.0 READ").opread
                ?? throw new InvalidDataException("The successful NFSv4.0 READ reply omitted the READ result arm.");
            READ4resok resok = readResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 READ reply omitted the READ success arm.");
            ReadOnlyMemory<byte> data = ReadRequiredOpaque(resok.data, "READ4resok.data", allowEmpty: true);
            return new OpenNfsV40ReadResult(overallStatus, (uint)data.Length, resok.eof, data);
        }

        internal static OpenNfsV40WriteResult ReadWriteResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 WRITE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 WRITE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40WriteResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 WRITE");
            WRITE4res writeResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_WRITE, "NFSv4.0 WRITE").opwrite
                ?? throw new InvalidDataException("The successful NFSv4.0 WRITE reply omitted the WRITE result arm.");
            WRITE4resok resok = writeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 WRITE reply omitted the WRITE success arm.");
            return new OpenNfsV40WriteResult(
                overallStatus,
                ReadRequiredUInt32(resok.count?.Value, "WRITE4resok.count"),
                MapWriteStability(resok.committed),
                ReadRequiredFixedOpaque(resok.writeverf?.Value, "WRITE4resok.writeverf", 8));
        }

        internal static OpenNfsV40CommitResult ReadCommitResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 COMMIT");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 COMMIT"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40CommitResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 COMMIT");
            COMMIT4res commitResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_COMMIT, "NFSv4.0 COMMIT").opcommit
                ?? throw new InvalidDataException("The successful NFSv4.0 COMMIT reply omitted the COMMIT result arm.");
            COMMIT4resok resok = commitResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 COMMIT reply omitted the COMMIT success arm.");
            return new OpenNfsV40CommitResult(
                overallStatus,
                ReadRequiredFixedOpaque(resok.writeverf?.Value, "COMMIT4resok.writeverf", 8));
        }
    }
}
