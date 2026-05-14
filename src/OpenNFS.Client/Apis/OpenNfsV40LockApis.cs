namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using static OpenNFS.Client.Internal.OpenNfsLockApiV40Requests;

    internal sealed class OpenNfsV40LockApis
    {
        private readonly OpenNfsClient _client;

        internal OpenNfsV40LockApis(OpenNfsClient client)
        {
            _client = client;
        }

        public Task<OpenNfsCompoundPlan> PrepareV4Async(IReadOnlyCollection<OpenNfsCompoundOperation> operations, CancellationToken cancellationToken)
        {
            return PrepareV4Async(OpenNfsProtocolVersion.Nfs40, "lock", operations, cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareV4Async(
            OpenNfsProtocolVersion protocolVersion,
            string tag,
            IReadOnlyCollection<OpenNfsCompoundOperation> operations,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(new OpenNfsCompoundRequest(protocolVersion, tag, operations), cancellationToken);
        }

        public Task<OpenNfsV40LockResult> TestV40Async(
            byte[] fileHandle,
            ulong clientId,
            string lockOwner,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateTestV40Request(fileHandle, clientId, lockOwner, lockType, offset, length),
                "NFSv4.0 LOCKT",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadTestV40Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareTestV40Async(
            byte[] fileHandle,
            ulong clientId,
            string lockOwner,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateTestV40Request(fileHandle, clientId, lockOwner, lockType, offset, length),
                cancellationToken);
        }

        public OpenNfsV40LockResult ReadTestV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockTestResult(encodedReply);
        }

        public Task<OpenNfsV40LockResult> LockFromOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            bool reclaim,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateLockFromOpenV40Request(
                    fileHandle,
                    openStateId,
                    openSequenceId,
                    clientId,
                    lockOwner,
                    lockSequenceId,
                    lockType,
                    offset,
                    length,
                    reclaim),
                "NFSv4.0 LOCK new owner",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadLockV40Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareLockFromOpenV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            bool reclaim,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateLockFromOpenV40Request(
                    fileHandle,
                    openStateId,
                    openSequenceId,
                    clientId,
                    lockOwner,
                    lockSequenceId,
                    lockType,
                    offset,
                    length,
                    reclaim),
                cancellationToken);
        }

        public Task<OpenNfsV40LockResult> LockV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateLockV40Request(fileHandle, lockStateId, lockSequenceId, lockType, offset, length),
                "NFSv4.0 LOCK existing owner",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadLockV40Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareLockV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateLockV40Request(fileHandle, lockStateId, lockSequenceId, lockType, offset, length),
                cancellationToken);
        }

        public OpenNfsV40LockResult ReadLockV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockResult(encodedReply);
        }

        public Task<OpenNfsV40LockResult> UnlockV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateUnlockV40Request(fileHandle, lockStateId, lockSequenceId, lockType, offset, length),
                "NFSv4.0 LOCKU",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadUnlockV40Result,
                cancellationToken);
        }

        public Task<OpenNfsCompoundPlan> PrepareUnlockV40Async(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateUnlockV40Request(fileHandle, lockStateId, lockSequenceId, lockType, offset, length),
                cancellationToken);
        }

        public OpenNfsV40LockResult ReadUnlockV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockUnlockResult(encodedReply);
        }
    }
}
