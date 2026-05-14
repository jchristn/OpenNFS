namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsFileApiV3Requests;

    internal sealed class OpenNfsFileV3FileSystemApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV3FileSystemApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3FileSystemStatusResult> GetFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsStatProcedure),
                "NFSv3 FSSTAT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadFileSystemStatusV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsStatProcedure),
                cancellationToken);
        }

        public OpenNfsV3FileSystemStatusResult ReadFileSystemStatusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadFileSystemStatusResult(encodedReply);
        }

        public Task<OpenNfsV3FileSystemInfoResult> GetFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsInfoProcedure),
                "NFSv3 FSINFO",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadFileSystemInfoV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsInfoProcedure),
                cancellationToken);
        }

        public OpenNfsV3FileSystemInfoResult ReadFileSystemInfoV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadFileSystemInfoResult(encodedReply);
        }

        public Task<OpenNfsV3PathConfigurationResult> GetPathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsPathConfProcedure),
                "NFSv3 PATHCONF",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadPathConfigurationV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PreparePathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsPathConfProcedure),
                cancellationToken);
        }

        public OpenNfsV3PathConfigurationResult ReadPathConfigurationV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadPathConfigurationResult(encodedReply);
        }
    }
}
