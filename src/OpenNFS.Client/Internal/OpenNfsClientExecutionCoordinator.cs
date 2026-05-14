namespace OpenNFS.Client.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;

    internal sealed class OpenNfsClientExecutionCoordinator
    {
        private readonly OpenNfsClientExecutionEngine _executionEngine;
        private readonly OpenNfsClientPlanFactory _planFactory;

        internal OpenNfsClientExecutionCoordinator(
            OpenNfsClientExecutionEngine executionEngine,
            OpenNfsClientPlanFactory planFactory)
        {
            _executionEngine = executionEngine;
            _planFactory = planFactory;
        }

        internal async Task<OpenNfsCompoundReply> ExecuteCompoundAsync(
            OpenNfsCompoundRequest request,
            OpenNfsOperationIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsCompoundPlan plan = await _planFactory.PrepareCompoundAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            string operationName = BuildRawCompoundOperationName(plan);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedCompoundAsync(
                plan,
                operationName,
                ConvertIdempotency(idempotency),
                cancellationToken).ConfigureAwait(false);
            return new OpenNfsCompoundReply(plan, operationName, encodedReply.ToArray());
        }

        internal async Task<TResult> ExecuteCompoundAsync<TResult>(
            OpenNfsCompoundRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            ArgumentNullException.ThrowIfNull(decodeReply);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsCompoundPlan plan = await _planFactory.PrepareCompoundAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedCompoundAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        internal async Task ExecuteMountV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Action<ReadOnlyMemory<byte>> validateReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(validateReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteMountV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            ValidateResult(encodedReply, operationName, validateReply);
        }

        internal async Task<TResult> ExecuteMountV3ProcedureAsync<TResult>(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(decodeReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteMountV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        internal async Task<OpenNfsV3ProcedureReply> ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            OpenNfsOperationIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await _planFactory.PrepareV3ProcedureAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            string operationName = BuildRawV3OperationName(plan);
            ReadOnlyMemory<byte> encodedReply = await ExecutePreparedV3ProcedureAsync(
                plan,
                operationName,
                ConvertIdempotency(idempotency),
                cancellationToken).ConfigureAwait(false);
            return new OpenNfsV3ProcedureReply(plan, operationName, encodedReply.ToArray());
        }

        internal async Task ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Action<ReadOnlyMemory<byte>> validateReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(validateReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            ValidateResult(encodedReply, operationName, validateReply);
        }

        internal async Task<TResult> ExecuteV3ProcedureAsync<TResult>(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(decodeReply);

            ReadOnlyMemory<byte> encodedReply = await ExecuteV3ProcedureAsync(
                request,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
            return DecodeResult(encodedReply, operationName, decodeReply);
        }

        private static string BuildRawCompoundOperationName(OpenNfsCompoundPlan plan)
        {
            return "Raw NFSv4." + plan.MinorVersion + " COMPOUND";
        }

        private static string BuildRawV3OperationName(OpenNfsV3ProcedurePlan plan)
        {
            return "Raw RPC program " + plan.ProgramNumber
                + " version " + plan.VersionNumber
                + " procedure " + plan.ProcedureNumber;
        }

        private static OpenNfsTransportPipelineIdempotency ConvertIdempotency(OpenNfsOperationIdempotency idempotency)
        {
            switch (idempotency)
            {
                case OpenNfsOperationIdempotency.NonIdempotent:
                    return OpenNfsTransportPipelineIdempotency.NonIdempotent;
                case OpenNfsOperationIdempotency.Idempotent:
                    return OpenNfsTransportPipelineIdempotency.Idempotent;
                default:
                    throw new OpenNfsClientProtocolException(
                        "The client execution path does not support idempotency mode '"
                        + idempotency.ToString() + "'.");
            }
        }

        private static TResult DecodeResult<TResult>(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply)
        {
            try
            {
                return decodeReply(encodedReply);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw OpenNfsClientExecutionEngine.CreateProtocolException(
                    operationName + " returned a malformed or unsupported reply payload.",
                    operationName,
                    exception,
                    isRetryable: false);
            }
        }

        private static void ValidateResult(
            ReadOnlyMemory<byte> encodedReply,
            string operationName,
            Action<ReadOnlyMemory<byte>> validateReply)
        {
            try
            {
                validateReply(encodedReply);
            }
            catch (Exception exception) when (exception is not OpenNfsClientException)
            {
                throw OpenNfsClientExecutionEngine.CreateProtocolException(
                    operationName + " returned a malformed or unsupported reply payload.",
                    operationName,
                    exception,
                    isRetryable: false);
            }
        }

        private async Task<ReadOnlyMemory<byte>> ExecuteMountV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await _planFactory.PrepareMountV3ProcedureAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            return await ExecutePreparedV3ProcedureAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReadOnlyMemory<byte>> ExecutePreparedCompoundAsync(
            OpenNfsCompoundPlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            return await _executionEngine.ExecutePreparedCompoundAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReadOnlyMemory<byte>> ExecutePreparedV3ProcedureAsync(
            OpenNfsV3ProcedurePlan plan,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            return await _executionEngine.ExecutePreparedV3ProcedureAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<ReadOnlyMemory<byte>> ExecuteV3ProcedureAsync(
            OpenNfsV3ProcedureRequest request,
            string operationName,
            OpenNfsTransportPipelineIdempotency idempotency,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(operationName);
            cancellationToken.ThrowIfCancellationRequested();

            OpenNfsV3ProcedurePlan plan = await _planFactory.PrepareV3ProcedureAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            return await ExecutePreparedV3ProcedureAsync(
                plan,
                operationName,
                idempotency,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
