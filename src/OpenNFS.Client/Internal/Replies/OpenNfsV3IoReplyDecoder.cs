namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V3.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV3ReplyDecodingSupport;

    internal static class OpenNfsV3IoReplyDecoder
    {
        internal static OpenNfsV3ReadResult ReadReadResult(ReadOnlyMemory<byte> encodedReply)
        {
            READ3res result = DecodePayload(encodedReply, "NFSv3 READ", READ3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 READ"));

            if (status != OpenNfsV3Status.Ok)
            {
                READ3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 READ result omitted the resfail arm.");
                return new OpenNfsV3ReadResult(
                    status,
                    fileAttributes: MapPostOperationAttributes(resfail.file_attributes, "READ3res.resfail.file_attributes"));
            }

            READ3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 READ result omitted the resok arm.");
            return new OpenNfsV3ReadResult(
                status,
                MapPostOperationAttributes(resok.file_attributes, "READ3res.resok.file_attributes"),
                ReadRequiredUInt32(resok.count?.Value, "READ3res.resok.count"),
                resok.eof,
                ReadRequiredOpaque(resok.data, "READ3res.resok.data", allowEmpty: true));
        }

        internal static OpenNfsV3WriteResult ReadWriteResult(ReadOnlyMemory<byte> encodedReply)
        {
            WRITE3res result = DecodePayload(encodedReply, "NFSv3 WRITE", WRITE3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 WRITE"));

            if (status != OpenNfsV3Status.Ok)
            {
                WRITE3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 WRITE result omitted the resfail arm.");
                return new OpenNfsV3WriteResult(
                    status,
                    fileWeakCacheConsistency: MapWeakCacheConsistency(resfail.file_wcc));
            }

            WRITE3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 WRITE result omitted the resok arm.");
            return new OpenNfsV3WriteResult(
                status,
                MapWeakCacheConsistency(resok.file_wcc),
                ReadRequiredUInt32(resok.count?.Value, "WRITE3res.resok.count"),
                MapWriteStability(resok.committed),
                ReadRequiredFixedOpaque(resok.verf?.Value, "WRITE3res.resok.verf", 8));
        }

        internal static OpenNfsV3CommitResult ReadCommitResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMMIT3res result = DecodePayload(encodedReply, "NFSv3 COMMIT", COMMIT3res.ReadFrom);
            OpenNfsV3Status status = MapStatus(ReadRequiredStatus(result.status, "NFSv3 COMMIT"));

            if (status != OpenNfsV3Status.Ok)
            {
                COMMIT3resfail resfail = result.resfail
                    ?? throw new InvalidDataException("The failed NFSv3 COMMIT result omitted the resfail arm.");
                return new OpenNfsV3CommitResult(
                    status,
                    fileWeakCacheConsistency: MapWeakCacheConsistency(resfail.file_wcc));
            }

            COMMIT3resok resok = result.resok
                ?? throw new InvalidDataException("The successful NFSv3 COMMIT result omitted the resok arm.");
            return new OpenNfsV3CommitResult(
                status,
                MapWeakCacheConsistency(resok.file_wcc),
                ReadRequiredFixedOpaque(resok.verf?.Value, "COMMIT3res.resok.verf", 8));
        }
    }
}
