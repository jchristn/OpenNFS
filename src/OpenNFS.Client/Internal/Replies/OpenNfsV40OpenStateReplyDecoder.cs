namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyAttributeMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyStateMapper;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40OpenStateReplyDecoder
    {
        internal static OpenNfsV40StateIdResult ReadCloseResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 CLOSE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 CLOSE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            if (result.resarray is null || (result.resarray.Length != 1 && result.resarray.Length != 2))
            {
                throw new InvalidDataException(
                    "The successful NFSv4.0 CLOSE reply was expected to contain either one operation (CLOSE) or two operations (PUTFH + CLOSE).");
            }

            if (result.resarray.Length == 2)
            {
                ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, "NFSv4.0 CLOSE");
            }

            int closeIndex = result.resarray.Length - 1;
            CLOSE4res closeResult = ReadExpectedOperation(result, closeIndex, nfs_opnum4.OP_CLOSE, "NFSv4.0 CLOSE").opclose
                ?? throw new InvalidDataException("The successful NFSv4.0 CLOSE reply omitted the CLOSE result arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(closeResult.open_stateid, "CLOSE4res.open_stateid"));
        }

        internal static OpenNfsV40DelegationReturnResult ReadDelegationReturnResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 DELEGRETURN");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 DELEGRETURN"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40DelegationReturnResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 DELEGRETURN");
            DELEGRETURN4res delegReturnResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_DELEGRETURN, "NFSv4.0 DELEGRETURN").opdelegreturn
                ?? throw new InvalidDataException("The successful NFSv4.0 DELEGRETURN reply omitted the DELEGRETURN result arm.");
            return new OpenNfsV40DelegationReturnResult(MapStatus(ReadRequiredStatus(delegReturnResult.status, "NFSv4.0 DELEGRETURN")));
        }

        internal static OpenNfsV40StateIdResult ReadOpenConfirmResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN_CONFIRM");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN_CONFIRM"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            if (result.resarray is null || (result.resarray.Length != 1 && result.resarray.Length != 2))
            {
                throw new InvalidDataException(
                    "The successful NFSv4.0 OPEN_CONFIRM reply was expected to contain either one operation (OPEN_CONFIRM) or two operations (PUTFH + OPEN_CONFIRM).");
            }

            if (result.resarray.Length == 2)
            {
                ReadExpectedOperation(result, 0, nfs_opnum4.OP_PUTFH, "NFSv4.0 OPEN_CONFIRM");
            }

            int openConfirmIndex = result.resarray.Length - 1;
            OPEN_CONFIRM4res openConfirmResult = ReadExpectedOperation(result, openConfirmIndex, nfs_opnum4.OP_OPEN_CONFIRM, "NFSv4.0 OPEN_CONFIRM").opopen_confirm
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_CONFIRM reply omitted the OPEN_CONFIRM result arm.");
            OPEN_CONFIRM4resok resok = openConfirmResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_CONFIRM reply omitted the OPEN_CONFIRM success arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(resok.open_stateid, "OPEN_CONFIRM4resok.open_stateid"));
        }

        internal static OpenNfsV40StateIdResult ReadOpenDowngradeResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN_DOWNGRADE");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN_DOWNGRADE"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40StateIdResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 1, "NFSv4.0 OPEN_DOWNGRADE");
            OPEN_DOWNGRADE4res openDowngradeResult = ReadExpectedOperation(result, 0, nfs_opnum4.OP_OPEN_DOWNGRADE, "NFSv4.0 OPEN_DOWNGRADE").opopen_downgrade
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_DOWNGRADE reply omitted the OPEN_DOWNGRADE result arm.");
            OPEN_DOWNGRADE4resok resok = openDowngradeResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN_DOWNGRADE reply omitted the OPEN_DOWNGRADE success arm.");
            return new OpenNfsV40StateIdResult(overallStatus, MapStateId(resok.open_stateid, "OPEN_DOWNGRADE4resok.open_stateid"));
        }

        internal static OpenNfsV40OpenResult ReadOpenResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 OPEN");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 OPEN"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40OpenResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 4, "NFSv4.0 OPEN");
            OPEN4res openResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_OPEN, "NFSv4.0 OPEN").opopen
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the OPEN result arm.");
            OPEN4resok openResok = openResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the OPEN success arm.");
            GETFH4res getfhResult = ReadExpectedOperation(result, 2, nfs_opnum4.OP_GETFH, "NFSv4.0 OPEN").opgetfh
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETFH result arm.");
            GETFH4resok getfhResok = getfhResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETFH success arm.");
            GETATTR4res getattrResult = ReadExpectedOperation(result, 3, nfs_opnum4.OP_GETATTR, "NFSv4.0 OPEN").opgetattr
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETATTR result arm.");
            GETATTR4resok getattrResok = getattrResult.resok4
                ?? throw new InvalidDataException("The successful NFSv4.0 OPEN reply omitted the GETATTR success arm.");

            return new OpenNfsV40OpenResult(
                overallStatus,
                MapStateId(openResok.stateid, "OPEN4resok.stateid"),
                requiresConfirmation: (openResok.rflags & (uint)Nfs40Constants.OPEN4_RESULT_CONFIRM) != 0U,
                directoryChangeInfo: MapChangeInfo(openResok.cinfo),
                appliedAttributeMaskWords: ReadBitmapWords(openResok.attrset, "OPEN4resok.attrset"),
                objectFileHandle: ReadRequiredOpaque(getfhResok.@object?.Value, "GETFH4resok.object"),
                objectAttributes: MapAttributes(getattrResok.obj_attributes),
                delegation: MapDelegation(openResok.delegation));
        }
    }
}
