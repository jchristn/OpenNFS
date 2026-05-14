namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Encodes the grouped NFSv3 file-oriented requests exposed by <see cref="OpenNFS.Client.Apis.FileApis"/>.
    /// </summary>
    internal static class OpenNfsFileApiV3Requests
    {
        internal static OpenNfsV3ProcedureRequest CreateSingleHandleRequest(byte[] fileHandle, uint procedureNumber)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateAccessRequest(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt32((uint)requestedAccess);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsAccessProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateReadRequest(byte[] fileHandle, ulong offset, uint count)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested read byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReadProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateWriteRequest(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            byte[] safeData = OpenNfsClientArgument.RequireBytes(data, nameof(data), allowEmpty: true);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32((uint)safeData.Length);
            writer.WriteInt32((int)stability);
            writer.WriteOpaque(safeData);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsWriteProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static OpenNfsV3ProcedureRequest CreateCommitRequest(byte[] fileHandle, ulong offset, uint count)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsCommitProcedure,
                procedurePayload: writer.ToArray());
        }
    }
}
