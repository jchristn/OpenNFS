namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsV42ReplyEnvelopeReader
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
                throw new InvalidDataException("The " + operationName + " reply omitted result index " + index + ".");
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
            return OpenNfsV42ReplyValueReader.ReadRequiredEnum(value, operationName + " status");
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
                nfs_opnum4.OP_SEQUENCE => terminalOperation.opsequence?.sr_status,
                nfs_opnum4.OP_PUTFH => terminalOperation.opputfh?.status,
                nfs_opnum4.OP_PUTROOTFH => terminalOperation.opputrootfh?.status,
                nfs_opnum4.OP_LOOKUP => terminalOperation.oplookup?.status,
                nfs_opnum4.OP_GETFH => terminalOperation.opgetfh?.status,
                nfs_opnum4.OP_IO_ADVISE => terminalOperation.opio_advise?.ior_status,
                nfs_opnum4.OP_READ_PLUS => terminalOperation.opread_plus?.rp_status,
                nfs_opnum4.OP_SEEK => terminalOperation.opseek?.sa_status,
                nfs_opnum4.OP_ALLOCATE => terminalOperation.opallocate?.ar_status,
                nfs_opnum4.OP_DEALLOCATE => terminalOperation.opdeallocate?.dr_status,
                _ => null,
            };

            return terminalStatus.HasValue
                ? MapStatus(terminalStatus.Value)
                : fallbackStatus;
        }
    }
}
