namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Client.Raw;
    using static OpenNFS.Client.Internal.OpenNfsDirectoryApiV3Requests;
    using static OpenNFS.Client.Internal.OpenNfsDirectoryApiV40Requests;

    /// <summary>
    /// Encapsulates grouped NFSv4.0 directory, namespace, and mutation flows.
    /// </summary>
    internal sealed class OpenNfsDirectoryV40Apis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsDirectoryV40Apis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsV40LookupResult> GetRootV40Async(CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateGetRootV40Request(),
                "NFSv4.0 PUTROOTFH",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadGetRootV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>PUTROOTFH</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareGetRootV40Async(CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateGetRootV40Request(), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>PUTROOTFH</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40LookupResult ReadGetRootV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadGetRootResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOOKUP</c> + <c>GETFH</c> + <c>GETATTR</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40LookupResult> LookupV40Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateLookupV40Request(directoryHandle, entryName),
                "NFSv4.0 LOOKUP",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLookupV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>LOOKUP</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareLookupV40Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateLookupV40Request(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LOOKUP</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40LookupResult ReadLookupV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLookupResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>SECINFO</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40SecurityInfoResult> GetSecurityInfoV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateSecurityInfoV40Request(directoryHandle, entryName),
                "NFSv4.0 SECINFO",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadSecurityInfoV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>SECINFO</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareGetSecurityInfoV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateSecurityInfoV40Request(directoryHandle, entryName),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>SECINFO</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40SecurityInfoResult ReadSecurityInfoV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadSecurityInfoResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>READDIR</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40ReadDirectoryResult> ReadDirectoryV40Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateReadDirectoryV40Request(directoryHandle, cookie, cookieVerifier, maxCount),
                "NFSv4.0 READDIR",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadDirectoryV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>READDIR</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareReadDirectoryV40Async(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint maxCount,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateReadDirectoryV40Request(directoryHandle, cookie, cookieVerifier, maxCount),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>READDIR</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40ReadDirectoryResult ReadReadDirectoryV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadReadDirectoryResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOOKUPP</c> + <c>GETFH</c> + <c>GETATTR</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40LookupResult> LookupParentV40Async(byte[] childHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateLookupParentV40Request(childHandle),
                "NFSv4.0 LOOKUPP",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLookupParentV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>LOOKUPP</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareLookupParentV40Async(byte[] childHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateLookupParentV40Request(childHandle), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LOOKUPP</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40LookupResult ReadLookupParentV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLookupParentResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>CREATE</c> + <c>GETFH</c> + <c>GETATTR</c> flow for a directory and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40CreateResult> CreateDirectoryV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCreateDirectoryV40Request(directoryHandle, entryName),
                "NFSv4.0 CREATE directory",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateDirectoryV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped directory-create COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCreateDirectoryV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCreateDirectoryV40Request(directoryHandle, entryName),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 directory-create result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40CreateResult ReadCreateDirectoryV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadCreateResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>CREATE</c> + <c>GETFH</c> + <c>GETATTR</c> flow for a symbolic link and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40CreateResult> CreateSymbolicLinkV40Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCreateSymbolicLinkV40Request(directoryHandle, entryName, targetPath),
                "NFSv4.0 CREATE symbolic link",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateSymbolicLinkV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped symbolic-link-create COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCreateSymbolicLinkV40Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCreateSymbolicLinkV40Request(directoryHandle, entryName, targetPath),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 symbolic-link-create result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40CreateResult ReadCreateSymbolicLinkV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadCreateResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>REMOVE</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40DirectoryMutationResult> RemoveEntryV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateRemoveEntryV40Request(directoryHandle, entryName),
                "NFSv4.0 REMOVE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadRemoveEntryV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>REMOVE</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareRemoveEntryV40Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateRemoveEntryV40Request(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>REMOVE</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40DirectoryMutationResult ReadRemoveEntryV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadRemoveResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>SAVEFH</c> + <c>PUTFH</c> + <c>RENAME</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40RenameResult> RenameV40Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] targetDirectoryHandle,
            string targetEntryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateRenameV40Request(sourceDirectoryHandle, sourceEntryName, targetDirectoryHandle, targetEntryName),
                "NFSv4.0 RENAME",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadRenameV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>RENAME</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareRenameV40Async(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] targetDirectoryHandle,
            string targetEntryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateRenameV40Request(sourceDirectoryHandle, sourceEntryName, targetDirectoryHandle, targetEntryName),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>RENAME</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40RenameResult ReadRenameV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadRenameResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>SAVEFH</c> + <c>PUTFH</c> + <c>LINK</c> flow and decodes the typed result.
        /// </summary>

        public Task<OpenNfsV40LinkResult> CreateHardLinkV40Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateHardLinkV40Request(sourceFileHandle, destinationDirectoryHandle, destinationEntryName),
                "NFSv4.0 LINK",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateHardLinkV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>LINK</c> COMPOUND plan.
        /// </summary>

        public Task<OpenNfsCompoundPlan> PrepareCreateHardLinkV40Async(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateHardLinkV40Request(sourceFileHandle, destinationDirectoryHandle, destinationEntryName),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LINK</c> result from a full encoded RPC reply.
        /// </summary>

        public OpenNfsV40LinkResult ReadCreateHardLinkV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLinkResult(encodedReply);
        }
    }
}
