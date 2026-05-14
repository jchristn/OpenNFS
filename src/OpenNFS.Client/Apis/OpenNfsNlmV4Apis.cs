namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsLockApiNlmV4Requests;

    internal sealed class OpenNfsNlmV4Apis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsNlmV4Apis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareNlmV4ProcedureAsync(
            uint procedureNumber,
            byte[] procedurePayload,
            CancellationToken cancellationToken,
            OpenNfsRetryMode retryMode = OpenNfsRetryMode.UseClientPolicy)
        {
            byte[] safeProcedurePayload = OpenNfsClientArgument.RequireBytes(procedurePayload, nameof(procedurePayload), allowEmpty: true);
            return _client.PrepareV3ProcedureAsync(
                new OpenNfsV3ProcedureRequest(
                    procedureNumber: procedureNumber,
                    procedurePayload: safeProcedurePayload,
                    retryMode: retryMode,
                    programNumber: OpenNfsV3RpcConstants.NlmProgram,
                    versionNumber: OpenNfsV3RpcConstants.NlmVersion),
                cancellationToken);
        }

        public Task<OpenNfsNlmV4TestResult> TestV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateTestRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, exclusive),
                "NLM v4 TEST",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadTestV4Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareTestV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateTestRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, exclusive),
                cancellationToken);
        }

        public Task<OpenNfsNlmV4Result> LockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateLockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive, reclaim, state),
                "NLM v4 LOCK",
                block ? OpenNfsTransportPipelineIdempotency.NonIdempotent : OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLockV4Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareLockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateLockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive, reclaim, state),
                cancellationToken);
        }

        public Task<OpenNfsNlmV4Result> CancelV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateCancelRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive),
                "NLM v4 CANCEL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadCancelV4Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCancelV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateCancelRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive),
                cancellationToken);
        }

        public Task<OpenNfsNlmV4Result> UnlockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateUnlockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                "NLM v4 UNLOCK",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadUnlockV4Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareUnlockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateUnlockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                cancellationToken);
        }

        public OpenNfsNlmV4TestResult ReadTestV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadTestResult(encodedReply);
        }

        public OpenNfsNlmV4Result ReadLockV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 LOCK");
        }

        public OpenNfsNlmV4Result ReadCancelV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 CANCEL");
        }

        public OpenNfsNlmV4Result ReadUnlockV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 UNLOCK");
        }
    }
}
