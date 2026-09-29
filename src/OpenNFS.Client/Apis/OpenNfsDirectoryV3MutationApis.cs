namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsDirectoryApiV3Requests;

    internal sealed class OpenNfsDirectoryV3MutationApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsDirectoryV3MutationApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV3CreatePathResult> CreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateFileRequest(directoryHandle, entryName, failIfExists, _client.Settings.DefaultFileCreateMode),
                "NFSv3 CREATE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateFileV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateFileRequest(directoryHandle, entryName, failIfExists, _client.Settings.DefaultFileCreateMode), cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreatePathResult(encodedReply);
        }

        public Task<OpenNfsV3CreatePathResult> CreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateDirectoryRequest(directoryHandle, entryName, _client.Settings.DefaultDirectoryCreateMode),
                "NFSv3 MKDIR",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateDirectoryV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateDirectoryRequest(directoryHandle, entryName, _client.Settings.DefaultDirectoryCreateMode), cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreateDirectoryResult(encodedReply);
        }

        public Task<OpenNfsV3DirectoryMutationResult> RemoveFileV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateRemoveFileRequest(directoryHandle, entryName),
                "NFSv3 REMOVE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadRemoveFileV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveFileV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateRemoveFileRequest(directoryHandle, entryName), cancellationToken);
        }

        public OpenNfsV3DirectoryMutationResult ReadRemoveFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRemoveFileResult(encodedReply);
        }

        public Task<OpenNfsV3DirectoryMutationResult> RemoveDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateRemoveDirectoryRequest(directoryHandle, entryName),
                "NFSv3 RMDIR",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadRemoveDirectoryV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateRemoveDirectoryRequest(directoryHandle, entryName), cancellationToken);
        }

        public OpenNfsV3DirectoryMutationResult ReadRemoveDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRemoveDirectoryResult(encodedReply);
        }

        public Task<OpenNfsV3RenameResult> RenameV3Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateRenameRequest(sourceDirectoryHandle, sourceEntryName, destinationDirectoryHandle, destinationEntryName),
                "NFSv3 RENAME",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadRenameV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareRenameV3Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateRenameRequest(sourceDirectoryHandle, sourceEntryName, destinationDirectoryHandle, destinationEntryName),
                cancellationToken);
        }

        public OpenNfsV3RenameResult ReadRenameV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRenameResult(encodedReply);
        }

        public Task<OpenNfsV3CreatePathResult> CreateSymbolicLinkV3Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSymbolicLinkRequest(directoryHandle, entryName, targetPath),
                "NFSv3 SYMLINK",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateSymbolicLinkV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateSymbolicLinkV3Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateSymbolicLinkRequest(directoryHandle, entryName, targetPath), cancellationToken);
        }

        public OpenNfsV3CreatePathResult ReadCreateSymbolicLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreateSymbolicLinkResult(encodedReply);
        }

        public Task<OpenNfsV3LinkResult> CreateHardLinkV3Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateHardLinkRequest(sourceFileHandle, destinationDirectoryHandle, destinationEntryName),
                "NFSv3 LINK",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateHardLinkV3Result,
                cancellationToken);
        }

        public Task<OpenNfsV3ProcedurePlan> PrepareCreateHardLinkV3Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateHardLinkRequest(sourceFileHandle, destinationDirectoryHandle, destinationEntryName),
                cancellationToken);
        }

        public OpenNfsV3LinkResult ReadCreateHardLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadLinkResult(encodedReply);
        }
    }
}
