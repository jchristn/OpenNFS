namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV3Requests;

    internal sealed class OpenNfsFileV3IoApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV3IoApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3ReadResult> ReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateReadRequest(fileHandle, offset, count),
                "NFSv3 READ",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateReadRequest(fileHandle, offset, count), cancellationToken);
        }

        public OpenNfsV3ReadResult ReadReadV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadResult(encodedReply);
        }

        public Task<OpenNfsV3ReadLinkResult> ReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(symbolicLinkHandle, OpenNfsV3RpcConstants.NfsReadLinkProcedure),
                "NFSv3 READLINK",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadLinkV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(symbolicLinkHandle, OpenNfsV3RpcConstants.NfsReadLinkProcedure),
                cancellationToken);
        }

        public OpenNfsV3ReadLinkResult ReadReadLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadLinkResult(encodedReply);
        }

        public Task<OpenNfsV3WriteResult> WriteV3Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateWriteRequest(fileHandle, offset, stability, data),
                "NFSv3 WRITE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadWriteV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareWriteV3Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateWriteRequest(fileHandle, offset, stability, data), cancellationToken);
        }

        public OpenNfsV3WriteResult ReadWriteV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadWriteResult(encodedReply);
        }

        public Task<OpenNfsV3CommitResult> CommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateCommitRequest(fileHandle, offset, count),
                "NFSv3 COMMIT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadCommitV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateCommitRequest(fileHandle, offset, count), cancellationToken);
        }

        public OpenNfsV3CommitResult ReadCommitV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCommitResult(encodedReply);
        }
    }
}
