namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsDirectoryApiV3Requests;

    internal sealed class OpenNfsDirectoryV3ReadApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsDirectoryV3ReadApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3ReadDirectoryResult> ReadDirectoryV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateReadDirectoryRequest(directoryHandle, cookie, cookieVerifier, count),
                "NFSv3 READDIR",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadDirectoryV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadDirectoryV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateReadDirectoryRequest(directoryHandle, cookie, cookieVerifier, count),
                cancellationToken);
        }

        public OpenNfsV3ReadDirectoryResult ReadReadDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadDirectoryResult(encodedReply);
        }

        public Task<OpenNfsV3ReadDirectoryPlusResult> ReadDirectoryPlusV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateReadDirectoryPlusRequest(directoryHandle, cookie, cookieVerifier, directoryCount, maxCount),
                "NFSv3 READDIRPLUS",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadDirectoryPlusV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareReadDirectoryPlusV3Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateReadDirectoryPlusRequest(directoryHandle, cookie, cookieVerifier, directoryCount, maxCount),
                cancellationToken);
        }

        public OpenNfsV3ReadDirectoryPlusResult ReadReadDirectoryPlusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadDirectoryPlusResult(encodedReply);
        }
    }
}
