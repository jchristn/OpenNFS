namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Provides grouped export-oriented convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class ExportApis
    {
        private readonly OpenNfsClient _client;

        internal ExportApis(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>EXPORT</c> plan for enumerating exported roots.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareListExportsV3Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateMountVoidRequest(OpenNfsV3RpcConstants.MountExportProcedure), cancellationToken);
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>DUMP</c> plan for enumerating current client mount records.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareListMountsV3Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateMountVoidRequest(OpenNfsV3RpcConstants.MountDumpProcedure), cancellationToken);
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>MNT</c> plan for mounting a specific export path.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareMountV3Async(string exportPath, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateMountPathRequest(OpenNfsV3RpcConstants.MountProcedure, exportPath), cancellationToken);
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>UMNT</c> plan for unmounting a specific export path.
        /// </summary>
        /// <param name="exportPath">Export path to unmount.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareUnmountV3Async(string exportPath, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateMountPathRequest(OpenNfsV3RpcConstants.MountUmountProcedure, exportPath), cancellationToken);
        }

        /// <summary>
        /// Prepares a MOUNT v3 <c>UMNTALL</c> plan.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareUnmountAllV3Async(CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateMountVoidRequest(OpenNfsV3RpcConstants.MountUmountAllProcedure), cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>EXPORT</c> request and decodes the typed export entries from the reply.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The decoded export entries.</returns>
        public Task<IReadOnlyList<OpenNfsExportV3Entry>> ListExportsV3Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateMountVoidRequest(OpenNfsV3RpcConstants.MountExportProcedure),
                "MOUNT v3 EXPORT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadListExportsV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>DUMP</c> request and decodes the typed mounted-export entries from the reply.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The decoded mounted-export entries.</returns>
        public Task<IReadOnlyList<OpenNfsMountedExportV3Entry>> ListMountsV3Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateMountVoidRequest(OpenNfsV3RpcConstants.MountDumpProcedure),
                "MOUNT v3 DUMP",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadListMountsV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>MNT</c> request and decodes the typed mount result from the reply.
        /// </summary>
        /// <param name="exportPath">Export path to mount.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>The decoded typed mount result.</returns>
        public Task<OpenNfsMountV3Result> MountV3Async(string exportPath, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateMountPathRequest(OpenNfsV3RpcConstants.MountProcedure, exportPath),
                "MOUNT v3 MNT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadMountV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>UMNT</c> request and validates the reply.
        /// </summary>
        /// <param name="exportPath">Export path to unmount.</param>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>A task that completes when the unmount reply has been validated.</returns>
        public Task UnmountV3Async(string exportPath, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateMountPathRequest(OpenNfsV3RpcConstants.MountUmountProcedure, exportPath),
                "MOUNT v3 UMNT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadUnmountV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes a MOUNT v3 <c>UMNTALL</c> request and validates the reply.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the request.</param>
        /// <returns>A task that completes when the unmount-all reply has been validated.</returns>
        public Task UnmountAllV3Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateMountVoidRequest(OpenNfsV3RpcConstants.MountUmountAllProcedure),
                "MOUNT v3 UMNTALL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadUnmountAllV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Decodes a full RPC reply for a MOUNT v3 <c>MNT</c> request into a typed result model.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <returns>The decoded typed MOUNT v3 result.</returns>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the RPC reply is rejected, accepted with a non-success RPC status, or carries malformed MOUNT payload data.
        /// </exception>
        public OpenNfsMountV3Result ReadMountV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsMountV3ReplyDecoder.ReadMountResult(encodedReply);
        }

        /// <summary>
        /// Decodes a full RPC reply for a MOUNT v3 <c>EXPORT</c> request into typed export entries.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <returns>The decoded export entries.</returns>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the RPC reply is rejected, accepted with a non-success RPC status, or carries malformed MOUNT payload data.
        /// </exception>
        public IReadOnlyList<OpenNfsExportV3Entry> ReadListExportsV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsMountV3ReplyDecoder.ReadExportList(encodedReply);
        }

        /// <summary>
        /// Decodes a full RPC reply for a MOUNT v3 <c>DUMP</c> request into typed mounted-export entries.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <returns>The decoded mounted-export entries.</returns>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the RPC reply is rejected, accepted with a non-success RPC status, or carries malformed MOUNT payload data.
        /// </exception>
        public IReadOnlyList<OpenNfsMountedExportV3Entry> ReadListMountsV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsMountV3ReplyDecoder.ReadMountedExportList(encodedReply);
        }

        /// <summary>
        /// Validates a full RPC reply for a MOUNT v3 <c>UMNT</c> request.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the RPC reply is rejected, accepted with a non-success RPC status, or carries an unexpected payload.
        /// </exception>
        public void ReadUnmountV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsMountV3ReplyDecoder.ValidateUnmountReply(encodedReply);
        }

        /// <summary>
        /// Validates a full RPC reply for a MOUNT v3 <c>UMNTALL</c> request.
        /// </summary>
        /// <param name="encodedReply">Full encoded ONC RPC reply bytes.</param>
        /// <exception cref="System.IO.InvalidDataException">
        /// Thrown when the RPC reply is rejected, accepted with a non-success RPC status, or carries an unexpected payload.
        /// </exception>
        public void ReadUnmountAllV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            OpenNfsMountV3ReplyDecoder.ValidateUnmountAllReply(encodedReply);
        }

        private static OpenNfsV3ProcedureRequest CreateMountPathRequest(uint procedureNumber, string exportPath)
        {
            string safeExportPath = OpenNfsClientArgument.RequireText(exportPath, nameof(exportPath));
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteString(safeExportPath);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray(),
                programNumber: OpenNfsV3RpcConstants.MountProgram,
                versionNumber: OpenNfsV3RpcConstants.MountVersion);
        }

        private static OpenNfsV3ProcedureRequest CreateMountVoidRequest(uint procedureNumber)
        {
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: [],
                programNumber: OpenNfsV3RpcConstants.MountProgram,
                versionNumber: OpenNfsV3RpcConstants.MountVersion);
        }
    }
}
