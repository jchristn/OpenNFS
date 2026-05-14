namespace OpenNFS.Protocol.V3.Nlm
{
    using System;
    using System.Threading;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal sealed class PreparedLockRequest
    {
        private PreparedLockRequest(NfsLockRequest? request, NfsLockResponse? earlyResponse, byte[]? fileHandleBytes)
        {
            Request = request;
            EarlyResponse = earlyResponse;
            FileHandleBytes = fileHandleBytes;
        }

        internal NfsLockRequest? Request { get; }

        internal NfsLockResponse? EarlyResponse { get; }

        internal byte[]? FileHandleBytes { get; }

        internal static PreparedLockRequest FromDisposition(NfsLockDisposition disposition)
        {
            return new PreparedLockRequest(request: null, new NfsLockResponse(disposition), fileHandleBytes: null);
        }

        internal static PreparedLockRequest FromRequest(NfsLockRequest request, byte[] fileHandleBytes)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(fileHandleBytes);
            return new PreparedLockRequest(request, earlyResponse: null, fileHandleBytes.AsSpan().ToArray());
        }
    }

    internal readonly struct TestLikeRequestResult
    {
        internal TestLikeRequestResult(NfsLockResponse response, byte[] cookie)
        {
            Response = response;
            Cookie = cookie;
        }

        internal NfsLockResponse Response { get; }

        internal byte[] Cookie { get; }
    }

    internal sealed class PendingBlockedLock
    {
        private readonly byte[] _cookie;
        private readonly byte[] _fileHandle;

        private PendingBlockedLock(byte[] cookie, byte[] fileHandle, NfsLockRequest request)
        {
            _cookie = cookie;
            _fileHandle = fileHandle;
            Request = request;
        }

        internal NfsLockRequest Request { get; }

        internal static PendingBlockedLock FromRequest(byte[] cookie, byte[] fileHandle, NfsLockRequest request)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            ArgumentNullException.ThrowIfNull(fileHandle);
            ArgumentNullException.ThrowIfNull(request);
            return new PendingBlockedLock(cookie.AsSpan().ToArray(), fileHandle.AsSpan().ToArray(), request);
        }

        internal bool MatchesIdentity(NfsLockRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return string.Equals(Request.Target.SourcePath, request.Target.SourcePath, StringComparison.Ordinal)
                && string.Equals(Request.Target.ExportPath, request.Target.ExportPath, StringComparison.Ordinal)
                && Request.Range.Offset == request.Range.Offset
                && Request.Range.Length == request.Range.Length
                && Request.Exclusive == request.Exclusive
                && Request.Owner.ProcessId == request.Owner.ProcessId
                && string.Equals(Request.Owner.CallerName, request.Owner.CallerName, StringComparison.Ordinal)
                && Request.Owner.OwnerHandle.Span.SequenceEqual(request.Owner.OwnerHandle.Span);
        }

        internal NfsLockRequest CreateTestRequest(CancellationToken cancellationToken)
        {
            return new NfsLockRequest(
                NfsLockOperation.Test,
                Request.Target,
                Request.Owner,
                Request.Range,
                Request.Exclusive,
                block: false,
                Request.Reclaim,
                Request.State,
                cancellationToken);
        }

        internal NfsLockRequest CreateGrantRequest(CancellationToken cancellationToken)
        {
            return new NfsLockRequest(
                NfsLockOperation.Lock,
                Request.Target,
                Request.Owner,
                Request.Range,
                Request.Exclusive,
                block: false,
                Request.Reclaim,
                Request.State,
                cancellationToken);
        }

        internal NfsLockRequest CreateCancelRequest(CancellationToken cancellationToken)
        {
            return new NfsLockRequest(
                NfsLockOperation.Cancel,
                Request.Target,
                Request.Owner,
                Request.Range,
                Request.Exclusive,
                block: true,
                Request.Reclaim,
                Request.State,
                cancellationToken);
        }

        internal NlmV4GrantedCallback CreateGrantedCallback()
        {
            return new NlmV4GrantedCallback(
                Request.Owner.CallerName,
                _cookie,
                _fileHandle,
                Request.Owner.OwnerHandle,
                Request.Owner.ProcessId,
                Request.Range.Offset,
                Request.Range.Length,
                Request.Exclusive);
        }
    }
}
