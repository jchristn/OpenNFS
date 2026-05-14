namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Client.Raw;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Provides grouped file-oriented convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class FileApis
    {
        private readonly OpenNfsClient _client;
        private readonly OpenNfsFileV42Apis _v42Apis;

        internal FileApis(OpenNfsClient client)
        {
            _client = client;
            _v42Apis = new OpenNfsFileV42Apis(client);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>IO_ADVISE</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42IoAdviseResult> IoAdviseV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong count,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? hints,
            CancellationToken cancellationToken)
        {
            return _v42Apis.IoAdviseV42Async(fileHandle, offset, count, hints, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>IO_ADVISE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareIoAdviseV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong count,
            IReadOnlyList<OpenNfsV42IoAdviceHint>? hints,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareIoAdviseV42Async(fileHandle, offset, count, hints, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>READ_PLUS</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42ReadPlusResult> ReadPlusV42Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _v42Apis.ReadPlusV42Async(fileHandle, offset, count, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>READ_PLUS</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareReadPlusV42Async(
            byte[] fileHandle,
            ulong offset,
            uint count,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareReadPlusV42Async(fileHandle, offset, count, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>SEEK</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42SeekResult> SeekV42Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsV42SeekTarget target,
            CancellationToken cancellationToken)
        {
            return _v42Apis.SeekV42Async(fileHandle, offset, target, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>SEEK</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareSeekV42Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsV42SeekTarget target,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareSeekV42Async(fileHandle, offset, target, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>ALLOCATE</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42AllocateResult> AllocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _v42Apis.AllocateV42Async(fileHandle, offset, length, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>ALLOCATE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareAllocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareAllocateV42Async(fileHandle, offset, length, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>COPY</c> grouped-session flow and decodes the typed result.
        /// </summary>
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
            return _v42Apis.CopyV42Async(
                sourceFileHandle,
                destinationFileHandle,
                sourceOffset,
                destinationOffset,
                count,
                consecutive,
                synchronous,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>COPY</c> COMPOUND plan.
        /// </summary>
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
            return _v42Apis.PrepareCopyV42Async(
                sourceFileHandle,
                destinationFileHandle,
                sourceOffset,
                destinationOffset,
                count,
                consecutive,
                synchronous,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>CLONE</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42CloneResult> CloneV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            CancellationToken cancellationToken)
        {
            return _v42Apis.CloneV42Async(
                sourceFileHandle,
                destinationFileHandle,
                sourceOffset,
                destinationOffset,
                count,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>CLONE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareCloneV42Async(
            byte[] sourceFileHandle,
            byte[] destinationFileHandle,
            ulong sourceOffset,
            ulong destinationOffset,
            ulong count,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareCloneV42Async(
                sourceFileHandle,
                destinationFileHandle,
                sourceOffset,
                destinationOffset,
                count,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.2 <c>DEALLOCATE</c> grouped-session flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV42DeallocateResult> DeallocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _v42Apis.DeallocateV42Async(fileHandle, offset, length, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.2 grouped <c>DEALLOCATE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareDeallocateV42Async(
            byte[] fileHandle,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _v42Apis.PrepareDeallocateV42Async(fileHandle, offset, length, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>GETATTR</c> flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40GetAttributesResult> GetAttributesV40Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return GetAttributesV40Async(fileHandle, requestedAttributes: null, cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>GETATTR</c> flow for a caller-selected attribute set and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40GetAttributesResult> GetAttributesV40Async(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AttributeKind>? requestedAttributes,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateGetAttributesV40Request(fileHandle, requestedAttributes),
                "NFSv4.0 GETATTR",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadGetAttributesV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>GETATTR</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareGetAttributesV40Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return PrepareGetAttributesV40Async(fileHandle, requestedAttributes: null, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>GETATTR</c> COMPOUND plan for a caller-selected attribute set.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareGetAttributesV40Async(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AttributeKind>? requestedAttributes,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateGetAttributesV40Request(fileHandle, requestedAttributes),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>GETATTR</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40GetAttributesResult ReadGetAttributesV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadGetAttributesResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>GETATTR</c> ACL flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40GetAclResult> GetAclV40Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateGetAclV40Request(fileHandle),
                "NFSv4.0 GETATTR ACL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadGetAclV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped ACL-read COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareGetAclV40Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateGetAclV40Request(fileHandle), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 ACL-read result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40GetAclResult ReadGetAclV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadGetAclResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>SETATTR</c> ACL flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40SetAclResult> SetAclV40Async(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AclEntry> entries,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateSetAclV40Request(fileHandle, entries),
                "NFSv4.0 SETATTR ACL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadSetAclV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped ACL-write COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareSetAclV40Async(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AclEntry> entries,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateSetAclV40Request(fileHandle, entries), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 ACL-write result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40SetAclResult ReadSetAclV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadSetAclResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>SETATTR</c> owner and owner-group flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40SetIdentityResult> SetOwnerAndGroupV40Async(
            byte[] fileHandle,
            string? owner,
            string? ownerGroup,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateSetOwnerAndGroupV40Request(fileHandle, owner, ownerGroup),
                "NFSv4.0 SETATTR owner/owner_group",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadSetOwnerAndGroupV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped owner and owner-group <c>SETATTR</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareSetOwnerAndGroupV40Async(
            byte[] fileHandle,
            string? owner,
            string? ownerGroup,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateSetOwnerAndGroupV40Request(fileHandle, owner, ownerGroup), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 owner and owner-group <c>SETATTR</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40SetIdentityResult ReadSetOwnerAndGroupV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadSetIdentityResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>ACCESS</c> flow and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40AccessResult> AccessV40Async(
            byte[] fileHandle,
            OpenNfsV40AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateAccessV40Request(fileHandle, requestedAccess),
                "NFSv4.0 ACCESS",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadAccessV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>ACCESS</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareAccessV40Async(
            byte[] fileHandle,
            OpenNfsV40AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateAccessV40Request(fileHandle, requestedAccess), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>ACCESS</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40AccessResult ReadAccessV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadAccessResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>READ</c> flow and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>OPEN</c> + <c>GETFH</c> + <c>GETATTR</c> flow for an existing file and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40OpenResult> OpenExistingV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenExistingV40Request(directoryHandle, clientId, openOwner, entryName, shareAccess, shareDeny, sequenceId),
                "NFSv4.0 OPEN existing",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadOpenExistingV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped existing-file <c>OPEN</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareOpenExistingV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenExistingV40Request(directoryHandle, clientId, openOwner, entryName, shareAccess, shareDeny, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 existing-file <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40OpenResult ReadOpenExistingV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>OPEN</c> + <c>GETFH</c> + <c>GETATTR</c> reclaim flow for an existing file and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40OpenResult> ReclaimOpenV40Async(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateReclaimOpenV40Request(fileHandle, clientId, openOwner, shareAccess, shareDeny, sequenceId),
                "NFSv4.0 OPEN reclaim",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadReclaimOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped reclaim <c>OPEN</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareReclaimOpenV40Async(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateReclaimOpenV40Request(fileHandle, clientId, openOwner, shareAccess, shareDeny, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 reclaim <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40OpenResult ReadReclaimOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>OPEN</c> + <c>GETFH</c> + <c>GETATTR</c> flow for create-through-open and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40OpenResult> CreateAndOpenV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCreateAndOpenV40Request(
                    directoryHandle,
                    clientId,
                    openOwner,
                    entryName,
                    shareAccess,
                    shareDeny,
                    sequenceId,
                    failIfExists),
                "NFSv4.0 OPEN create",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCreateAndOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped create-through-open <c>OPEN</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareCreateAndOpenV40Async(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCreateAndOpenV40Request(
                    directoryHandle,
                    clientId,
                    openOwner,
                    entryName,
                    shareAccess,
                    shareDeny,
                    sequenceId,
                    failIfExists),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 create-through-open <c>OPEN</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40OpenResult ReadCreateAndOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>OPEN_CONFIRM</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40StateIdResult> ConfirmOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenConfirmV40Request(openStateId, sequenceId),
                "NFSv4.0 OPEN_CONFIRM",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadConfirmOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>PUTFH</c> + <c>OPEN_CONFIRM</c> request and decodes the typed result.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>OPEN_CONFIRM</c>.
        /// </summary>
        public Task<OpenNfsV40StateIdResult> ConfirmOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenConfirmV40Request(fileHandle, openStateId, sequenceId),
                "NFSv4.0 OPEN_CONFIRM",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadConfirmOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>OPEN_CONFIRM</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareConfirmOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateOpenConfirmV40Request(openStateId, sequenceId), cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>PUTFH</c> + <c>OPEN_CONFIRM</c> COMPOUND plan.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>OPEN_CONFIRM</c>.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareConfirmOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenConfirmV40Request(fileHandle, openStateId, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>OPEN_CONFIRM</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40StateIdResult ReadConfirmOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenConfirmResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>OPEN_DOWNGRADE</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40StateIdResult> DowngradeOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateOpenDowngradeV40Request(openStateId, sequenceId, shareAccess, shareDeny),
                "NFSv4.0 OPEN_DOWNGRADE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadDowngradeOpenV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>OPEN_DOWNGRADE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareDowngradeOpenV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateOpenDowngradeV40Request(openStateId, sequenceId, shareAccess, shareDeny),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>OPEN_DOWNGRADE</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40StateIdResult ReadDowngradeOpenV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadOpenDowngradeResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>CLOSE</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40StateIdResult> CloseV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCloseV40Request(openStateId, sequenceId),
                "NFSv4.0 CLOSE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCloseV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>PUTFH</c> + <c>CLOSE</c> request and decodes the typed result.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>CLOSE</c>.
        /// </summary>
        public Task<OpenNfsV40StateIdResult> CloseV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateCloseV40Request(fileHandle, openStateId, sequenceId),
                "NFSv4.0 CLOSE",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadCloseV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>CLOSE</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareCloseV40Async(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateCloseV40Request(openStateId, sequenceId), cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>PUTFH</c> + <c>CLOSE</c> COMPOUND plan.
        /// Use this overload when interoperating with peers that require the current filehandle to be set explicitly before <c>CLOSE</c>.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareCloseV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateCloseV40Request(fileHandle, openStateId, sequenceId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>CLOSE</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40StateIdResult ReadCloseV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadCloseResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>DELEGRETURN</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40DelegationReturnResult> ReturnDelegationV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateDelegationReturnV40Request(fileHandle, delegationStateId),
                "NFSv4.0 DELEGRETURN",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadReturnDelegationV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>DELEGRETURN</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareReturnDelegationV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateDelegationReturnV40Request(fileHandle, delegationStateId),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>DELEGRETURN</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40DelegationReturnResult ReadReturnDelegationV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadDelegationReturnResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>GETATTR</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 GETATTR result.</returns>
        public Task<OpenNfsV3GetAttributesResult> GetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsGetattrProcedure),
                "NFSv3 GETATTR",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadGetAttributesV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>GETATTR</c> plan for a filehandle.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareGetAttributesV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsGetattrProcedure),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>GETATTR</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 GETATTR result.</returns>
        public OpenNfsV3GetAttributesResult ReadGetAttributesV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadGetAttributesResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>ACCESS</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="requestedAccess">Requested NFSv3 ACCESS bit mask.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 ACCESS result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>ACCESS</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="requestedAccess">Requested NFSv3 ACCESS bit mask.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareAccessV3Async(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateAccessRequest(fileHandle, requestedAccess), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>ACCESS</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 ACCESS result.</returns>
        public OpenNfsV3AccessResult ReadAccessV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadAccessResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>READ</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="count">Requested read byte count.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 READ result.</returns>
        public Task<OpenNfsV3ReadResult> ReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateReadRequest(fileHandle, offset, count),
                "NFSv3 READ",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>READ</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="count">Requested read byte count.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareReadV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateReadRequest(fileHandle, offset, count), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>READ</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 READ result.</returns>
        public OpenNfsV3ReadResult ReadReadV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>READLINK</c> request and decodes the typed result.
        /// </summary>
        /// <param name="symbolicLinkHandle">Symbolic-link filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 READLINK result.</returns>
        public Task<OpenNfsV3ReadLinkResult> ReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(symbolicLinkHandle, OpenNfsV3RpcConstants.NfsReadLinkProcedure),
                "NFSv3 READLINK",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadReadLinkV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>READLINK</c> plan.
        /// </summary>
        /// <param name="symbolicLinkHandle">Symbolic-link filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareReadLinkV3Async(byte[] symbolicLinkHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(symbolicLinkHandle, OpenNfsV3RpcConstants.NfsReadLinkProcedure),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>READLINK</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 READLINK result.</returns>
        public OpenNfsV3ReadLinkResult ReadReadLinkV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadReadLinkResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>WRITE</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="stability">Requested write stability mode.</param>
        /// <param name="data">Payload bytes to write.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 WRITE result.</returns>
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

        /// <summary>
        /// Prepares an NFSv3 <c>WRITE</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="stability">Requested write stability mode.</param>
        /// <param name="data">Payload bytes to write.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareWriteV3Async(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateWriteRequest(fileHandle, offset, stability, data), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>WRITE</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 WRITE result.</returns>
        public OpenNfsV3WriteResult ReadWriteV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadWriteResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>COMMIT</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="count">Requested commit byte count.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 COMMIT result.</returns>
        public Task<OpenNfsV3CommitResult> CommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateCommitRequest(fileHandle, offset, count),
                "NFSv3 COMMIT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadCommitV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>COMMIT</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="offset">Starting file offset.</param>
        /// <param name="count">Requested commit byte count.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareCommitV3Async(byte[] fileHandle, ulong offset, uint count, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(CreateCommitRequest(fileHandle, offset, count), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>COMMIT</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 COMMIT result.</returns>
        public OpenNfsV3CommitResult ReadCommitV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadCommitResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>FSSTAT</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 FSSTAT result.</returns>
        public Task<OpenNfsV3FileSystemStatusResult> GetFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsStatProcedure),
                "NFSv3 FSSTAT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadFileSystemStatusV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>FSSTAT</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemStatusV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsStatProcedure),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>FSSTAT</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 FSSTAT result.</returns>
        public OpenNfsV3FileSystemStatusResult ReadFileSystemStatusV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadFileSystemStatusResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>FSINFO</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 FSINFO result.</returns>
        public Task<OpenNfsV3FileSystemInfoResult> GetFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsInfoProcedure),
                "NFSv3 FSINFO",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadFileSystemInfoV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>FSINFO</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareFileSystemInfoV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsFsInfoProcedure),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>FSINFO</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 FSINFO result.</returns>
        public OpenNfsV3FileSystemInfoResult ReadFileSystemInfoV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadFileSystemInfoResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv3 <c>PATHCONF</c> request and decodes the typed result.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the execution operation.</param>
        /// <returns>The typed NFSv3 PATHCONF result.</returns>
        public Task<OpenNfsV3PathConfigurationResult> GetPathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsPathConfProcedure),
                "NFSv3 PATHCONF",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadPathConfigurationV3Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv3 <c>PATHCONF</c> plan.
        /// </summary>
        /// <param name="fileHandle">Filehandle bytes.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PreparePathConfigurationV3Async(byte[] fileHandle, CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateSingleHandleRequest(fileHandle, OpenNfsV3RpcConstants.NfsPathConfProcedure),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv3 <c>PATHCONF</c> result from a full encoded RPC reply.
        /// </summary>
        /// <param name="encodedReply">Full encoded RPC reply bytes.</param>
        /// <returns>The typed NFSv3 PATHCONF result.</returns>
        public OpenNfsV3PathConfigurationResult ReadPathConfigurationV3Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNfsV3ReplyDecoder.ReadPathConfigurationResult(encodedReply);
        }

        private static OpenNfsCompoundRequest CreateOpenExistingV40Request(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateOpenArguments(
                                clientId,
                                openOwner,
                                entryName,
                                shareAccess,
                                shareDeny,
                                sequenceId,
                                opentype4.OPEN4_NOCREATE,
                                failIfExists: false).WriteTo)),
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

        private static OpenNfsCompoundRequest CreateCreateAndOpenV40Request(
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            bool failIfExists)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-create",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(directoryHandle, nameof(directoryHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateOpenArguments(
                                clientId,
                                openOwner,
                                entryName,
                                shareAccess,
                                shareDeny,
                                sequenceId,
                                opentype4.OPEN4_CREATE,
                                failIfExists).WriteTo)),
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

        private static OpenNfsCompoundRequest CreateReclaimOpenV40Request(
            byte[] fileHandle,
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-reclaim",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN,
                        EncodeV40Payload(
                            CreateReclaimOpenArguments(
                                clientId,
                                openOwner,
                                shareAccess,
                                shareDeny,
                                sequenceId).WriteTo)),
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

        private static OpenNfsCompoundRequest CreateOpenConfirmV40Request(OpenNfsV40StateId openStateId, uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-confirm",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_CONFIRM,
                        EncodeV40Payload(
                            new OPEN_CONFIRM4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateOpenConfirmV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-confirm",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_CONFIRM,
                        EncodeV40Payload(
                            new OPEN_CONFIRM4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateOpenDowngradeV40Request(
            OpenNfsV40StateId openStateId,
            uint sequenceId,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "open-downgrade",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_OPEN_DOWNGRADE,
                        EncodeV40Payload(
                            new OPEN_DOWNGRADE4args
                            {
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                share_access = (uint)shareAccess,
                                share_deny = (uint)shareDeny,
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateCloseV40Request(OpenNfsV40StateId openStateId, uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "close",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CLOSE,
                        EncodeV40Payload(
                            new CLOSE4args
                            {
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateCloseV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint sequenceId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "close",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_CLOSE,
                        EncodeV40Payload(
                            new CLOSE4args
                            {
                                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                                open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateDelegationReturnV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId delegationStateId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "delegreturn",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_DELEGRETURN,
                        EncodeV40Payload(
                            new DELEGRETURN4args
                            {
                                deleg_stateid = CreateStateId(delegationStateId, nameof(delegationStateId)),
                            }.WriteTo)),
                });
        }

        private static OPEN4args CreateOpenArguments(
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            opentype4 openType,
            bool failIfExists)
        {
            if (shareAccess == OpenNfsV40ShareAccess.None)
            {
                throw new ArgumentOutOfRangeException(nameof(shareAccess), shareAccess, "The requested NFSv4 OPEN share-access mask must not be empty.");
            }

            string safeOwner = OpenNfsClientArgument.RequireText(openOwner, nameof(openOwner));
            string safeEntryName = OpenNfsClientArgument.RequireText(entryName, nameof(entryName));
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                share_access = (uint)shareAccess,
                share_deny = (uint)shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = System.Text.Encoding.UTF8.GetBytes(safeOwner),
                },
                openhow = new openflag4
                {
                    opentype = openType,
                    how = openType == opentype4.OPEN4_CREATE
                        ? new createhow4
                        {
                            mode = failIfExists ? createmode4.GUARDED4 : createmode4.UNCHECKED4,
                            createattrs = CreateEmptyV40Attributes(),
                        }
                        : null,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_NULL,
                    file = new component4
                    {
                        Value = new utf8str_cs
                        {
                            Value = new utf8string
                            {
                                Value = System.Text.Encoding.UTF8.GetBytes(safeEntryName),
                            },
                        },
                    },
                },
            };
        }

        private static OPEN4args CreateReclaimOpenArguments(
            ulong clientId,
            string openOwner,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId)
        {
            if (shareAccess == OpenNfsV40ShareAccess.None)
            {
                throw new ArgumentOutOfRangeException(nameof(shareAccess), shareAccess, "The requested NFSv4 reclaim OPEN share-access mask must not be empty.");
            }

            string safeOwner = OpenNfsClientArgument.RequireText(openOwner, nameof(openOwner));
            return new OPEN4args
            {
                seqid = CreateSequenceId(sequenceId, nameof(sequenceId)),
                share_access = (uint)shareAccess,
                share_deny = (uint)shareDeny,
                owner = new open_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = System.Text.Encoding.UTF8.GetBytes(safeOwner),
                },
                openhow = new openflag4
                {
                    opentype = opentype4.OPEN4_NOCREATE,
                },
                claim = new open_claim4
                {
                    claim = open_claim_type4.CLAIM_PREVIOUS,
                    delegate_type = open_delegation_type4.OPEN_DELEGATE_NONE,
                },
            };
        }

        private static seqid4 CreateSequenceId(uint sequenceId, string parameterName)
        {
            if (sequenceId == 0U)
            {
                throw new ArgumentOutOfRangeException(parameterName, sequenceId, "The requested NFSv4 sequence id must be greater than zero.");
            }

            return new seqid4
            {
                Value = sequenceId,
            };
        }

        private static stateid4 CreateStateId(OpenNfsV40StateId stateId, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(stateId);
            return new stateid4
            {
                seqid = stateId.SequenceId,
                other = OpenNfsClientArgument.RequireFixedBytes(stateId.Other.ToArray(), expectedLength: 12, parameterName),
            };
        }

        private static stable_how4 CreateWriteStability(OpenNfsWriteStability stability)
        {
            return stability switch
            {
                OpenNfsWriteStability.Unstable => stable_how4.UNSTABLE4,
                OpenNfsWriteStability.DataSync => stable_how4.DATA_SYNC4,
                OpenNfsWriteStability.FileSync => stable_how4.FILE_SYNC4,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(stability),
                    stability,
                    "The requested NFSv4 WRITE stability value is not supported."),
            };
        }

        private static OpenNfsCompoundRequest CreateAccessV40Request(
            byte[] fileHandle,
            OpenNfsV40AccessMask requestedAccess)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "access",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_ACCESS,
                        EncodeV40Payload(
                            new ACCESS4args
                            {
                                access = (uint)requestedAccess,
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateGetAttributesV40Request(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AttributeKind>? requestedAttributes)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "getattr",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_GETATTR,
                        EncodeV40Payload(
                            new GETATTR4args
                            {
                                attr_request = requestedAttributes is null
                                    ? CreateDefaultV40AttributeRequest()
                                    : CreateV40AttributeRequest(requestedAttributes),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateGetAclV40Request(byte[] fileHandle)
        {
            return CreateGetAttributesV40Request(
                fileHandle,
                new[]
                {
                    OpenNfsV40AttributeKind.AclSupport,
                    OpenNfsV40AttributeKind.Acl,
                });
        }

        private static OpenNfsCompoundRequest CreateReadLinkV40Request(byte[] symbolicLinkHandle)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "readlink",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(symbolicLinkHandle, nameof(symbolicLinkHandle)),
                    new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_READLINK, Array.Empty<byte>()),
                });
        }

        private static OpenNfsCompoundRequest CreateReadV40Request(byte[] fileHandle, ulong offset, uint count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested NFSv4 READ byte count must be greater than zero.");
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "read",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_READ,
                        EncodeV40Payload(
                            new READ4args
                            {
                                stateid = new stateid4
                                {
                                    seqid = 0,
                                    other = new byte[12],
                                },
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                count = new count4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateWriteV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId stateId,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data)
        {
            byte[] safeData = OpenNfsClientArgument.RequireBytes(data, nameof(data), allowEmpty: true);
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "write",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_WRITE,
                        EncodeV40Payload(
                            new WRITE4args
                            {
                                stateid = CreateStateId(stateId, nameof(stateId)),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                stable = CreateWriteStability(stability),
                                data = safeData,
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateCommitV40Request(byte[] fileHandle, ulong offset, uint count)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "commit",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_COMMIT,
                        EncodeV40Payload(
                            new COMMIT4args
                            {
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                count = new count4
                                {
                                    Value = count,
                                },
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateSetAclV40Request(
            byte[] fileHandle,
            IReadOnlyList<OpenNfsV40AclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "setacl",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETATTR,
                        EncodeV40Payload(
                            new SETATTR4args
                            {
                                stateid = new stateid4
                                {
                                    seqid = 0U,
                                    other = new byte[12],
                                },
                                obj_attributes = CreateV40AclAttributes(entries),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateSetOwnerAndGroupV40Request(
            byte[] fileHandle,
            string? owner,
            string? ownerGroup)
        {
            fattr4 identityAttributes = CreateV40IdentityAttributes(owner, ownerGroup);
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "set-identity",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETATTR,
                        EncodeV40Payload(
                            new SETATTR4args
                            {
                                stateid = new stateid4
                                {
                                    seqid = 0U,
                                    other = new byte[12],
                                },
                                obj_attributes = identityAttributes,
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

        private static bitmap4 CreateV40AttributeRequest(IReadOnlyList<OpenNfsV40AttributeKind> requestedAttributes)
        {
            ArgumentNullException.ThrowIfNull(requestedAttributes);
            if (requestedAttributes.Count < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedAttributes),
                    requestedAttributes.Count,
                    "The requested NFSv4 GETATTR attribute set must contain at least one attribute.");
            }

            List<int> attributeIds = new List<int>(requestedAttributes.Count);
            for (int index = 0; index < requestedAttributes.Count; index++)
            {
                int attributeId = (int)requestedAttributes[index];
                if (!attributeIds.Contains(attributeId))
                {
                    attributeIds.Add(attributeId);
                }
            }

            int highestAttributeId = -1;
            for (int index = 0; index < attributeIds.Count; index++)
            {
                highestAttributeId = Math.Max(highestAttributeId, attributeIds[index]);
            }

            uint[] words = new uint[(highestAttributeId / 32) + 1];
            for (int index = 0; index < attributeIds.Count; index++)
            {
                int attributeId = attributeIds[index];
                int wordIndex = attributeId / 32;
                int bitIndex = attributeId % 32;
                words[wordIndex] |= 1U << bitIndex;
            }

            return new bitmap4
            {
                Value = words,
            };
        }

        private static fattr4 CreateV40AclAttributes(IReadOnlyList<OpenNfsV40AclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            OpenNFS.Rpc.Xdr.XdrWriter writer = new OpenNFS.Rpc.Xdr.XdrWriter();
            nfsace4[] mappedEntries = new nfsace4[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                OpenNfsV40AclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL entry collections cannot contain null entries.");
                mappedEntries[index] = new nfsace4
                {
                    type = new acetype4
                    {
                        Value = (uint)entry.EntryType,
                    },
                    flag = new aceflag4
                    {
                        Value = (uint)entry.EntryFlags,
                    },
                    access_mask = new acemask4
                    {
                        Value = (uint)entry.Permissions,
                    },
                    who = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = System.Text.Encoding.UTF8.GetBytes(entry.Who),
                        },
                    },
                };
            }

            new fattr4_acl
            {
                Value = mappedEntries,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = CreateV40AttributeRequest(new[] { OpenNfsV40AttributeKind.Acl }),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        private static fattr4 CreateV40IdentityAttributes(string? owner, string? ownerGroup)
        {
            if (string.IsNullOrWhiteSpace(owner) && string.IsNullOrWhiteSpace(ownerGroup))
            {
                throw new ArgumentException("At least one of owner or owner-group must be supplied for NFSv4 identity updates.", nameof(owner));
            }

            XdrWriter writer = new XdrWriter();
            List<OpenNfsV40AttributeKind> attributeKinds = new List<OpenNfsV40AttributeKind>();

            if (!string.IsNullOrWhiteSpace(owner))
            {
                attributeKinds.Add(OpenNfsV40AttributeKind.Owner);
                new fattr4_owner
                {
                    Value = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(owner),
                        },
                    },
                }.WriteTo(writer);
            }

            if (!string.IsNullOrWhiteSpace(ownerGroup))
            {
                attributeKinds.Add(OpenNfsV40AttributeKind.OwnerGroup);
                new fattr4_owner_group
                {
                    Value = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(ownerGroup),
                        },
                    },
                }.WriteTo(writer);
            }

            return new fattr4
            {
                attrmask = CreateV40AttributeRequest(attributeKinds),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
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

        private static byte[] EncodeV40Payload(Action<OpenNFS.Rpc.Xdr.XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            OpenNFS.Rpc.Xdr.XdrWriter writer = new OpenNFS.Rpc.Xdr.XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        private static OpenNfsV3ProcedureRequest CreateSingleHandleRequest(byte[] fileHandle, uint procedureNumber)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateAccessRequest(
            byte[] fileHandle,
            OpenNfsV3AccessMask requestedAccess)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt32((uint)requestedAccess);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsAccessProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateReadRequest(byte[] fileHandle, ulong offset, uint count)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "The requested read byte count must be greater than zero.");
            }

            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsReadProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateWriteRequest(
            byte[] fileHandle,
            ulong offset,
            OpenNfsWriteStability stability,
            byte[] data)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            byte[] safeData = OpenNfsClientArgument.RequireBytes(data, nameof(data), allowEmpty: true);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32((uint)safeData.Length);
            writer.WriteInt32((int)stability);
            writer.WriteOpaque(safeData);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsWriteProcedure,
                procedurePayload: writer.ToArray());
        }

        private static OpenNfsV3ProcedureRequest CreateCommitRequest(byte[] fileHandle, ulong offset, uint count)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            OpenNfsClientXdrWriter writer = new OpenNfsClientXdrWriter();
            writer.WriteOpaque(safeFileHandle);
            writer.WriteUInt64(offset);
            writer.WriteUInt32(count);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsCommitProcedure,
                procedurePayload: writer.ToArray());
        }
    }
}
