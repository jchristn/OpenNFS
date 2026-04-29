namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Rpc.RpcMessages;

    internal sealed class ScriptedRpcExecutor : IOpenNfsRpcExecutor
    {
        private readonly Func<OpenNfsRpcExecutionRequest, OpenNfsTransportPipelineAttempt, CancellationToken, Task<RpcMessageEnvelope>> _executeAsync;
        private readonly List<OpenNfsTransportPipelineAttempt> _attempts = new List<OpenNfsTransportPipelineAttempt>();
        private readonly List<OpenNfsRpcExecutionRequest> _requests = new List<OpenNfsRpcExecutionRequest>();

        public ScriptedRpcExecutor(
            Func<OpenNfsRpcExecutionRequest, OpenNfsTransportPipelineAttempt, CancellationToken, Task<RpcMessageEnvelope>> executeAsync)
        {
            ArgumentNullException.ThrowIfNull(executeAsync);
            _executeAsync = executeAsync;
        }

        public IReadOnlyList<OpenNfsTransportPipelineAttempt> Attempts => _attempts;

        public IReadOnlyList<OpenNfsRpcExecutionRequest> Requests => _requests;

        public Task<RpcMessageEnvelope> ExecuteAsync(
            OpenNfsRpcExecutionRequest request,
            OpenNfsTransportPipelineAttempt attempt,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(attempt);

            _requests.Add(request);
            _attempts.Add(attempt);
            return _executeAsync(request, attempt, cancellationToken);
        }
    }
}
