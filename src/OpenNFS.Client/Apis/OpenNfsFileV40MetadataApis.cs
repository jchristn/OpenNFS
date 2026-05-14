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
    /// Encapsulates grouped NFSv4.0 file metadata and identity flows.
    /// </summary>
    internal sealed class OpenNfsFileV40MetadataApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsFileV40MetadataApis(OpenNfsClient client)
        {
            _client = client;
        }

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
    }
}
