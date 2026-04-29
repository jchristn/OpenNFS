namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Provides grouped administrative convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class AdministrationApis
    {
        private readonly OpenNfsClient _client;

        internal AdministrationApis(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Prepares an NFSv3 <c>NULL</c> plan for connectivity checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PreparePingNfsV3Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateNullRequest(
                OpenNfsV3RpcConstants.NfsNullProcedure,
                OpenNfsV3RpcConstants.NfsProgram,
                OpenNfsV3RpcConstants.NfsVersion),
                cancellationToken);
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>NULL</c> plan for connectivity checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PreparePingMountV3Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateNullRequest(
                OpenNfsV3RpcConstants.MountNullProcedure,
                OpenNfsV3RpcConstants.MountProgram,
                OpenNfsV3RpcConstants.MountVersion),
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NLM v4 <c>NULL</c> plan for connectivity checks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PreparePingNlmV4Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateNullRequest(
                OpenNfsV3RpcConstants.NlmNullProcedure,
                OpenNfsV3RpcConstants.NlmProgram,
                OpenNfsV3RpcConstants.NlmVersion),
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv3 <c>NULL</c> request for connectivity validation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>A task that completes when the reply has been validated.</returns>
        public Task PingNfsV3Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateNullRequest(
                    OpenNfsV3RpcConstants.NfsNullProcedure,
                    OpenNfsV3RpcConstants.NfsProgram,
                    OpenNfsV3RpcConstants.NfsVersion),
                "NFSv3 NULL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadPingNfsV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>NULL</c> request for connectivity validation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>A task that completes when the reply has been validated.</returns>
        public Task PingMountV3Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateNullRequest(
                    OpenNfsV3RpcConstants.MountNullProcedure,
                    OpenNfsV3RpcConstants.MountProgram,
                    OpenNfsV3RpcConstants.MountVersion),
                "MOUNT v3 NULL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadPingMountV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NLM v4 <c>NULL</c> request for connectivity validation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>A task that completes when the reply has been validated.</returns>
        public Task PingNlmV4Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateNullRequest(
                    OpenNfsV3RpcConstants.NlmNullProcedure,
                    OpenNfsV3RpcConstants.NlmProgram,
                    OpenNfsV3RpcConstants.NlmVersion),
                "NLM v4 NULL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadPingNlmV4Result,
                cancellationToken);
        }

        /// <summary>
        /// Validates a full RPC reply for an NFSv3 <c>NULL</c> request.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        public void ReadPingNfsV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(encodedReply, "NFSv3 NULL");
        }

        /// <summary>
        /// Validates a full RPC reply for a MOUNT v3 <c>NULL</c> request.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        public void ReadPingMountV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(encodedReply, "MOUNT v3 NULL");
        }

        /// <summary>
        /// Validates a full RPC reply for an NLM v4 <c>NULL</c> request.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        public void ReadPingNlmV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(encodedReply, "NLM v4 NULL");
        }

        private static OpenNfsV3ProcedureRequest CreateNullRequest(uint procedureNumber, ulong programNumber, ulong versionNumber)
        {
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: [],
                programNumber: programNumber,
                versionNumber: versionNumber);
        }
    }
}
