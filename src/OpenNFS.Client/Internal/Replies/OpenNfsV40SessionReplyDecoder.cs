namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40SessionReplyDecoder
    {
        internal static OpenNfsV40SessionResult ReadRenewResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 RENEW");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 RENEW"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SessionResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 RENEW");
            RENEW4res renewResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_RENEW, "NFSv4.0 RENEW").oprenew
                ?? throw new InvalidDataException("The successful NFSv4.0 RENEW reply omitted the RENEW result arm.");
            return new OpenNfsV40SessionResult(MapStatus(ReadRequiredStatus(renewResult.status, "NFSv4.0 RENEW")));
        }

        internal static OpenNfsV40SetClientIdResult ReadSetClientIdResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETCLIENTID");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETCLIENTID"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SetClientIdResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 SETCLIENTID");
            SETCLIENTID4res setClientIdResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SETCLIENTID, "NFSv4.0 SETCLIENTID").opsetclientid
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the SETCLIENTID result arm.");
            SETCLIENTID4resok resok = setClientIdResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the SETCLIENTID success arm.");
            clientid4 clientId = resok.clientid
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID reply omitted the assigned clientid.");
            return new OpenNfsV40SetClientIdResult(
                overallStatus,
                clientId.Value,
                ReadRequiredFixedOpaque(resok.setclientid_confirm?.Value, "SETCLIENTID4resok.setclientid_confirm", 8));
        }

        internal static OpenNfsV40SessionResult ReadSetClientIdConfirmResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 SETCLIENTID_CONFIRM");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 SETCLIENTID_CONFIRM"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40SessionResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 SETCLIENTID_CONFIRM");
            SETCLIENTID_CONFIRM4res confirmResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_SETCLIENTID_CONFIRM, "NFSv4.0 SETCLIENTID_CONFIRM").opsetclientid_confirm
                ?? throw new InvalidDataException("The successful NFSv4.0 SETCLIENTID_CONFIRM reply omitted the SETCLIENTID_CONFIRM result arm.");
            return new OpenNfsV40SessionResult(MapStatus(ReadRequiredStatus(confirmResult.status, "NFSv4.0 SETCLIENTID_CONFIRM")));
        }
    }
}
