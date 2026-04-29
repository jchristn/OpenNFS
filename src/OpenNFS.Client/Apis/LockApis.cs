namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
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
    /// Provides grouped locking-oriented convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class LockApis
    {
        private readonly OpenNfsClient _client;

        internal LockApis(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Prepares an NFSv4 COMPOUND lock-oriented plan using the default lock tag and NFSv4.0.
        /// </summary>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated COMPOUND plan.</returns>
        public Task<OpenNfsCompoundPlan> PrepareV4Async(IReadOnlyCollection<OpenNfsCompoundOperation> operations, CancellationToken cancellationToken)
        {
            return PrepareV4Async(OpenNfsProtocolVersion.Nfs40, "lock", operations, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4 COMPOUND lock-oriented plan.
        /// </summary>
        /// <param name="protocolVersion">NFSv4 protocol version to use.</param>
        /// <param name="tag">Client tag for the COMPOUND payload.</param>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated COMPOUND plan.</returns>
        public Task<OpenNfsCompoundPlan> PrepareV4Async(
            OpenNfsProtocolVersion protocolVersion,
            string tag,
            IReadOnlyCollection<OpenNfsCompoundOperation> operations,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(new OpenNfsCompoundRequest(protocolVersion, tag, operations), cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOCKT</c> flow and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>LOCKT</c> COMPOUND plan.
        /// </summary>
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

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LOCKT</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40LockResult ReadTestV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockTestResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOCK</c> flow for a new lock owner derived from an open state and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Prepares an NFSv4.0 grouped new-owner <c>LOCK</c> COMPOUND plan.
        /// </summary>
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

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOCK</c> flow for an existing lock owner and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Prepares an NFSv4.0 grouped existing-owner <c>LOCK</c> COMPOUND plan.
        /// </summary>
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

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LOCK</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40LockResult ReadLockV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 COMPOUND <c>PUTFH</c> + <c>LOCKU</c> flow and decodes the typed result.
        /// </summary>
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

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>LOCKU</c> COMPOUND plan.
        /// </summary>
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

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>LOCKU</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40LockResult ReadUnlockV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadLockUnlockResult(encodedReply);
        }

        /// <summary>
        /// Prepares an NLM v4 procedure plan for v3-era locking flows.
        /// </summary>
        /// <param name="procedureNumber">NLM v4 procedure number.</param>
        /// <param name="procedurePayload">XDR-encoded procedure payload.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <param name="retryMode">Retry mode for the raw procedure plan.</param>
        /// <returns>The validated raw procedure plan.</returns>
        public Task<OpenNfsV3ProcedurePlan> PrepareNlmV4ProcedureAsync(
            uint procedureNumber,
            byte[] procedurePayload,
            CancellationToken cancellationToken,
            OpenNfsRetryMode retryMode = OpenNfsRetryMode.UseClientPolicy)
        {
            byte[] safeProcedurePayload = OpenNfsClientArgument.RequireBytes(procedurePayload, nameof(procedurePayload), allowEmpty: true);
            return _client.PrepareV3ProcedureAsync(
                new OpenNfsV3ProcedureRequest(
                    procedureNumber: procedureNumber,
                    procedurePayload: safeProcedurePayload,
                    retryMode: retryMode,
                    programNumber: OpenNfsV3RpcConstants.NlmProgram,
                    versionNumber: OpenNfsV3RpcConstants.NlmVersion),
                cancellationToken);
        }

        /// <summary>
        /// Executes an NLM v4 <c>TEST</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsNlmV4TestResult> TestV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateTestRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, exclusive),
                "NLM v4 TEST",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadTestV4Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NLM v4 <c>TEST</c> plan.
        /// </summary>
        public Task<OpenNfsV3ProcedurePlan> PrepareTestV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateTestRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, exclusive),
                cancellationToken);
        }

        /// <summary>
        /// Executes an NLM v4 <c>LOCK</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsNlmV4Result> LockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateLockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive, reclaim, state),
                "NLM v4 LOCK",
                block ? OpenNfsTransportPipelineIdempotency.NonIdempotent : OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadLockV4Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NLM v4 <c>LOCK</c> plan.
        /// </summary>
        public Task<OpenNfsV3ProcedurePlan> PrepareLockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateLockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive, reclaim, state),
                cancellationToken);
        }

        /// <summary>
        /// Executes an NLM v4 <c>CANCEL</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsNlmV4Result> CancelV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateCancelRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive),
                "NLM v4 CANCEL",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadCancelV4Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NLM v4 <c>CANCEL</c> plan.
        /// </summary>
        public Task<OpenNfsV3ProcedurePlan> PrepareCancelV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateCancelRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length, block, exclusive),
                cancellationToken);
        }

        /// <summary>
        /// Executes an NLM v4 <c>UNLOCK</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsNlmV4Result> UnlockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteV3ProcedureAsync(
                CreateUnlockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                "NLM v4 UNLOCK",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadUnlockV4Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NLM v4 <c>UNLOCK</c> plan.
        /// </summary>
        public Task<OpenNfsV3ProcedurePlan> PrepareUnlockV4Async(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            CancellationToken cancellationToken)
        {
            return _client.PrepareV3ProcedureAsync(
                CreateUnlockRequest(cookie, fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a full RPC reply for an NLM v4 <c>TEST</c> request into a typed result.
        /// </summary>
        public OpenNfsNlmV4TestResult ReadTestV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadTestResult(encodedReply);
        }

        /// <summary>
        /// Decodes a full RPC reply for an NLM v4 <c>LOCK</c> request into a typed result.
        /// </summary>
        public OpenNfsNlmV4Result ReadLockV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 LOCK");
        }

        /// <summary>
        /// Decodes a full RPC reply for an NLM v4 <c>CANCEL</c> request into a typed result.
        /// </summary>
        public OpenNfsNlmV4Result ReadCancelV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 CANCEL");
        }

        /// <summary>
        /// Decodes a full RPC reply for an NLM v4 <c>UNLOCK</c> request into a typed result.
        /// </summary>
        public OpenNfsNlmV4Result ReadUnlockV4Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsNlmV4ReplyDecoder.ReadResult(encodedReply, "NLM v4 UNLOCK");
        }

        private static OpenNfsCompoundRequest CreateTestV40Request(
            byte[] fileHandle,
            ulong clientId,
            string lockOwner,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lockt",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCKT,
                        EncodeV40Payload(
                            new LOCKT4args
                            {
                                locktype = MapLockType(lockType),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                owner = CreateLockOwner(clientId, lockOwner),
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateLockFromOpenV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId openStateId,
            uint openSequenceId,
            ulong clientId,
            string lockOwner,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length,
            bool reclaim)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lock-open",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCK,
                        EncodeV40Payload(
                            new LOCK4args
                            {
                                locktype = MapLockType(lockType),
                                reclaim = reclaim,
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                locker = new locker4
                                {
                                    new_lock_owner = true,
                                    open_owner = new open_to_lock_owner4
                                    {
                                        open_seqid = CreateSequenceId(openSequenceId, nameof(openSequenceId)),
                                        open_stateid = CreateStateId(openStateId, nameof(openStateId)),
                                        lock_seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                        lock_owner = CreateLockOwner(clientId, lockOwner),
                                    },
                                },
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateLockV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "lock",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCK,
                        EncodeV40Payload(
                            new LOCK4args
                            {
                                locktype = MapLockType(lockType),
                                reclaim = false,
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                                locker = new locker4
                                {
                                    new_lock_owner = false,
                                    lock_owner = new exist_lock_owner4
                                    {
                                        lock_stateid = CreateStateId(lockStateId, nameof(lockStateId)),
                                        lock_seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                    },
                                },
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateUnlockV40Request(
            byte[] fileHandle,
            OpenNfsV40StateId lockStateId,
            uint lockSequenceId,
            OpenNfsV40LockType lockType,
            ulong offset,
            ulong length)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "locku",
                new OpenNfsCompoundOperation[]
                {
                    CreatePutFileHandleOperation(fileHandle, nameof(fileHandle)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_LOCKU,
                        EncodeV40Payload(
                            new LOCKU4args
                            {
                                locktype = MapLockType(lockType),
                                seqid = CreateSequenceId(lockSequenceId, nameof(lockSequenceId)),
                                lock_stateid = CreateStateId(lockStateId, nameof(lockStateId)),
                                offset = new offset4
                                {
                                    Value = offset,
                                },
                                length = new length4
                                {
                                    Value = length,
                                },
                            }.WriteTo)),
                });
        }

        private static nfs_lock_type4 MapLockType(OpenNfsV40LockType lockType)
        {
            return lockType switch
            {
                OpenNfsV40LockType.Read => nfs_lock_type4.READ_LT,
                OpenNfsV40LockType.Write => nfs_lock_type4.WRITE_LT,
                OpenNfsV40LockType.ReadWait => nfs_lock_type4.READW_LT,
                OpenNfsV40LockType.WriteWait => nfs_lock_type4.WRITEW_LT,
                _ => throw new ArgumentOutOfRangeException(nameof(lockType), lockType, "The requested NFSv4 lock type is not supported."),
            };
        }

        private static lock_owner4 CreateLockOwner(ulong clientId, string lockOwner)
        {
            return new lock_owner4
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
                owner = System.Text.Encoding.UTF8.GetBytes(OpenNfsClientArgument.RequireText(lockOwner, nameof(lockOwner))),
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

        private static OpenNfsCompoundOperation CreatePutFileHandleOperation(byte[] fileHandle, string parameterName)
        {
            return new OpenNfsCompoundOperation(
                (uint)nfs_opnum4.OP_PUTFH,
                EncodeV40Payload(
                    new PUTFH4args
                    {
                        @object = new nfs_fh4
                        {
                            Value = OpenNfsClientArgument.RequireBytes(fileHandle, parameterName, allowEmpty: false),
                        },
                    }.WriteTo));
        }

        private static byte[] EncodeV40Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);
            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        private static OpenNfsV3ProcedureRequest CreateTestRequest(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive)
        {
            nlm4_testargs arguments = new nlm4_testargs
            {
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmTestProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateLockRequest(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state)
        {
            nlm4_lockargs arguments = new nlm4_lockargs
            {
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                reclaim = reclaim,
                state = new int32
                {
                    Value = state,
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmLockProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateCancelRequest(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive)
        {
            nlm4_cancargs arguments = new nlm4_cancargs
            {
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmCancelProcedure, arguments.WriteTo);
        }

        private static OpenNfsV3ProcedureRequest CreateUnlockRequest(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            nlm4_unlockargs arguments = new nlm4_unlockargs
            {
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmUnlockProcedure, arguments.WriteTo);
        }

        private static nlm4_lock CreateLock(
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            string safeCallerName = OpenNfsClientArgument.RequireText(callerName, nameof(callerName));
            return new nlm4_lock
            {
                caller_name = safeCallerName,
                fh = CreateNetObject(fileHandle, nameof(fileHandle), allowEmpty: false),
                oh = CreateNetObject(ownerHandle, nameof(ownerHandle), allowEmpty: false),
                svid = new int32
                {
                    Value = ownerProcessId,
                },
                l_offset = new uint64
                {
                    Value = offset,
                },
                l_len = new uint64
                {
                    Value = length,
                },
            };
        }

        private static netobj CreateNetObject(byte[] value, string parameterName, bool allowEmpty)
        {
            byte[] safeValue = OpenNfsClientArgument.RequireBytes(value, parameterName, allowEmpty);
            return new netobj
            {
                Value = safeValue,
            };
        }

        private static OpenNfsV3ProcedureRequest CreateEncodedRequest(uint procedureNumber, Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: procedureNumber,
                procedurePayload: writer.ToArray(),
                programNumber: OpenNfsV3RpcConstants.NlmProgram,
                versionNumber: OpenNfsV3RpcConstants.NlmVersion);
        }
    }
}
