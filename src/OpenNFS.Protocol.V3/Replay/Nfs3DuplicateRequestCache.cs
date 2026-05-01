namespace OpenNFS.Protocol.V3.Replay
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Security.Cryptography;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Replay;
    using OpenNFS.Rpc.RpcMessages;

    internal sealed class Nfs3DuplicateRequestCache
    {
        private readonly RpcReplayCache<RpcRequestCorrelationKey, ReplayEntry> _cache;
        private readonly Func<DateTimeOffset> _utcNow;

        internal Nfs3DuplicateRequestCache(
            TimeSpan? entryLifetime = null,
            Func<DateTimeOffset>? utcNow = null)
        {
            _cache = new RpcReplayCache<RpcRequestCorrelationKey, ReplayEntry>(entryLifetime ?? TimeSpan.FromMinutes(5));
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        internal bool TryGetReplay(RpcMessageEnvelope request, out RpcMessageEnvelope? reply)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!CanCache(request)
                || !TryCreateCorrelationKey(request, out RpcRequestCorrelationKey key))
            {
                reply = null;
                return false;
            }

            byte[] requestFingerprint = CreateRequestFingerprint(request);
            if (!_cache.TryGet(key, _utcNow(), out ReplayEntry? replayEntry) || replayEntry is null)
            {
                reply = null;
                return false;
            }

            if (!replayEntry.RequestFingerprint.Span.SequenceEqual(requestFingerprint))
            {
                reply = null;
                return false;
            }

            reply = RpcMessageCodec.Decode(replayEntry.ReplyBytes);
            return true;
        }

        internal void StoreReply(RpcMessageEnvelope request, RpcMessageEnvelope reply)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(reply);

            if (!CanCache(request)
                || !TryCreateCorrelationKey(request, out RpcRequestCorrelationKey key))
            {
                return;
            }

            _cache.Store(
                key,
                new ReplayEntry(CreateRequestFingerprint(request), RpcMessageCodec.Encode(reply)),
                _utcNow());
        }

        private static void AppendBuffer(IncrementalHash hash, ReadOnlySpan<byte> value)
        {
            Span<byte> lengthBuffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, value.Length);
            hash.AppendData(lengthBuffer);
            hash.AppendData(value);
        }

        private static void AppendUInt32(IncrementalHash hash, uint value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            hash.AppendData(buffer);
        }

        private static bool CanCache(RpcMessageEnvelope request)
        {
            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                return false;
            }

            return body.cbody.prog == (uint)NFS_PROGRAM_Program.Program
                && body.cbody.vers == (uint)NFS_PROGRAM_Program.Version_NFS_V3
                && body.cbody.proc != (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_NULL;
        }

        private static byte[] CreateRequestFingerprint(RpcMessageEnvelope request)
        {
            rpc_msg_body body = request.Header.body
                ?? throw new InvalidDataException("Duplicate-request fingerprinting requires an RPC message body.");
            call_body callBody = body.cbody
                ?? throw new InvalidDataException("Duplicate-request fingerprinting requires an RPC call body.");

            opaque_auth credential = callBody.cred ?? RpcAuthenticationCodec.CreateNone();
            opaque_auth verifier = callBody.verf ?? RpcAuthenticationCodec.CreateNone();

            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendUInt32(hash, (uint)(credential.flavor ?? auth_flavor.AUTH_NONE));
            AppendBuffer(hash, credential.body ?? Array.Empty<byte>());
            AppendUInt32(hash, (uint)(verifier.flavor ?? auth_flavor.AUTH_NONE));
            AppendBuffer(hash, verifier.body ?? Array.Empty<byte>());
            AppendBuffer(hash, request.ProcedurePayload.Span);
            return hash.GetHashAndReset();
        }

        private static string CreateRequesterIdentity(opaque_auth credential)
        {
            if (credential.flavor == auth_flavor.AUTH_SYS)
            {
                try
                {
                    authsys_parms parameters = RpcAuthenticationCodec.ReadSystem(credential);
                    uint[] groups = parameters.gids ?? Array.Empty<uint>();
                    return "auth-sys:"
                        + parameters.machinename
                        + ":"
                        + parameters.uid.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ":"
                        + parameters.gid.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ":"
                        + string.Join(",", groups);
                }
                catch (InvalidDataException)
                {
                }
            }

            byte[] credentialBody = credential.body ?? Array.Empty<byte>();
            string bodyHash = credentialBody.Length == 0
                ? "empty"
                : Convert.ToHexString(SHA256.HashData(credentialBody));
            return "auth-"
                + ((int)(credential.flavor ?? auth_flavor.AUTH_NONE)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + bodyHash;
        }

        private static string CreateStableRequesterIdentity(RpcMessageEnvelope request, opaque_auth credential)
        {
            string authRequesterIdentity = CreateRequesterIdentity(credential);
            if (string.IsNullOrWhiteSpace(request.RequesterIdentity))
            {
                return authRequesterIdentity;
            }

            if (credential.flavor != auth_flavor.AUTH_SYS)
            {
                return request.RequesterIdentity!;
            }

            if (!TryNormalizeTransportHost(request.RequesterIdentity!, out string? transportHost))
            {
                return authRequesterIdentity;
            }

            return "transport-host:" + transportHost + "|" + authRequesterIdentity;
        }

        private static bool TryNormalizeTransportHost(string requesterIdentity, out string? transportHost)
        {
            transportHost = null;
            if (string.IsNullOrWhiteSpace(requesterIdentity))
            {
                return false;
            }

            string trimmedIdentity = requesterIdentity.Trim();
            if (trimmedIdentity.StartsWith("[", StringComparison.Ordinal))
            {
                int closingBracketIndex = trimmedIdentity.IndexOf(']');
                if (closingBracketIndex <= 1)
                {
                    return false;
                }

                transportHost = trimmedIdentity.Substring(1, closingBracketIndex - 1);
                return transportHost.Length > 0;
            }

            int lastColonIndex = trimmedIdentity.LastIndexOf(':');
            if (lastColonIndex <= 0)
            {
                transportHost = trimmedIdentity;
                return transportHost.Length > 0;
            }

            transportHost = trimmedIdentity.Substring(0, lastColonIndex);
            return transportHost.Length > 0;
        }

        private static bool TryCreateCorrelationKey(RpcMessageEnvelope request, out RpcRequestCorrelationKey key)
        {
            rpc_msg_body? body = request.Header.body;
            if (body?.mtype != msg_type.CALL || body.cbody is null)
            {
                key = default;
                return false;
            }

            call_body callBody = body.cbody;
            opaque_auth credential = callBody.cred ?? RpcAuthenticationCodec.CreateNone();
            string requesterIdentity = CreateStableRequesterIdentity(request, credential);

            key = new RpcRequestCorrelationKey(
                requesterIdentity,
                request.Header.xid,
                callBody.prog,
                callBody.vers,
                callBody.proc);
            return true;
        }

        private sealed class ReplayEntry
        {
            private readonly byte[] _replyBytes;
            private readonly byte[] _requestFingerprint;

            internal ReplayEntry(byte[] requestFingerprint, byte[] replyBytes)
            {
                ArgumentNullException.ThrowIfNull(requestFingerprint);
                ArgumentNullException.ThrowIfNull(replyBytes);
                _requestFingerprint = requestFingerprint.AsSpan().ToArray();
                _replyBytes = replyBytes.AsSpan().ToArray();
            }

            internal ReadOnlyMemory<byte> ReplyBytes => _replyBytes;

            internal ReadOnlyMemory<byte> RequestFingerprint => _requestFingerprint;
        }
    }
}
