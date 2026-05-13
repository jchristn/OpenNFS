namespace OpenNFS.Client.Sessions
{
    using System;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Decoded payload returned by a successful NFSv4.1 <c>READ</c> operation.
    /// </summary>
    public sealed class OpenNfsV41ReadPayload
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41ReadPayload"/> class.
        /// </summary>
        /// <param name="data">The bytes returned by the server. The array is defensively copied.</param>
        /// <param name="endOfFile">Whether the server reported EOF for the read.</param>
        public OpenNfsV41ReadPayload(byte[] data, bool endOfFile)
        {
            ArgumentNullException.ThrowIfNull(data);

            Data = new byte[data.Length];
            Buffer.BlockCopy(data, 0, Data, 0, data.Length);
            EndOfFile = endOfFile;
        }

        /// <summary>
        /// Gets the bytes returned by the server.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets a value indicating whether the server reported EOF for the read.
        /// </summary>
        public bool EndOfFile { get; }
    }

    /// <summary>
    /// Convenience readers for successful NFSv4.1 COMPOUND results produced by
    /// <see cref="OpenNfsV41ClientSession.TrySendCompoundAsync"/>.
    /// </summary>
    public static class OpenNfsV41CompoundResultReaders
    {
        /// <summary>
        /// Attempts to extract the first successful <c>OPEN</c> stateid from a full-success result.
        /// </summary>
        /// <param name="result">The result envelope.</param>
        /// <param name="stateid">The copied stateid, when present.</param>
        /// <returns><c>true</c> when a successful <c>OPEN</c> stateid was found.</returns>
        public static bool TryGetOpenStateId(OpenNfsV41CompoundResult result, out stateid4? stateid)
        {
            ArgumentNullException.ThrowIfNull(result);

            stateid = null;
            if (!result.IsFullSuccess || result.Outcome?.Response.resarray is not nfs_resop4[] results)
            {
                return false;
            }

            for (int index = 0; index < results.Length; index++)
            {
                OPEN4res? openResult = results[index].resop == nfs_opnum4.OP_OPEN
                    ? results[index].opopen
                    : null;
                if (openResult?.status == nfsstat4.NFS4_OK && openResult.resok4?.stateid is stateid4 openStateId)
                {
                    stateid = CopyStateId(openStateId);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the first successful <c>OPEN</c> stateid from a full-success result.
        /// </summary>
        /// <param name="result">The result envelope.</param>
        /// <param name="operationName">A human-readable operation name embedded in thrown exceptions.</param>
        /// <returns>The copied stateid.</returns>
        public static stateid4 GetOpenStateIdOrThrow(OpenNfsV41CompoundResult result, string operationName)
        {
            ArgumentNullException.ThrowIfNull(result);

            _ = result.GetOutcomeOrThrow(operationName);
            if (TryGetOpenStateId(result, out stateid4? stateid) && stateid is not null)
            {
                return stateid;
            }

            throw new InvalidOperationException(
                (operationName ?? "NFSv4.1 OPEN") + " completed without a successful OPEN stateid result.");
        }

        /// <summary>
        /// Attempts to extract the first successful <c>READ</c> payload from a full-success result.
        /// </summary>
        /// <param name="result">The result envelope.</param>
        /// <param name="payload">The copied read payload, when present.</param>
        /// <returns><c>true</c> when a successful <c>READ</c> payload was found.</returns>
        public static bool TryGetReadPayload(OpenNfsV41CompoundResult result, out OpenNfsV41ReadPayload? payload)
        {
            ArgumentNullException.ThrowIfNull(result);

            payload = null;
            if (!result.IsFullSuccess || result.Outcome?.Response.resarray is not nfs_resop4[] results)
            {
                return false;
            }

            for (int index = 0; index < results.Length; index++)
            {
                READ4res? readResult = results[index].resop == nfs_opnum4.OP_READ
                    ? results[index].opread
                    : null;
                if (readResult?.status == nfsstat4.NFS4_OK && readResult.resok4 is READ4resok readPayload)
                {
                    payload = new OpenNfsV41ReadPayload(readPayload.data ?? Array.Empty<byte>(), readPayload.eof);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts the first successful <c>READ</c> payload from a full-success result.
        /// </summary>
        /// <param name="result">The result envelope.</param>
        /// <param name="operationName">A human-readable operation name embedded in thrown exceptions.</param>
        /// <returns>The copied read payload.</returns>
        public static OpenNfsV41ReadPayload GetReadPayloadOrThrow(OpenNfsV41CompoundResult result, string operationName)
        {
            ArgumentNullException.ThrowIfNull(result);

            _ = result.GetOutcomeOrThrow(operationName);
            if (TryGetReadPayload(result, out OpenNfsV41ReadPayload? payload) && payload is not null)
            {
                return payload;
            }

            throw new InvalidOperationException(
                (operationName ?? "NFSv4.1 READ") + " completed without a successful READ payload result.");
        }

        private static stateid4 CopyStateId(stateid4 stateid)
        {
            byte[]? other = null;
            if (stateid.other is not null)
            {
                other = new byte[stateid.other.Length];
                Buffer.BlockCopy(stateid.other, 0, other, 0, stateid.other.Length);
            }

            return new stateid4
            {
                seqid = stateid.seqid,
                other = other,
            };
        }
    }
}
