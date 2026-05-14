namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsV40ReplyEnvelopeReader
    {
        internal static COMPOUND4res ReadCompoundResult(ReadOnlyMemory<byte> encodedReply, string operationName)
        {
            ReadOnlyMemory<byte> procedurePayload = OpenNfsRpcReplyDecoder.ReadAcceptedSuccessProcedurePayload(
                encodedReply,
                operationName);
            XdrReader reader = new XdrReader(procedurePayload);
            COMPOUND4res decodedPayload = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return decodedPayload;
        }

        internal static OpenNfsV40Status MapStatus(nfsstat4 value)
        {
            return (OpenNfsV40Status)(int)value;
        }

        internal static nfs_resop4 ReadExpectedOperation(
            COMPOUND4res result,
            int index,
            nfs_opnum4 expectedOperation,
            string operationName)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");
            if (operations.Length <= index)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply omitted result index " + index + ".");
            }

            nfs_resop4 operation = operations[index];
            if (operation.resop != expectedOperation)
            {
                throw new InvalidDataException(
                    "The " + operationName + " reply reported operation "
                    + operation.resop?.ToString() + " at result index " + index
                    + " instead of " + expectedOperation.ToString() + ".");
            }

            return operation;
        }

        internal static nfsstat4 ReadRequiredStatus(nfsstat4? value, string operationName)
        {
            return OpenNfsV40ReplyValueReader.ReadRequiredEnum(value, operationName + " status");
        }

        internal static OpenNfsV40Status ReadTerminalStatus(COMPOUND4res result, OpenNfsV40Status fallbackStatus)
        {
            if (result.resarray is null || result.resarray.Length < 1)
            {
                return fallbackStatus;
            }

            nfs_resop4 terminalOperation = result.resarray[result.resarray.Length - 1];
            nfsstat4? terminalStatus = terminalOperation.resop switch
            {
                nfs_opnum4.OP_ACCESS => terminalOperation.opaccess?.status,
                nfs_opnum4.OP_CLOSE => terminalOperation.opclose?.status,
                nfs_opnum4.OP_COMMIT => terminalOperation.opcommit?.status,
                nfs_opnum4.OP_CREATE => terminalOperation.opcreate?.status,
                nfs_opnum4.OP_DELEGPURGE => terminalOperation.opdelegpurge?.status,
                nfs_opnum4.OP_DELEGRETURN => terminalOperation.opdelegreturn?.status,
                nfs_opnum4.OP_GETATTR => terminalOperation.opgetattr?.status,
                nfs_opnum4.OP_GETFH => terminalOperation.opgetfh?.status,
                nfs_opnum4.OP_ILLEGAL => terminalOperation.opillegal?.status,
                nfs_opnum4.OP_LINK => terminalOperation.oplink?.status,
                nfs_opnum4.OP_LOCK => terminalOperation.oplock?.status,
                nfs_opnum4.OP_LOCKT => terminalOperation.oplockt?.status,
                nfs_opnum4.OP_LOCKU => terminalOperation.oplocku?.status,
                nfs_opnum4.OP_LOOKUP => terminalOperation.oplookup?.status,
                nfs_opnum4.OP_LOOKUPP => terminalOperation.oplookupp?.status,
                nfs_opnum4.OP_NVERIFY => terminalOperation.opnverify?.status,
                nfs_opnum4.OP_OPEN => terminalOperation.opopen?.status,
                nfs_opnum4.OP_OPENATTR => terminalOperation.opopenattr?.status,
                nfs_opnum4.OP_OPEN_CONFIRM => terminalOperation.opopen_confirm?.status,
                nfs_opnum4.OP_OPEN_DOWNGRADE => terminalOperation.opopen_downgrade?.status,
                nfs_opnum4.OP_PUTFH => terminalOperation.opputfh?.status,
                nfs_opnum4.OP_PUTPUBFH => terminalOperation.opputpubfh?.status,
                nfs_opnum4.OP_PUTROOTFH => terminalOperation.opputrootfh?.status,
                nfs_opnum4.OP_READ => terminalOperation.opread?.status,
                nfs_opnum4.OP_READDIR => terminalOperation.opreaddir?.status,
                nfs_opnum4.OP_READLINK => terminalOperation.opreadlink?.status,
                nfs_opnum4.OP_RELEASE_LOCKOWNER => terminalOperation.oprelease_lockowner?.status,
                nfs_opnum4.OP_REMOVE => terminalOperation.opremove?.status,
                nfs_opnum4.OP_RENAME => terminalOperation.oprename?.status,
                nfs_opnum4.OP_RENEW => terminalOperation.oprenew?.status,
                nfs_opnum4.OP_RESTOREFH => terminalOperation.oprestorefh?.status,
                nfs_opnum4.OP_SAVEFH => terminalOperation.opsavefh?.status,
                nfs_opnum4.OP_SECINFO => terminalOperation.opsecinfo?.status,
                nfs_opnum4.OP_SETATTR => terminalOperation.opsetattr?.status,
                nfs_opnum4.OP_SETCLIENTID => terminalOperation.opsetclientid?.status,
                nfs_opnum4.OP_SETCLIENTID_CONFIRM => terminalOperation.opsetclientid_confirm?.status,
                nfs_opnum4.OP_VERIFY => terminalOperation.opverify?.status,
                nfs_opnum4.OP_WRITE => terminalOperation.opwrite?.status,
                _ => null,
            };

            return terminalStatus.HasValue
                ? MapStatus(terminalStatus.Value)
                : fallbackStatus;
        }

        internal static void EnsureSuccessOperationCount(COMPOUND4res result, int expectedCount, string operationName)
        {
            nfs_resop4[] operations = result.resarray
                ?? throw new InvalidDataException("The " + operationName + " reply omitted the result array.");

            if (operations.Length != expectedCount)
            {
                throw new InvalidDataException(
                    "The successful " + operationName + " reply reported "
                    + operations.Length + " operation result(s) instead of " + expectedCount + ".");
            }
        }
    }
}
