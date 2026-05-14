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
    /// Encapsulates grouped NFSv4.0 file I/O flows.
    /// </summary>
    internal sealed class OpenNfsFileV40IoApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV40IoApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV40ReadResult> ReadV40Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateReadV40Request(fileHandle, offset, count),
                "NFSv4.0 READ",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>READ</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareReadV40Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateReadV40Request(fileHandle, offset, count), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>READ</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40ReadResult ReadReadV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadReadResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>WRITE</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40WriteResult> WriteV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId stateId,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateWriteV40Request(fileHandle, stateId, offset, stability, data),
                "NFSv4.0 WRITE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadWriteV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>WRITE</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareWriteV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId stateId,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateWriteV40Request(fileHandle, stateId, offset, stability, data),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>WRITE</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40WriteResult ReadWriteV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadWriteResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>COMMIT</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40CommitResult> CommitV40Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCommitV40Request(fileHandle, offset, count),
                "NFSv4.0 COMMIT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadCommitV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>COMMIT</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCommitV40Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCommitV40Request(fileHandle, offset, count),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>COMMIT</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40CommitResult ReadCommitV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadCommitResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>READLINK</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40ReadLinkResult> ReadLinkV40Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateReadLinkV40Request(symbolicLinkHandle),
                "NFSv4.0 READLINK",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadLinkV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>READLINK</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareReadLinkV40Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateReadLinkV40Request(symbolicLinkHandle), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>READLINK</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40ReadLinkResult ReadReadLinkV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadReadLinkResult(encodedReply);
        }
    }
}
