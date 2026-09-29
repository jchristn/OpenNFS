namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    /// <summary>
    /// Client-side RPC executor that forwards calls to the real network executor while letting a test rewrite
    /// NFSv3 call payloads before they are sent and reply payloads before they are decoded.
    /// Every NFSv3 call is recorded so tests can assert on the exact procedure sequence the client issued.
    /// </summary>
    internal sealed class InterceptingRpcExecutor : IOpenNfsRpcExecutor, IAsyncDisposable
    {
        internal const uint NfsProgram = 100003;

        private readonly ConcurrentQueue<InterceptedNfsCall> _calls = new ConcurrentQueue<InterceptedNfsCall>();
        private readonly OpenNfsNetworkRpcExecutor _inner = new OpenNfsNetworkRpcExecutor();

        /// <summary>
        /// Gets or sets an optional call rewriter. It receives the NFSv3 procedure number and the procedure payload and
        /// returns the payload to send (or the original payload to leave the call unchanged).
        /// </summary>
        internal Func<uint, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? RewriteCall { get; set; }

        /// <summary>
        /// Gets or sets an optional reply rewriter. It receives the NFSv3 procedure number, the (possibly rewritten) call payload,
        /// and the reply procedure payload and returns the reply payload the client should decode.
        /// </summary>
        internal Func<uint, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? RewriteReply { get; set; }

        /// <summary>
        /// Gets or sets an optional asynchronous hook invoked before an NFSv3 call is forwarded (for example to delay a call
        /// until the caller's cancellation token fires).
        /// </summary>
        internal Func<uint, CancellationToken, Task>? BeforeSendAsync { get; set; }

        internal IReadOnlyList<InterceptedNfsCall> Calls => _calls.ToArray();

        internal OpenNfsRpcConnectionPool Pool => _inner.TcpPool;

        public ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }

        internal static OpenNfsClient CreateClient(OpenNfsClientSettings settings, InterceptingRpcExecutor executor)
        {
            return new OpenNfsClient(settings, executor, transportPipeline: null);
        }

        internal IReadOnlyList<InterceptedNfsCall> CallsFor(uint procedure)
        {
            return _calls.Where(call => call.Procedure == procedure).ToArray();
        }

        public async Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            call_body? callBody = request.CallEnvelope.Header.body?.cbody;
            if (callBody is null || callBody.prog != NfsProgram || callBody.vers != 3)
            {
                return await _inner.ExecuteAsync(request, attempt, cancellationToken).ConfigureAwait(false);
            }

            uint procedure = callBody.proc;
            ReadOnlyMemory<byte> callPayload = request.CallEnvelope.ProcedurePayload;
            OpenNfsRpcExecutionRequest effectiveRequest = request;

            Func<uint, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? rewriteCall = RewriteCall;
            if (rewriteCall is not null)
            {
                ReadOnlyMemory<byte> rewrittenPayload = rewriteCall(procedure, callPayload);
                if (!rewrittenPayload.Span.SequenceEqual(callPayload.Span))
                {
                    callPayload = rewrittenPayload;
                    effectiveRequest = new OpenNfsRpcExecutionRequest(
                        request.OperationName,
                        request.ProgramBinding,
                        request.TransportPolicy,
                        new RpcMessageEnvelope(request.CallEnvelope.Header, rewrittenPayload));
                }
            }

            _calls.Enqueue(new InterceptedNfsCall(procedure, callPayload.ToArray()));

            Func<uint, CancellationToken, Task>? beforeSendAsync = BeforeSendAsync;
            if (beforeSendAsync is not null)
            {
                await beforeSendAsync(procedure, cancellationToken).ConfigureAwait(false);
            }

            RpcMessageEnvelope reply = await _inner.ExecuteAsync(effectiveRequest, attempt, cancellationToken).ConfigureAwait(false);

            Func<uint, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? rewriteReply = RewriteReply;
            if (rewriteReply is null || reply.ProcedurePayload.Length == 0)
            {
                return reply;
            }

            ReadOnlyMemory<byte> rewrittenReply = rewriteReply(procedure, callPayload, reply.ProcedurePayload);
            return new RpcMessageEnvelope(reply.Header, rewrittenReply);
        }
    }

    /// <summary>
    /// One NFSv3 call observed by <see cref="InterceptingRpcExecutor"/>.
    /// </summary>
    internal sealed class InterceptedNfsCall
    {
        internal InterceptedNfsCall(uint procedure, byte[] payload)
        {
            Procedure = procedure;
            Payload = payload;
        }

        internal uint Procedure { get; }

        internal byte[] Payload { get; }
    }
}
