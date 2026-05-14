namespace OpenNFS.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V42.Generated;
    using static OpenNFS.Client.Apis.OpenNfsApiEncoding;

    internal sealed class OpenNfsV42GroupedSessionSupport : IAsyncDisposable
    {
        private const uint RequestedSlots = 4U;

        private readonly OpenNfsClientSettings settings;
        private readonly byte[] clientOwnerId;
        private readonly byte[] clientVerifier;
        private readonly SemaphoreSlim sessionLock;
        private SessionState? sessionState;
        private bool isDisposed;

        internal OpenNfsV42GroupedSessionSupport(OpenNfsClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            this.settings = settings;
            sessionLock = new SemaphoreSlim(1, 1);

            byte[] ownerSeed = Guid.NewGuid().ToByteArray();
            clientOwnerId = new byte[ownerSeed.Length];
            Buffer.BlockCopy(ownerSeed, 0, clientOwnerId, 0, ownerSeed.Length);
            clientVerifier = new byte[8];
            Buffer.BlockCopy(ownerSeed, 0, clientVerifier, 0, clientVerifier.Length);
        }

        internal async Task<TResult> ExecuteSequencedCompoundAsync<TResult>(
            OpenNfsCompoundRequest innerRequest,
            string operationName,
            OpenNfsOperationIdempotency idempotency,
            Func<ReadOnlyMemory<byte>, TResult> decodeReply,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(innerRequest);
            ArgumentNullException.ThrowIfNull(operationName);
            ArgumentNullException.ThrowIfNull(decodeReply);
            ThrowIfDisposed();

            SessionState state = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
            OpenNfsV41SequenceLease lease = state.SlotTable.AcquireLease();
            bool advanceSequence = false;
            bool invalidateSession = false;
            int reconnectAttemptsRemaining = Math.Max(0, settings.RetryPolicy.MaximumAttempts - 1);

            try
            {
                _ = idempotency;

                while (true)
                {
                    OpenNfsV42GroupedSessionConnection currentConnection = state.ConnectionManager.CurrentConnection;
                    OpenNfsCompoundRequest wrappedRequest = WrapWithSequence(innerRequest, state.SessionId, lease);
                    ReadOnlyMemory<byte> encodedReply;

                    try
                    {
                        encodedReply = await currentConnection
                            .ExecuteAsync(wrappedRequest, operationName, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is not OpenNfsClientException)
                    {
                        if (ShouldReconnectAfterTransportFailure(exception) && reconnectAttemptsRemaining > 0)
                        {
                            reconnectAttemptsRemaining--;
                            try
                            {
                                await state.ConnectionManager
                                    .ReconnectAfterTransportFailureAsync(currentConnection, cancellationToken)
                                    .ConfigureAwait(false);
                                continue;
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception reconnectFailure) when (reconnectFailure is not OpenNfsClientException)
                            {
                                invalidateSession = true;
                                throw OpenNfsClientExecutionEngine.TranslateExecutionException(operationName, reconnectFailure);
                            }
                        }

                        if (ShouldInvalidateSession(exception))
                        {
                            invalidateSession = true;
                        }

                        throw OpenNfsClientExecutionEngine.TranslateExecutionException(operationName, exception);
                    }

                    SequenceOutcome sequenceOutcome = ReadSequenceOutcome(encodedReply, operationName);
                    advanceSequence = sequenceOutcome.AdvanceSequence;
                    invalidateSession = sequenceOutcome.InvalidateSession;

                    try
                    {
                        return decodeReply(encodedReply);
                    }
                    catch (Exception exception) when (exception is not OpenNfsClientException)
                    {
                        throw OpenNfsClientExecutionEngine.CreateProtocolException(
                            operationName + " returned a malformed or unsupported reply payload.",
                            operationName,
                            exception,
                            isRetryable: false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            finally
            {
                if (invalidateSession)
                {
                    await InvalidateSessionAsync(state).ConfigureAwait(false);
                }
                else
                {
                    state.SlotTable.Release(lease, advanceSequence);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            SessionState? currentState = sessionState;
            sessionState = null;

            if (currentState is not null)
            {
                await DisposeStateAsync(currentState).ConfigureAwait(false);
            }

            sessionLock.Dispose();
        }

        private static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId)
        {
            channel_attrs4 channelAttributes = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0U },
                ca_maxrequestsize = new count4 { Value = 1024U * 1024U },
                ca_maxresponsesize = new count4 { Value = 1024U * 1024U },
                ca_maxresponsesize_cached = new count4 { Value = 64U * 1024U },
                ca_maxoperations = new count4 { Value = 16U },
                ca_maxrequests = new count4 { Value = RequestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4
                {
                    Value = clientId,
                },
                csa_sequence = new sequenceid4
                {
                    Value = sequenceId,
                },
                csa_flags = 0U,
                csa_fore_chan_attrs = channelAttributes,
                csa_back_chan_attrs = channelAttributes,
                csa_cb_program = 0U,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        private static EXCHANGE_ID4args BuildExchangeIdArguments(byte[] verifierBytes, byte[] ownerSeed)
        {
            byte[] safeVerifier = OpenNfsClientArgument.RequireFixedBytes(verifierBytes, expectedLength: 8, nameof(verifierBytes));
            byte[] safeOwnerSeed = OpenNfsClientArgument.RequireBytes(ownerSeed, nameof(ownerSeed), allowEmpty: false);
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4
                    {
                        Value = safeVerifier,
                    },
                    co_ownerid = safeOwnerSeed,
                },
                eia_flags = 0U,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        private static OpenNfsCompoundRequest BuildDestroySessionRequest(byte[] sessionId, ulong clientId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                "client-v42-destroy-session",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_DESTROY_SESSION,
                        EncodeV42Payload(
                            new DESTROY_SESSION4args
                            {
                                dsa_sessionid = new sessionid4
                                {
                                    Value = sessionId,
                                },
                            }.WriteTo)),
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_DESTROY_CLIENTID,
                        EncodeV42Payload(
                            new DESTROY_CLIENTID4args
                            {
                                dca_clientid = new clientid4
                                {
                                    Value = clientId,
                                },
                            }.WriteTo)),
                });
        }

        private static SequenceOutcome ReadSequenceOutcome(ReadOnlyMemory<byte> encodedReply, string operationName)
        {
            COMPOUND4res result = OpenNfsV42ReplyEnvelopeReader.ReadCompoundResult(encodedReply, operationName);
            if (result.status == nfsstat4.NFS4_OK)
            {
                return new SequenceOutcome(advanceSequence: true, invalidateSession: false);
            }

            if (result.resarray is null || result.resarray.Length < 1 || result.resarray[0].resop != nfs_opnum4.OP_SEQUENCE)
            {
                return new SequenceOutcome(advanceSequence: false, invalidateSession: false);
            }

            nfsstat4? sequenceStatus = result.resarray[0].opsequence?.sr_status;
            if (sequenceStatus == nfsstat4.NFS4_OK)
            {
                return new SequenceOutcome(advanceSequence: true, invalidateSession: false);
            }

            bool invalidateSession = sequenceStatus == nfsstat4.NFS4ERR_BADSESSION
                || sequenceStatus == nfsstat4.NFS4ERR_CONN_NOT_BOUND_TO_SESSION;
            return new SequenceOutcome(advanceSequence: false, invalidateSession: invalidateSession);
        }

        private static bool ShouldReconnectAfterTransportFailure(Exception exception)
        {
            return exception is IOException
                || exception is TimeoutException
                || exception is System.Net.Sockets.SocketException
                || exception is ObjectDisposedException;
        }

        private static bool ShouldInvalidateSession(Exception exception)
        {
            return exception is IOException
                || exception is TimeoutException
                || exception is System.Net.Sockets.SocketException
                || exception is ObjectDisposedException;
        }

        private static OpenNfsCompoundRequest WrapWithSequence(
            OpenNfsCompoundRequest innerRequest,
            byte[] sessionId,
            OpenNfsV41SequenceLease lease)
        {
            byte[] safeSessionId = OpenNfsClientArgument.RequireBytes(sessionId, nameof(sessionId), allowEmpty: false);
            List<OpenNfsCompoundOperation> operations = new List<OpenNfsCompoundOperation>(innerRequest.Operations.Count + 1)
            {
                new OpenNfsCompoundOperation(
                    (uint)nfs_opnum4.OP_SEQUENCE,
                    EncodeV42Payload(
                        new SEQUENCE4args
                        {
                            sa_sessionid = new sessionid4
                            {
                                Value = safeSessionId,
                            },
                            sa_sequenceid = new sequenceid4
                            {
                                Value = lease.SequenceId,
                            },
                            sa_slotid = new slotid4
                            {
                                Value = lease.SlotId,
                            },
                            sa_highest_slotid = new slotid4
                            {
                                Value = lease.HighestSlotId,
                            },
                            sa_cachethis = true,
                        }.WriteTo)),
            };

            for (int index = 0; index < innerRequest.Operations.Count; index++)
            {
                operations.Add(innerRequest.Operations[index]);
            }

            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs42,
                innerRequest.Tag,
                operations,
                innerRequest.RetryMode);
        }

        private async Task DisposeStateAsync(SessionState state)
        {
            try
            {
                await state.ConnectionManager.CurrentConnection
                    .ExecuteAsync(BuildDestroySessionRequest(state.SessionId, state.ClientId), "NFSv4.2 DESTROY_SESSION", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            await state.ConnectionManager.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<SessionState> EnsureSessionAsync(CancellationToken cancellationToken)
        {
            SessionState? currentState = sessionState;
            if (currentState is not null)
            {
                return currentState;
            }

            await sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                currentState = sessionState;
                if (currentState is not null)
                {
                    return currentState;
                }

                OpenNfsV42GroupedSessionConnection connection = await OpenNfsV42GroupedSessionConnection
                    .ConnectAsync(settings, cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    OpenNfsCompoundRequest exchangeRequest = new OpenNfsCompoundRequest(
                        OpenNfsProtocolVersion.Nfs42,
                        "client-v42-exchange-id",
                        new OpenNfsCompoundOperation[]
                        {
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_EXCHANGE_ID,
                                EncodeV42Payload(BuildExchangeIdArguments(clientVerifier, clientOwnerId).WriteTo)),
                        });
                    ReadOnlyMemory<byte> exchangeReply = await connection
                        .ExecuteAsync(exchangeRequest, "NFSv4.2 EXCHANGE_ID", cancellationToken)
                        .ConfigureAwait(false);
                    COMPOUND4res exchangeCompound = OpenNfsV42ReplyEnvelopeReader.ReadCompoundResult(exchangeReply, "NFSv4.2 EXCHANGE_ID");
                    if (exchangeCompound.status != nfsstat4.NFS4_OK
                        || exchangeCompound.resarray is null
                        || exchangeCompound.resarray.Length != 1
                        || exchangeCompound.resarray[0].opexchange_id?.eir_status != nfsstat4.NFS4_OK
                        || exchangeCompound.resarray[0].opexchange_id?.eir_resok4?.eir_clientid is null
                        || exchangeCompound.resarray[0].opexchange_id?.eir_resok4?.eir_sequenceid is null)
                    {
                        throw new OpenNfsClientProtocolException("The reusable NFSv4.2 grouped session path failed to establish a clientid through EXCHANGE_ID.");
                    }

                    EXCHANGE_ID4resok exchangeResult = exchangeCompound.resarray[0].opexchange_id!.eir_resok4!;
                    ulong clientId = exchangeResult.eir_clientid!.Value;
                    uint createSequenceId = exchangeResult.eir_sequenceid!.Value;

                    OpenNfsCompoundRequest createSessionRequest = new OpenNfsCompoundRequest(
                        OpenNfsProtocolVersion.Nfs42,
                        "client-v42-create-session",
                        new OpenNfsCompoundOperation[]
                        {
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_CREATE_SESSION,
                                EncodeV42Payload(BuildCreateSessionArguments(clientId, createSequenceId).WriteTo)),
                        });
                    ReadOnlyMemory<byte> createSessionReply = await connection
                        .ExecuteAsync(createSessionRequest, "NFSv4.2 CREATE_SESSION", cancellationToken)
                        .ConfigureAwait(false);
                    COMPOUND4res createSessionCompound = OpenNfsV42ReplyEnvelopeReader.ReadCompoundResult(createSessionReply, "NFSv4.2 CREATE_SESSION");
                    if (createSessionCompound.status != nfsstat4.NFS4_OK
                        || createSessionCompound.resarray is null
                        || createSessionCompound.resarray.Length != 1
                        || createSessionCompound.resarray[0].opcreate_session?.csr_status != nfsstat4.NFS4_OK
                        || createSessionCompound.resarray[0].opcreate_session?.csr_resok4?.csr_sessionid?.Value is null)
                    {
                        throw new OpenNfsClientProtocolException("The reusable NFSv4.2 grouped session path failed to establish a session through CREATE_SESSION.");
                    }

                    CREATE_SESSION4resok createSessionResult = createSessionCompound.resarray[0].opcreate_session!.csr_resok4!;
                    byte[] issuedSessionId = createSessionResult.csr_sessionid!.Value!;
                    uint negotiatedSlotCount = createSessionResult.csr_fore_chan_attrs?.ca_maxrequests?.Value ?? RequestedSlots;
                    if (negotiatedSlotCount == 0U)
                    {
                        throw new OpenNfsClientProtocolException("The reusable NFSv4.2 grouped session path negotiated zero fore-channel slots.");
                    }

                    currentState = new SessionState(
                        new OpenNfsV42GroupedSessionConnectionManager(settings, connection, issuedSessionId),
                        clientId,
                        CopyBytes(issuedSessionId),
                        new OpenNfsV41ClientSlotTable(negotiatedSlotCount));
                    sessionState = currentState;
                    return currentState;
                }
                catch
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                sessionLock.Release();
            }
        }

        private async Task InvalidateSessionAsync(SessionState failedState)
        {
            await sessionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(sessionState, failedState))
                {
                    return;
                }

                sessionState = null;
            }
            finally
            {
                sessionLock.Release();
            }
            await failedState.ConnectionManager.DisposeAsync().ConfigureAwait(false);
        }

        private static byte[] CopyBytes(byte[] bytes)
        {
            byte[] copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV42GroupedSessionSupport));
            }
        }

        private sealed class SessionState
        {
            internal SessionState(
                OpenNfsV42GroupedSessionConnectionManager connectionManager,
                ulong clientId,
                byte[] sessionId,
                OpenNfsV41ClientSlotTable slotTable)
            {
                ConnectionManager = connectionManager;
                ClientId = clientId;
                SessionId = sessionId;
                SlotTable = slotTable;
            }

            internal OpenNfsV42GroupedSessionConnectionManager ConnectionManager { get; }

            internal ulong ClientId { get; }

            internal byte[] SessionId { get; }

            internal OpenNfsV41ClientSlotTable SlotTable { get; }
        }

        private readonly struct SequenceOutcome
        {
            internal SequenceOutcome(bool advanceSequence, bool invalidateSession)
            {
                AdvanceSequence = advanceSequence;
                InvalidateSession = invalidateSession;
            }

            internal bool AdvanceSequence { get; }

            internal bool InvalidateSession { get; }
        }
    }
}
