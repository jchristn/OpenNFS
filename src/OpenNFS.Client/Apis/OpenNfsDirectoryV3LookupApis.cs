namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsDirectoryApiV3Requests;

    internal sealed class OpenNfsDirectoryV3LookupApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsDirectoryV3LookupApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3LookupResult> LookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateLookupRequest(directoryHandle, entryName),
                "NFSv3 LOOKUP",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLookupV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareLookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateLookupRequest(directoryHandle, entryName), cancellationToken);
        }

        public OpenNfsV3LookupResult ReadLookupV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadLookupResult(encodedReply);
        }
    }
}
