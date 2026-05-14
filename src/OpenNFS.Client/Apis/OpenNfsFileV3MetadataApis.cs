namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV3Requests;

    internal sealed class OpenNfsFileV3MetadataApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV3MetadataApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3GetAttributesResult> GetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsGetattrProcedure),
                "NFSv3 GETATTR",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadGetAttributesV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareGetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsGetattrProcedure),
                cancellationToken);
        }

        public OpenNfsV3GetAttributesResult ReadGetAttributesV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadGetAttributesResult(encodedReply);
        }

        public Task<OpenNfsV3AccessResult> AccessV3Async(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateAccessRequest(fileHandle, requestedAccess),
                "NFSv3 ACCESS",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadAccessV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareAccessV3Async(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateAccessRequest(fileHandle, requestedAccess), cancellationToken);
        }

        public OpenNfsV3AccessResult ReadAccessV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadAccessResult(encodedReply);
        }
    }
}
