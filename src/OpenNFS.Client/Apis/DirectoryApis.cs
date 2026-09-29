namespace OpenNFS.Client.Apis
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Client.Raw;

    /// <summary>
    /// Provides grouped directory-oriented convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class DirectoryApis
    {
        private readonly OpenNfsClient _client;

        internal DirectoryApis(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTROOTFH</c> + <c>GETFH</c> + <c>GETATTR</c> flow and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Executes an NFSv3 <c>LOOKUP</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="entryName">Directory entry name to resolve.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 LOOKUP result.</returns>
        public Task<OpenNfsV3LookupResult> LookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateLookupRequest(directoryHandle, entryName),
                "NFSv3 LOOKUP",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLookupV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>LOOKUP</c> plan for a directory entry name.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="entryName">Directory entry name to resolve.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareLookupV3Async(byte[] directoryHandle, string entryName, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateLookupRequest(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>LOOKUP</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 LOOKUP result.</returns>
        public OpenNfsV3LookupResult ReadLookupV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadLookupResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>READDIR</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="cookie">Directory cookie to continue from.</param>
        /// <param name="cookieVerifier">Eight-byte cookie verifier from a prior reply, or eight zero bytes for an initial request.</param>
        /// <param name="count">Maximum reply byte count requested from the server.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 READDIR result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>READDIR</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="cookie">Directory cookie to continue from.</param>
        /// <param name="cookieVerifier">Eight-byte cookie verifier from a prior reply, or eight zero bytes for an initial request.</param>
        /// <param name="count">Maximum reply byte count requested from the server.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
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

        /// <summary>
        /// Decodes a typed NFSv3 <c>READDIR</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 READDIR result.</returns>
        public OpenNfsV3ReadDirectoryResult ReadReadDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadDirectoryResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>READDIRPLUS</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="cookie">Directory cookie to continue from.</param>
        /// <param name="cookieVerifier">Eight-byte cookie verifier from a prior reply, or eight zero bytes for an initial request.</param>
        /// <param name="directoryCount">Maximum directory-entry byte count requested from the server.</param>
        /// <param name="maxCount">Maximum total reply byte count requested from the server.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 READDIRPLUS result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>READDIRPLUS</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Directory filehandle bytes.</param>
        /// <param name="cookie">Directory cookie to continue from.</param>
        /// <param name="cookieVerifier">Eight-byte cookie verifier from a prior reply, or eight zero bytes for an initial request.</param>
        /// <param name="directoryCount">Maximum directory-entry byte count requested from the server.</param>
        /// <param name="maxCount">Maximum total reply byte count requested from the server.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
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

        /// <summary>
        /// Decodes a typed NFSv3 <c>READDIRPLUS</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 READDIRPLUS result.</returns>
        public OpenNfsV3ReadDirectoryPlusResult ReadReadDirectoryPlusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadDirectoryPlusResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>CREATE</c> request for a regular file and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Entry name to create.</param>
        /// <param name="failIfExists">When <c>true</c>, use guarded-create semantics; otherwise use unchecked-create semantics.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 CREATE result.</returns>
        public Task<OpenNfsV3CreatePathResult> CreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateFileRequest(directoryHandle, entryName, failIfExists),
                "NFSv3 CREATE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateFileV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>CREATE</c> plan for a regular file.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Entry name to create.</param>
        /// <param name="failIfExists">When <c>true</c>, use guarded-create semantics; otherwise use unchecked-create semantics.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareCreateFileV3Async(
            byte[] directoryHandle,
            string entryName,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateFileRequest(directoryHandle, entryName, failIfExists), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>CREATE</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 CREATE result.</returns>
        public OpenNfsV3CreatePathResult ReadCreateFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreatePathResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>MKDIR</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Directory name to create.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 MKDIR result.</returns>
        public Task<OpenNfsV3CreatePathResult> CreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateDirectoryRequest(directoryHandle, entryName),
                "NFSv3 MKDIR",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateDirectoryV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>MKDIR</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Directory name to create.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareCreateDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateDirectoryRequest(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>MKDIR</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 MKDIR result.</returns>
        public OpenNfsV3CreatePathResult ReadCreateDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreateDirectoryResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>REMOVE</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">File entry name to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 REMOVE result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>REMOVE</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">File entry name to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveFileV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateRemoveFileRequest(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>REMOVE</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 REMOVE result.</returns>
        public OpenNfsV3DirectoryMutationResult ReadRemoveFileV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRemoveFileResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>RMDIR</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Directory entry name to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 RMDIR result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>RMDIR</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Directory entry name to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareRemoveDirectoryV3Async(
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateRemoveDirectoryRequest(directoryHandle, entryName), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>RMDIR</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 RMDIR result.</returns>
        public OpenNfsV3DirectoryMutationResult ReadRemoveDirectoryV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRemoveDirectoryResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>RENAME</c> request and decodes the typed result.
        /// </summary>
        /// <param name="sourceDirectoryHandle">Source parent directory filehandle bytes.</param>
        /// <param name="sourceEntryName">Source entry name.</param>
        /// <param name="destinationDirectoryHandle">Destination parent directory filehandle bytes.</param>
        /// <param name="destinationEntryName">Destination entry name.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 RENAME result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>RENAME</c> plan.
        /// </summary>
        /// <param name="sourceDirectoryHandle">Source parent directory filehandle bytes.</param>
        /// <param name="sourceEntryName">Source entry name.</param>
        /// <param name="destinationDirectoryHandle">Destination parent directory filehandle bytes.</param>
        /// <param name="destinationEntryName">Destination entry name.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
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

        /// <summary>
        /// Decodes a typed NFSv3 <c>RENAME</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 RENAME result.</returns>
        public OpenNfsV3RenameResult ReadRenameV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadRenameResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>SYMLINK</c> request and decodes the typed result.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Symbolic-link entry name to create.</param>
        /// <param name="targetPath">Symbolic-link target path.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 SYMLINK result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>SYMLINK</c> plan.
        /// </summary>
        /// <param name="directoryHandle">Parent directory filehandle bytes.</param>
        /// <param name="entryName">Symbolic-link entry name to create.</param>
        /// <param name="targetPath">Symbolic-link target path.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareCreateSymbolicLinkV3Async(
            byte[] directoryHandle,
            string entryName,
            string targetPath,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateSymbolicLinkRequest(directoryHandle, entryName, targetPath), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>SYMLINK</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 SYMLINK result.</returns>
        public OpenNfsV3CreatePathResult ReadCreateSymbolicLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCreateSymbolicLinkResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>LINK</c> request and decodes the typed result.
        /// </summary>
        /// <param name="sourceFileHandle">Source filehandle bytes.</param>
        /// <param name="destinationDirectoryHandle">Destination parent directory filehandle bytes.</param>
        /// <param name="destinationEntryName">Destination entry name.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 LINK result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>LINK</c> plan.
        /// </summary>
        /// <param name="sourceFileHandle">Source filehandle bytes.</param>
        /// <param name="destinationDirectoryHandle">Destination parent directory filehandle bytes.</param>
        /// <param name="destinationEntryName">Destination entry name.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
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

        /// <summary>
        /// Decodes a typed NFSv3 <c>LINK</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 LINK result.</returns>
        public OpenNfsV3LinkResult ReadCreateHardLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadLinkResult(encodedReply);
        }

        private static OpenNfsCompoundRequest CreateGetRootV40Request()
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "putrootfh",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateLookupV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lookup",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOOKUP,
                        EncodeV40Payload(
                            new LOOKUP4args
                            {
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateLookupParentV40Request(byte[] childHandle)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lookupp",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(childHandle, nameof(childHandle)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_LOOKUPP, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateSecurityInfoV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "secinfo",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SECINFO,
                        EncodeV40Payload(
                            new SECINFO4args
                            {
                                name = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateCreateDirectoryV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "create-dir",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CREATE,
                        EncodeV40Payload(
                            new CREATE4args
                            {
                                objtype = new createtype4
                                {
                                    type = nfs_ftype4.NF4DIR,
                                },
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                                createattrs = CreateEmptyV40Attributes(),
                            }.WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateCreateSymbolicLinkV40Request(
            byte[] directoryHandle,
            string entryName,
            string targetPath)
        {
            string safeTargetPath = OpenNfsClientArgument.RequireText(targetPath, nameof(targetPath));
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "create-symlink",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CREATE,
                        EncodeV40Payload(
                            new CREATE4args
                            {
                                objtype = new createtype4
                                {
                                    type = nfs_ftype4.NF4LNK,
                                    linkdata = new linktext4
                                    {
                                        Value = System.Text.Encoding.UTF8.GetBytes(safeTargetPath),
                                    },
                                },
                                objname = CreatePathComponent(entryName, nameof(entryName)),
                                createattrs = CreateEmptyV40Attributes(),
                            }.WriteTo)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateRemoveEntryV40Request(byte[] directoryHandle, string entryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "remove",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_REMOVE,
                        EncodeV40Payload(
                            new REMOVE4args
                            {
                                target = CreatePathComponent(entryName, nameof(entryName)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateRenameV40Request(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] targetDirectoryHandle,
            string targetEntryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "rename",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(sourceDirectoryHandle, nameof(sourceDirectoryHandle)),
                    CreateSaveFileHandleOperation(),
                    CreatePutFileHandleOperation(targetDirectoryHandle, nameof(targetDirectoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_RENAME,
                        EncodeV40Payload(
                            new RENAME4args
                            {
                                oldname = CreatePathComponent(sourceEntryName, nameof(sourceEntryName)),
                                newname = CreatePathComponent(targetEntryName, nameof(targetEntryName)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateHardLinkV40Request(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "link",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(sourceFileHandle, nameof(sourceFileHandle)),
                    CreateSaveFileHandleOperation(),
                    CreatePutFileHandleOperation(destinationDirectoryHandle, nameof(destinationDirectoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LINK,
                        EncodeV40Payload(
                            new LINK4args
                            {
                                newname = CreatePathComponent(destinationEntryName, nameof(destinationEntryName)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateReadDirectoryV40Request(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint maxCount)
        {
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The requested NFSv4 READDIR reply byte count must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "readdir",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_READDIR,
                        EncodeV40Payload(
                            new READDIR4args
                            {
                                cookie = new nfs_cookie4
                                {
                                    Value = cookie,
                                },
                                cookieverf = new verifier4
                                {
                                    Value = safeCookieVerifier,
                                },
                                dircount = new count4
                                {
                                    Value = maxCount,
                                },
                                maxcount = new count4
                                {
                                    Value = maxCount,
                                },
                                attr_request = CreateDefaultV40AttributeRequest(),
                            }.WriteTo)),
                });
        }

        private static bitmap4 CreateDefaultV40AttributeRequest()
        {
            return new bitmap4
            {
                Value = new[]
                {
                    (1U << (int)Nfs40Constants.FATTR4_TYPE)
                    | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                    | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                    | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                },
            };
        }

        private static fattr4 CreateEmptyV40Attributes()
        {
            return new fattr4
            {
                attrmask = new bitmap4
                {
                    Value = Array.Empty<uint>(),
                },
                attr_vals = new attrlist4
                {
                    Value = Array.Empty<byte>(),
                },
            };
        }

        private static OpenNfsCompoundOperation CreatePutFileHandleOperation(byte[] fileHandle, string parameterName)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false);
            return new OpenNfsCompoundOperation(
                (uint)nfs_opnum4.OP_PUTFH,
                EncodeV40Payload(
                    new PUTFH4args
                    {
                        @object = new nfs_fh4
                        {
                            Value = safeFileHandle,
                        },
                    }.WriteTo));
        }

        private static OpenNfsCompoundOperation CreateSaveFileHandleOperation()
        {
            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_SAVEFH, Array.Empty<byte>());
        }

        private static component4 CreatePathComponent(string entryName, string parameterName)
        {
            return new component4
            {
                Value = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = System.Text.Encoding.UTF8.GetBytes(OpenNfsClientArgument.RequireEntryName(entryName, parameterName)),
                    },
                },
            };
        }

        private static byte[] EncodeV40Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        private static OpenNfsV3ProcedureRequest CreateLookupRequest(byte[] directoryHandle, string entryName)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            string safeEntryName = OpenNfsClientArgument.RequireEntryName(entryName, nameof(entryName));
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteString(safeEntryName);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsLookupProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateReadDirectoryRequest(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint count)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested directory byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteUInt64(cookie);
            writer.WriteFixedOpaque(safeCookieVerifier);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReaddirProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateReadDirectoryPlusRequest(
            byte[] directoryHandle,
            ulong cookie,
            byte[] cookieVerifier,
            uint directoryCount,
            uint maxCount)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            byte[] safeCookieVerifier = OpenNfsClientArgument.RequireFixedBytes(cookieVerifier, expectedLength: 8, nameof(cookieVerifier));

            if (directoryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(directoryCount), directoryCount, "The requested directory-entry byte count must be greater than zero.");
            }

            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "The requested total reply byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeDirectoryHandle);
            writer.WriteUInt64(cookie);
            writer.WriteFixedOpaque(safeCookieVerifier);
            writer.WriteUInt32(directoryCount);
            writer.WriteUInt32(maxCount);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReaddirPlusProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateFileRequest(byte[] directoryHandle, string entryName, bool failIfExists)
        {
            CREATE3args arguments = new CREATE3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                how = new createhow3
                {
                    mode = failIfExists ? createmode3.GUARDED : createmode3.UNCHECKED,
                    obj_attributes = CreateUnsetAttributes(),
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsCreateProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateDirectoryRequest(byte[] directoryHandle, string entryName)
        {
            MKDIR3args arguments = new MKDIR3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                attributes = CreateUnsetAttributes(),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsMkdirProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateRemoveFileRequest(byte[] directoryHandle, string entryName)
        {
            REMOVE3args arguments = new REMOVE3args
            {
                @object = CreateDirectoryOperationArguments(directoryHandle, entryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRemoveProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateRemoveDirectoryRequest(byte[] directoryHandle, string entryName)
        {
            RMDIR3args arguments = new RMDIR3args
            {
                @object = CreateDirectoryOperationArguments(directoryHandle, entryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRmdirProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateRenameRequest(
            byte[] sourceDirectoryHandle,
            string sourceEntryName,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            RENAME3args arguments = new RENAME3args
            {
                from = CreateDirectoryOperationArguments(sourceDirectoryHandle, sourceEntryName),
                to = CreateDirectoryOperationArguments(destinationDirectoryHandle, destinationEntryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsRenameProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateSymbolicLinkRequest(
            byte[] directoryHandle,
            string entryName,
            string targetPath)
        {
            string safeTargetPath = OpenNfsClientArgument.RequireText(targetPath, nameof(targetPath));
            SYMLINK3args arguments = new SYMLINK3args
            {
                where = CreateDirectoryOperationArguments(directoryHandle, entryName),
                symlink = new symlinkdata3
                {
                    symlink_attributes = CreateUnsetAttributes(),
                    symlink_data = new nfspath3
                    {
                        Value = safeTargetPath,
                    },
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsSymlinkProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateHardLinkRequest(
            byte[] sourceFileHandle,
            byte[] destinationDirectoryHandle,
            string destinationEntryName)
        {
            LINK3args arguments = new LINK3args
            {
                file = CreateFileHandle(sourceFileHandle, nameof(sourceFileHandle)),
                link = CreateDirectoryOperationArguments(destinationDirectoryHandle, destinationEntryName),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NfsLinkProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateEncodedRequest(uint procedureNumber, Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray());
        }

        private static diropargs3 CreateDirectoryOperationArguments(byte[] directoryHandle, string entryName)
        {
            byte[] safeDirectoryHandle = OpenNfsClientArgument.RequireBytes(directoryHandle, nameof(directoryHandle), allowEmpty: false);
            string safeEntryName = OpenNfsClientArgument.RequireEntryName(entryName, nameof(entryName));
            return new diropargs3
            {
                dir = new nfs_fh3
                {
                    data = safeDirectoryHandle,
                },
                name = new filename3
                {
                    Value = safeEntryName,
                },
            };
        }

        private static nfs_fh3 CreateFileHandle(byte[] fileHandle, string parameterName)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false);
            return new nfs_fh3
            {
                data = safeFileHandle,
            };
        }

        private static sattr3 CreateUnsetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }
    }
}
