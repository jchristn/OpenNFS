namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV42Requests;

    /// <summary>
    /// Encapsulates grouped NFSv4.2 advisory, sparse-file, and same-server copy/clone flows.
    /// </summary>
    /// <remarks>
    /// The execute helpers establish and then reuse a client-scoped v4.2 session so healthy grouped
    /// calls pay the <c>EXCHANGE_ID</c> / <c>CREATE_SESSION</c> bootstrap cost only once per client
    /// instance. Each grouped call still sends a leading <c>SEQUENCE</c> followed by the target file
    /// operation COMPOUND on that dedicated grouped-session connection. When the underlying TCP
    /// transport breaks after session establishment, the grouped path reconnects and re-binds the
    /// existing session before replaying the interrupted sequenced call.
    /// The corresponding <c>Prepare*</c> helpers expose the inner file-operation COMPOUND plan only.
    /// For exact on-wire control over the session bootstrap, reconnect behavior, and final request
    /// shape, prefer the raw COMPOUND planning APIs on <see cref="OpenNfsClient"/> directly.
    /// </remarks>
    internal sealed class OpenNfsFileV42Apis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV42Apis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV42IoAdviseResult> IoAdviseV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong count,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? hints,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateIoAdviseV42Request(fileHandle, offset, count, hints),
                "NFSv4.2 IO_ADVISE",
                OpenNfsOperationIdempotency.Idempotent,
                ReadIoAdviseV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareIoAdviseV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong count,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? hints,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateIoAdviseV42Request(fileHandle, offset, count, hints),
                cancellationToken);
        }

        public OpenNfsV42IoAdviseResult ReadIoAdviseV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadIoAdviseResult(encodedReply);
        }

        public Task<OpenNfsV42ReadPlusResult> ReadPlusV42Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateReadPlusV42Request(fileHandle, offset, count),
                "NFSv4.2 READ_PLUS",
                OpenNfsOperationIdempotency.Idempotent,
                ReadReadPlusV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareReadPlusV42Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateReadPlusV42Request(fileHandle, offset, count),
                cancellationToken);
        }

        public OpenNfsV42ReadPlusResult ReadReadPlusV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadReadPlusResult(encodedReply);
        }

        public Task<OpenNfsV42SeekResult> SeekV42Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsV42SeekTarget target,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateSeekV42Request(fileHandle, offset, target),
                "NFSv4.2 SEEK",
                OpenNfsOperationIdempotency.Idempotent,
                ReadSeekV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareSeekV42Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsV42SeekTarget target,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateSeekV42Request(fileHandle, offset, target),
                cancellationToken);
        }

        public OpenNfsV42SeekResult ReadSeekV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadSeekResult(encodedReply);
        }

        public Task<OpenNfsV42AllocateResult> AllocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateAllocateV42Request(fileHandle, offset, length),
                "NFSv4.2 ALLOCATE",
                OpenNfsOperationIdempotency.NonIdempotent,
                ReadAllocateV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareAllocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateAllocateV42Request(fileHandle, offset, length),
                cancellationToken);
        }

        public OpenNfsV42AllocateResult ReadAllocateV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadAllocateResult(encodedReply);
        }

        public Task<OpenNfsV42CopyResult> CopyV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            bool consecutive,
            bool synchronous,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateCopyV42Request(sourceFileHandle, destinationFileHandle, sourceOffset, destinationOffset, count, consecutive, synchronous),
                "NFSv4.2 COPY",
                OpenNfsOperationIdempotency.NonIdempotent,
                ReadCopyV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareCopyV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            bool consecutive,
            bool synchronous,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCopyV42Request(sourceFileHandle, destinationFileHandle, sourceOffset, destinationOffset, count, consecutive, synchronous),
                cancellationToken);
        }

        public OpenNfsV42CopyResult ReadCopyV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadCopyResult(encodedReply);
        }

        public Task<OpenNfsV42CloneResult> CloneV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateCloneV42Request(sourceFileHandle, destinationFileHandle, sourceOffset, destinationOffset, count),
                "NFSv4.2 CLONE",
                OpenNfsOperationIdempotency.NonIdempotent,
                ReadCloneV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareCloneV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCloneV42Request(sourceFileHandle, destinationFileHandle, sourceOffset, destinationOffset, count),
                cancellationToken);
        }

        public OpenNfsV42CloneResult ReadCloneV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadCloneResult(encodedReply);
        }

        public Task<OpenNfsV42DeallocateResult> DeallocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteGroupedV42CompoundAsync(
                CreateDeallocateV42Request(fileHandle, offset, length),
                "NFSv4.2 DEALLOCATE",
                OpenNfsOperationIdempotency.NonIdempotent,
                ReadDeallocateV42Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareDeallocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateDeallocateV42Request(fileHandle, offset, length),
                cancellationToken);
        }

        public OpenNfsV42DeallocateResult ReadDeallocateV42Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV42FileReplyDecoder.ReadDeallocateResult(encodedReply);
        }
    }
}
