namespace OpenNFS.Server.Internal.V41
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Concurrent;
    using System.Security.Cryptography;
    using System.Text;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal static class Nfs41ServerCompoundPayloadCodec
    {
        internal static RpcMessageEnvelope CreateAcceptedSuccessReply(uint xid, COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);

            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: accept_stat.SUCCESS,
                procedurePayload: writer.ToArray());
        }

        internal static byte[] EncodeCompoundResult(COMPOUND4res value)
        {
            ArgumentNullException.ThrowIfNull(value);

            XdrWriter writer = new XdrWriter();
            value.WriteTo(writer);
            return writer.ToArray();
        }

        internal static T ReadPayload<T>(ReadOnlyMemory<byte> payload, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(readValue);

            XdrReader reader = new XdrReader(payload);
            T value = readValue(reader);
            reader.EnsureFullyConsumed();
            return value;
        }
    }

    internal sealed class Nfs41ResolvedHandle
    {
        internal Nfs41ResolvedHandle(
            OpenNfsExportDefinition export,
            NfsFileHandle fileHandle,
            NfsFileHandleTarget target,
            NfsPathInfo pathInfo)
        {
            ArgumentNullException.ThrowIfNull(export);
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(pathInfo);

            Export = export;
            FileHandle = fileHandle;
            Target = target;
            PathInfo = pathInfo;
        }

        internal OpenNfsExportDefinition Export { get; }

        internal NfsFileHandle FileHandle { get; }

        internal NfsFileHandleTarget Target { get; }

        internal NfsPathInfo PathInfo { get; }
    }

    internal sealed class Nfs41CompoundState
    {
        private Nfs41ResolvedHandle? currentHandle;

        internal void SetCurrentHandle(Nfs41ResolvedHandle handle)
        {
            ArgumentNullException.ThrowIfNull(handle);
            currentHandle = handle;
        }

        internal bool TryGetCurrentHandle(out Nfs41ResolvedHandle? handle)
        {
            handle = currentHandle;
            return handle is not null;
        }
    }

    internal sealed class Nfs41OpenStateTable
    {
        private readonly ConcurrentDictionary<string, Nfs41OpenState> openStates =
            new ConcurrentDictionary<string, Nfs41OpenState>(StringComparer.Ordinal);
        private long nextToken;

        internal stateid4 Register(Nfs41ResolvedHandle handle, uint sequenceId)
        {
            ArgumentNullException.ThrowIfNull(handle);

            byte[] other = new byte[12];
            ulong token = unchecked((ulong)System.Threading.Interlocked.Increment(ref nextToken));
            BinaryPrimitives.WriteUInt64BigEndian(other.AsSpan(0, 8), token);
            uint pathHash = ComputePathHash(handle.Target.SourcePath);
            BinaryPrimitives.WriteUInt32BigEndian(other.AsSpan(8, 4), pathHash);

            stateid4 stateId = new stateid4
            {
                seqid = sequenceId,
                other = other,
            };

            openStates[CreateKey(stateId)] = new Nfs41OpenState(stateId, handle);
            return CloneStateId(stateId);
        }

        internal bool TryGet(stateid4? stateId, out Nfs41OpenState? openState)
        {
            string? key = TryCreateKey(stateId);
            if (key is null)
            {
                openState = null;
                return false;
            }

            return openStates.TryGetValue(key, out openState);
        }

        internal bool Close(stateid4? stateId, out stateid4? closedStateId)
        {
            string? key = TryCreateKey(stateId);
            if (key is null)
            {
                closedStateId = null;
                return false;
            }

            if (openStates.TryRemove(key, out Nfs41OpenState? openState) && openState is not null)
            {
                closedStateId = CloneStateId(openState.StateId);
                return true;
            }

            closedStateId = null;
            return false;
        }

        private static uint ComputePathHash(string value)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4));
        }

        private static string? TryCreateKey(stateid4? stateId)
        {
            if (stateId?.other is null || stateId.other.Length != 12)
            {
                return null;
            }

            return CreateKey(stateId);
        }

        private static string CreateKey(stateid4 stateId)
        {
            return stateId.seqid.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + Convert.ToHexString(stateId.other ?? Array.Empty<byte>());
        }

        private static stateid4 CloneStateId(stateid4 stateId)
        {
            byte[] other = new byte[12];
            Buffer.BlockCopy(stateId.other ?? Array.Empty<byte>(), 0, other, 0, other.Length);

            return new stateid4
            {
                seqid = stateId.seqid,
                other = other,
            };
        }
    }

    internal sealed class Nfs41OpenState
    {
        internal Nfs41OpenState(stateid4 stateId, Nfs41ResolvedHandle handle)
        {
            ArgumentNullException.ThrowIfNull(stateId);
            ArgumentNullException.ThrowIfNull(handle);

            StateId = stateId;
            Handle = handle;
        }

        internal stateid4 StateId { get; }

        internal Nfs41ResolvedHandle Handle { get; }
    }
}
