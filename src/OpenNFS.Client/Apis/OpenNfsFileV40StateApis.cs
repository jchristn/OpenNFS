namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV3Requests;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40IoRequests;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40MetadataRequests;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV40StateRequests;

    /// <summary>
    /// Encapsulates grouped NFSv4.0 file open, close, and delegation-state flows.
    /// </summary>
    internal sealed class OpenNfsFileV40StateApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV40StateApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV40OpenResult> OpenExistingV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenExistingV40Request(directoryHandle, clientId, openOwner, entryName, shareAccess, shareDeny, sequenceId),
                "NFSv4.0 OPEN existing",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadOpenExistingV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped existing-file <c>OPEN</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareOpenExistingV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenExistingV40Request(directoryHandle, clientId, openOwner, entryName, shareAccess, shareDeny, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 existing-file <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40OpenResult ReadOpenExistingV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>OPEN</c> + <c>GETFH</c> + <c>GETATTR</c> reclaim flow for an existing file and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40OpenResult> ReclaimOpenV40Async(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateReclaimOpenV40Request(fileHandle, clientId, openOwner, shareAccess, shareDeny, sequenceId),
                "NFSv4.0 OPEN reclaim",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadReclaimOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped reclaim <c>OPEN</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareReclaimOpenV40Async(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateReclaimOpenV40Request(fileHandle, clientId, openOwner, shareAccess, shareDeny, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 reclaim <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40OpenResult ReadReclaimOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>OPEN</c> + <c>GETFH</c> + <c>GETATTR</c> flow for create-through-open and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40OpenResult> CreateAndOpenV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCreateAndOpenV40Request(
                    directoryHandle,
                    clientId,
                    openOwner,
                    entryName,
                    shareAccess,
                    shareDeny,
                    sequenceId,
                    failIfExists),
                "NFSv4.0 OPEN create",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateAndOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped create-through-open <c>OPEN</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCreateAndOpenV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCreateAndOpenV40Request(
                    directoryHandle,
                    clientId,
                    openOwner,
                    entryName,
                    shareAccess,
                    shareDeny,
                    sequenceId,
                    failIfExists),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 create-through-open <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40OpenResult ReadCreateAndOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>OPEN_CONFIRM</c> request and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40StateIdResult> ConfirmOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenConfirmV40Request(openStateId, sequenceId),
                "NFSv4.0 OPEN_CONFIRM",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadConfirmOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>PUTFH</c> + <c>OPEN_CONFIRM</c> request and decodes the typed result.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>OPEN_CONFIRM</c>.
        /// </summary>

        public Task<OpenNfsV40StateIdResult> ConfirmOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenConfirmV40Request(fileHandle, openStateId, sequenceId),
                "NFSv4.0 OPEN_CONFIRM",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadConfirmOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>OPEN_CONFIRM</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareConfirmOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateOpenConfirmV40Request(openStateId, sequenceId), cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>PUTFH</c> + <c>OPEN_CONFIRM</c> COMPOUND plan.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>OPEN_CONFIRM</c>.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareConfirmOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenConfirmV40Request(fileHandle, openStateId, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>OPEN_CONFIRM</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40StateIdResult ReadConfirmOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenConfirmResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>OPEN_DOWNGRADE</c> request and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40StateIdResult> DowngradeOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenDowngradeV40Request(openStateId, sequenceId, shareAccess, shareDeny),
                "NFSv4.0 OPEN_DOWNGRADE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadDowngradeOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>OPEN_DOWNGRADE</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareDowngradeOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenDowngradeV40Request(openStateId, sequenceId, shareAccess, shareDeny),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>OPEN_DOWNGRADE</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40StateIdResult ReadDowngradeOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenDowngradeResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>CLOSE</c> request and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40StateIdResult> CloseV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCloseV40Request(openStateId, sequenceId),
                "NFSv4.0 CLOSE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCloseV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>PUTFH</c> + <c>CLOSE</c> request and decodes the typed result.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>CLOSE</c>.
        /// </summary>

        public Task<OpenNfsV40StateIdResult> CloseV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCloseV40Request(fileHandle, openStateId, sequenceId),
                "NFSv4.0 CLOSE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCloseV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>CLOSE</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCloseV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateCloseV40Request(openStateId, sequenceId), cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>PUTFH</c> + <c>CLOSE</c> COMPOUND plan.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>CLOSE</c>.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCloseV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCloseV40Request(fileHandle, openStateId, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>CLOSE</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40StateIdResult ReadCloseV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadCloseResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>DELEGRETURN</c> request and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40DelegationReturnResult> ReturnDelegationV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateDelegationReturnV40Request(fileHandle, delegationStateId),
                "NFSv4.0 DELEGRETURN",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadReturnDelegationV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>DELEGRETURN</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareReturnDelegationV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateDelegationReturnV40Request(fileHandle, delegationStateId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>DELEGRETURN</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40DelegationReturnResult ReadReturnDelegationV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadDelegationReturnResult(encodedReply);
        }
    }
}
