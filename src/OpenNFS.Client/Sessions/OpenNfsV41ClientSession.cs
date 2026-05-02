namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Represents an established NFSv4.1 client session bound to a single TCP connection.
    /// </summary>
    /// <remarks>
    /// The session is created through <see cref="EstablishAsync"/>. Each call to
    /// <see cref="SendCompoundAsync"/> auto-injects a <c>SEQUENCE</c> operation as the first op of the
    /// supplied operations, allocates a free slot, and increments the slot's per-slot sequence id when
    /// the call succeeds. Reply caching is requested per call through the <c>cacheReply</c> argument.
    /// <para>
    /// The current surface is intentionally minimal: it covers session establishment, single-slot-at-a-time
    /// COMPOUND dispatch, and clean teardown via <see cref="DisposeAsync"/>. Reconnect-and-retry recovery
    /// against a forced disconnect is tracked separately in Phase 10.1; the slot table on this session
    /// already preserves the per-slot sequence-id state that future reconnect logic will need.
    /// </para>
    /// </remarks>
    public sealed class OpenNfsV41ClientSession : IAsyncDisposable
    {
        private readonly OpenNfsV41ClientSlotTable slotTable;
        private readonly TimeSpan callTimeout;
        private readonly TimeSpan connectTimeout;
        private readonly IPEndPoint endpoint;
        private readonly byte[] sessionId;
        private readonly ulong clientId;
        private readonly bool autoReconnect;
        private readonly int maximumReconnectAttempts;
        private OpenNfsV41ClientConnection connection;
        private readonly SemaphoreSlim connectionLock;
        private bool isDisposed;

        private readonly byte[] serverMajorId;
        private readonly byte[] serverScope;
        private readonly ulong serverMinorId;

        private OpenNfsV41ClientSession(
            OpenNfsV41ClientConnection connection,
            byte[] sessionId,
            ulong clientId,
            uint negotiatedSlotCount,
            TimeSpan callTimeout,
            TimeSpan connectTimeout,
            IPEndPoint endpoint,
            bool autoReconnect,
            int maximumReconnectAttempts,
            ulong serverMinorId,
            byte[] serverMajorId,
            byte[] serverScope)
        {
            this.connection = connection;
            this.sessionId = sessionId;
            this.clientId = clientId;
            this.callTimeout = callTimeout;
            this.connectTimeout = connectTimeout;
            this.endpoint = endpoint;
            this.autoReconnect = autoReconnect;
            this.maximumReconnectAttempts = Math.Max(0, maximumReconnectAttempts);
            this.serverMinorId = serverMinorId;
            this.serverMajorId = serverMajorId;
            this.serverScope = serverScope;
            slotTable = new OpenNfsV41ClientSlotTable(negotiatedSlotCount);
            connectionLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Gets the 16-byte server-issued session identifier.
        /// </summary>
        public IReadOnlyList<byte> SessionId
        {
            get
            {
                byte[] copy = new byte[sessionId.Length];
                Buffer.BlockCopy(sessionId, 0, copy, 0, sessionId.Length);
                return copy;
            }
        }

        /// <summary>
        /// Gets the server-issued clientid.
        /// </summary>
        public ulong ClientId => clientId;

        /// <summary>
        /// Gets the negotiated fore-channel slot count.
        /// </summary>
        public uint NegotiatedSlotCount => slotTable.Size;

        /// <summary>
        /// Gets the server-owner minor identifier returned by <c>EXCHANGE_ID</c>.
        /// </summary>
        public ulong ServerMinorId => serverMinorId;

        /// <summary>
        /// Gets the server-owner major identifier returned by <c>EXCHANGE_ID</c>.
        /// </summary>
        /// <remarks>
        /// Per RFC 8881 §2.5, two sessions established against different network endpoints belong to
        /// the same NFS server when their major identifiers and scopes match. Clients can use this to
        /// detect server-side trunking opportunities.
        /// </remarks>
        public IReadOnlyList<byte> ServerMajorId
        {
            get
            {
                byte[] copy = new byte[serverMajorId.Length];
                Buffer.BlockCopy(serverMajorId, 0, copy, 0, serverMajorId.Length);
                return copy;
            }
        }

        /// <summary>
        /// Gets the server scope returned by <c>EXCHANGE_ID</c>.
        /// </summary>
        public IReadOnlyList<byte> ServerScope
        {
            get
            {
                byte[] copy = new byte[serverScope.Length];
                Buffer.BlockCopy(serverScope, 0, copy, 0, serverScope.Length);
                return copy;
            }
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="other"/> represents the same server
        /// instance as this session, by comparing the RFC 8881 §2.5 server major id and server scope.
        /// </summary>
        /// <param name="other">The session to compare against.</param>
        /// <returns><c>true</c> when both sessions point at the same server.</returns>
        public bool IsSameServerInstance(OpenNfsV41ClientSession other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return serverMajorId.AsSpan().SequenceEqual(other.serverMajorId)
                && serverScope.AsSpan().SequenceEqual(other.serverScope);
        }

        /// <summary>
        /// Gets the local TCP endpoint of the underlying connection.
        /// </summary>
        public IPEndPoint LocalEndpoint => connection.LocalEndpoint;

        /// <summary>
        /// Forcibly aborts the underlying TCP socket. Test-only hook used to simulate a network drop.
        /// </summary>
        internal void AbortConnectionForTest()
        {
            connection.AbortForTest();
        }

        /// <summary>
        /// Gets the remote TCP endpoint of the underlying connection.
        /// </summary>
        public IPEndPoint RemoteEndpoint => connection.RemoteEndpoint;

        /// <summary>
        /// Establishes a new NFSv4.1 client session over a fresh TCP connection.
        /// </summary>
        /// <param name="options">The session options.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The established session.</returns>
        public static async Task<OpenNfsV41ClientSession> EstablishAsync(
            OpenNfsV41ClientSessionOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);

            OpenNfsV41ClientConnection connection = await OpenNfsV41ClientConnection
                .ConnectAsync(options.Endpoint, options.ConnectTimeout, options.CallTimeout, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                COMPOUND4args exchangeIdCompound = new COMPOUND4args
                {
                    tag = MakeTag("client-exchange-id"),
                    minorversion = 1,
                    argarray = new[]
                    {
                        new nfs_argop4
                        {
                            argop = nfs_opnum4.OP_EXCHANGE_ID,
                            opexchange_id = BuildExchangeIdArguments(options.ClientOwner),
                        },
                    },
                };
                COMPOUND4res exchangeIdResponse = await connection.SendCompoundAsync(exchangeIdCompound, cancellationToken).ConfigureAwait(false);
                EnsureCompoundOk(exchangeIdResponse, "EXCHANGE_ID");
                EXCHANGE_ID4resok exchangeOk = exchangeIdResponse.resarray![0].opexchange_id?.eir_resok4
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include the expected resok payload.");

                ulong issuedClientId = exchangeOk.eir_clientid?.Value
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include a clientid.");
                uint createSequenceId = exchangeOk.eir_sequenceid?.Value
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include a sequenceid.");
                ulong issuedServerMinorId = exchangeOk.eir_server_owner?.so_minor_id ?? 0u;
                byte[] issuedServerMajorId = exchangeOk.eir_server_owner?.so_major_id ?? Array.Empty<byte>();
                byte[] issuedServerScope = exchangeOk.eir_server_scope ?? Array.Empty<byte>();

                COMPOUND4args createSessionCompound = new COMPOUND4args
                {
                    tag = MakeTag("client-create-session"),
                    minorversion = 1,
                    argarray = new[]
                    {
                        new nfs_argop4
                        {
                            argop = nfs_opnum4.OP_CREATE_SESSION,
                            opcreate_session = BuildCreateSessionArguments(issuedClientId, createSequenceId, options.RequestedSlots),
                        },
                    },
                };
                COMPOUND4res createSessionResponse = await connection.SendCompoundAsync(createSessionCompound, cancellationToken).ConfigureAwait(false);
                EnsureCompoundOk(createSessionResponse, "CREATE_SESSION");
                CREATE_SESSION4resok createOk = createSessionResponse.resarray![0].opcreate_session?.csr_resok4
                    ?? throw new InvalidOperationException("CREATE_SESSION succeeded but did not include the expected resok payload.");

                byte[] sessionIdBytes = createOk.csr_sessionid?.Value
                    ?? throw new InvalidOperationException("CREATE_SESSION succeeded but did not include a sessionid.");
                uint negotiatedSlotCount = createOk.csr_fore_chan_attrs?.ca_maxrequests?.Value
                    ?? options.RequestedSlots;
                if (negotiatedSlotCount == 0)
                {
                    throw new InvalidOperationException("CREATE_SESSION negotiated zero fore-channel slots.");
                }

                return new OpenNfsV41ClientSession(
                    connection,
                    sessionIdBytes,
                    issuedClientId,
                    negotiatedSlotCount,
                    options.CallTimeout,
                    options.ConnectTimeout,
                    options.Endpoint,
                    options.AutoReconnect,
                    options.MaximumReconnectAttempts,
                    issuedServerMinorId,
                    issuedServerMajorId,
                    issuedServerScope);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Returns a path-first ergonomic facade backed by this session. The facade exposes grouped
        /// <c>Metadata</c>, <c>Files</c>, and <c>Directories</c> members that mirror the v3
        /// <see cref="OpenNFS.Client.OpenNfsMountSession"/> shape but route through NFSv4.1 COMPOUNDs
        /// composed by <see cref="OpenNfsV41PathOperations"/>.
        /// </summary>
        /// <returns>The non-owning facade. The facade does not dispose this session.</returns>
        public OpenNfsV41MountSession CreateMountSession()
        {
            ThrowIfDisposed();
            return new OpenNfsV41MountSession(this);
        }

        /// <summary>
        /// Sends a COMPOUND through this session, auto-prefixed with a <c>SEQUENCE</c> operation.
        /// </summary>
        /// <param name="operations">The operations to send after SEQUENCE.</param>
        /// <param name="cacheReply">Whether the server should cache the reply for retransmission.</param>
        /// <param name="tag">The COMPOUND tag.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The COMPOUND outcome.</returns>
        public async Task<OpenNfsV41CompoundOutcome> SendCompoundAsync(
            IReadOnlyList<nfs_argop4> operations,
            bool cacheReply,
            string tag,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operations);
            ThrowIfDisposed();

            OpenNfsV41SequenceLease lease = slotTable.AcquireLease();
            bool advanceSequence = false;
            try
            {
                nfs_argop4[] argarray = BuildArgArrayWithSequence(lease, cacheReply, operations);
                COMPOUND4args compound = new COMPOUND4args
                {
                    tag = MakeTag(tag),
                    minorversion = 1,
                    argarray = argarray,
                };

                int reconnectAttemptsRemaining = autoReconnect ? Math.Max(1, maximumReconnectAttempts) : 0;

                while (true)
                {
                    using CancellationTokenSource callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    if (callTimeout > TimeSpan.Zero)
                    {
                        callCts.CancelAfter(callTimeout);
                    }

                    OpenNfsV41ClientConnection currentConnection = connection;
                    COMPOUND4res response;
                    try
                    {
                        response = await currentConnection.SendCompoundAsync(compound, callCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (IsTransportFailure(ex) && reconnectAttemptsRemaining > 0)
                    {
                        reconnectAttemptsRemaining--;
                        await ReconnectAfterTransportFailureAsync(currentConnection, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (response.status == nfsstat4.NFS4_OK)
                    {
                        advanceSequence = true;
                    }
                    else if (response.resarray is { Length: > 0 }
                        && response.resarray[0].resop == nfs_opnum4.OP_SEQUENCE
                        && response.resarray[0].opsequence?.sr_status == nfsstat4.NFS4_OK)
                    {
                        advanceSequence = true;
                    }

                    return new OpenNfsV41CompoundOutcome(response, lease.SlotId, lease.SequenceId);
                }
            }
            finally
            {
                slotTable.Release(lease, advanceSequence);
            }
        }

        /// <summary>
        /// Sends a COMPOUND through this session and returns a non-throwing
        /// <see cref="OpenNfsV41CompoundResult"/> envelope that distinguishes full success, partial
        /// success, and transport failure.
        /// </summary>
        /// <param name="operations">The operations to send after SEQUENCE.</param>
        /// <param name="cacheReply">Whether the server should cache the reply for retransmission.</param>
        /// <param name="tag">The COMPOUND tag.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The envelope.</returns>
        public async Task<OpenNfsV41CompoundResult> TrySendCompoundAsync(
            IReadOnlyList<nfs_argop4> operations,
            bool cacheReply,
            string tag,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operations);

            try
            {
                OpenNfsV41CompoundOutcome outcome = await SendCompoundAsync(
                    operations,
                    cacheReply,
                    tag,
                    cancellationToken).ConfigureAwait(false);

                int observedSuccessfulOps = CountSuccessfulOperations(outcome);
                bool overallOk = outcome.Response.status == nfsstat4.NFS4_OK;
                if (overallOk)
                {
                    return OpenNfsV41CompoundResult.FullSuccess(outcome, observedSuccessfulOps);
                }

                if (observedSuccessfulOps > 0)
                {
                    return OpenNfsV41CompoundResult.Partial(outcome, observedSuccessfulOps);
                }

                return OpenNfsV41CompoundResult.FullSuccess(outcome, operationsObservedSuccessfully: 0);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure) when (IsTransportFailure(failure) || failure is System.IO.EndOfStreamException || failure is OperationCanceledException)
            {
                return OpenNfsV41CompoundResult.TransportFailure(failure);
            }
        }

        private static int CountSuccessfulOperations(OpenNfsV41CompoundOutcome outcome)
        {
            nfs_resop4[] results = outcome.Response.resarray ?? Array.Empty<nfs_resop4>();
            int count = 0;
            for (int index = 0; index < results.Length; index++)
            {
                if (GetOpStatus(results[index]) == nfsstat4.NFS4_OK)
                {
                    count++;
                }
            }

            return count;
        }

        private static nfsstat4? GetOpStatus(nfs_resop4 result)
        {
            return result.resop switch
            {
                nfs_opnum4.OP_SEQUENCE => result.opsequence?.sr_status,
                nfs_opnum4.OP_EXCHANGE_ID => result.opexchange_id?.eir_status,
                nfs_opnum4.OP_CREATE_SESSION => result.opcreate_session?.csr_status,
                nfs_opnum4.OP_DESTROY_SESSION => result.opdestroy_session?.dsr_status,
                nfs_opnum4.OP_DESTROY_CLIENTID => result.opdestroy_clientid?.dcr_status,
                nfs_opnum4.OP_BIND_CONN_TO_SESSION => result.opbind_conn_to_session?.bctsr_status,
                nfs_opnum4.OP_ILLEGAL => result.opillegal?.status,
                _ => null,
            };
        }

        private async Task ReconnectAfterTransportFailureAsync(
            OpenNfsV41ClientConnection failedConnection,
            CancellationToken cancellationToken)
        {
            await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(connection, failedConnection))
                {
                    return;
                }

                OpenNfsV41ClientConnection newConnection = await OpenNfsV41ClientConnection
                    .ConnectAsync(endpoint, connectTimeout, callTimeout, cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    COMPOUND4args bindCompound = new COMPOUND4args
                    {
                        tag = MakeTag("client-auto-rebind"),
                        minorversion = 1,
                        argarray = new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                                opbind_conn_to_session = new BIND_CONN_TO_SESSION4args
                                {
                                    bctsa_sessid = new sessionid4 { Value = sessionId },
                                    bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                                    bctsa_use_conn_in_rdma_mode = false,
                                },
                            },
                        },
                    };

                    COMPOUND4res bindResponse = await newConnection.SendCompoundAsync(bindCompound, cancellationToken).ConfigureAwait(false);
                    EnsureCompoundOk(bindResponse, "BIND_CONN_TO_SESSION");
                }
                catch
                {
                    await newConnection.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                connection = newConnection;
                await failedConnection.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                connectionLock.Release();
            }
        }

        private static bool IsTransportFailure(Exception exception)
        {
            return exception is System.IO.IOException
                || exception is System.Net.Sockets.SocketException
                || exception is ObjectDisposedException;
        }

        /// <summary>
        /// Replaces the underlying TCP connection and re-binds it to this session via
        /// <c>BIND_CONN_TO_SESSION</c>.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>A task that completes when the new connection is bound.</returns>
        /// <remarks>
        /// Per RFC 8881 §8.6 and §18.34, a client that detects a transport break can preserve the
        /// underlying session by establishing a fresh connection and binding it. This call is
        /// idempotent: calling it on an already-healthy session simply replaces the connection. The
        /// session preserves the client-side per-slot sequence-id state so subsequent
        /// <see cref="SendCompoundAsync"/> calls remain compatible with the server's slot table.
        /// </remarks>
        public async Task ReconnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                OpenNfsV41ClientConnection oldConnection = connection;
                OpenNfsV41ClientConnection newConnection = await OpenNfsV41ClientConnection
                    .ConnectAsync(endpoint, connectTimeout, callTimeout, cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    COMPOUND4args bindCompound = new COMPOUND4args
                    {
                        tag = MakeTag("client-bind-conn"),
                        minorversion = 1,
                        argarray = new[]
                        {
                            new nfs_argop4
                            {
                                argop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                                opbind_conn_to_session = new BIND_CONN_TO_SESSION4args
                                {
                                    bctsa_sessid = new sessionid4 { Value = sessionId },
                                    bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                                    bctsa_use_conn_in_rdma_mode = false,
                                },
                            },
                        },
                    };

                    COMPOUND4res bindResponse = await newConnection.SendCompoundAsync(bindCompound, cancellationToken).ConfigureAwait(false);
                    EnsureCompoundOk(bindResponse, "BIND_CONN_TO_SESSION");
                }
                catch
                {
                    await newConnection.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

                connection = newConnection;
                await oldConnection.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                connectionLock.Release();
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            try
            {
                COMPOUND4args destroyCompound = new COMPOUND4args
                {
                    tag = MakeTag("client-destroy-session"),
                    minorversion = 1,
                    argarray = new[]
                    {
                        new nfs_argop4
                        {
                            argop = nfs_opnum4.OP_DESTROY_SESSION,
                            opdestroy_session = new DESTROY_SESSION4args
                            {
                                dsa_sessionid = new sessionid4 { Value = sessionId },
                            },
                        },
                        new nfs_argop4
                        {
                            argop = nfs_opnum4.OP_DESTROY_CLIENTID,
                            opdestroy_clientid = new DESTROY_CLIENTID4args
                            {
                                dca_clientid = new clientid4 { Value = clientId },
                            },
                        },
                    },
                };

                using CancellationTokenSource teardownCts = new CancellationTokenSource(callTimeout);
                await connection.SendCompoundAsync(destroyCompound, teardownCts.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            connectionLock.Dispose();

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        private nfs_argop4[] BuildArgArrayWithSequence(
            OpenNfsV41SequenceLease lease,
            bool cacheReply,
            IReadOnlyList<nfs_argop4> operations)
        {
            nfs_argop4 sequenceOp = new nfs_argop4
            {
                argop = nfs_opnum4.OP_SEQUENCE,
                opsequence = new SEQUENCE4args
                {
                    sa_sessionid = new sessionid4 { Value = sessionId },
                    sa_sequenceid = new sequenceid4 { Value = lease.SequenceId },
                    sa_slotid = new slotid4 { Value = lease.SlotId },
                    sa_highest_slotid = new slotid4 { Value = lease.HighestSlotId },
                    sa_cachethis = cacheReply,
                },
            };

            nfs_argop4[] argarray = new nfs_argop4[operations.Count + 1];
            argarray[0] = sequenceOp;
            for (int index = 0; index < operations.Count; index++)
            {
                argarray[index + 1] = operations[index];
            }

            return argarray;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(OpenNfsV41ClientSession));
            }
        }

        private static EXCHANGE_ID4args BuildExchangeIdArguments(OpenNfsV41ClientOwner owner)
        {
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4 { Value = owner.GetVerifier() },
                    co_ownerid = owner.GetOwnerId(),
                },
                eia_flags = 0,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        private static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            channel_attrs4 attrs = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0 },
                ca_maxrequestsize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new count4 { Value = 64 * 1024 },
                ca_maxoperations = new count4 { Value = 16 },
                ca_maxrequests = new count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4 { Value = clientId },
                csa_sequence = new sequenceid4 { Value = sequenceId },
                csa_flags = 0,
                csa_fore_chan_attrs = attrs,
                csa_back_chan_attrs = attrs,
                csa_cb_program = 0x40000000u,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        private static utf8str_cs MakeTag(string text)
        {
            return new utf8str_cs
            {
                Value = new utf8string { Value = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty) },
            };
        }

        private static void EnsureCompoundOk(COMPOUND4res response, string operationName)
        {
            if (response.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException(
                    "NFSv4.1 " + operationName + " COMPOUND returned " + response.status?.ToString() + ".");
            }

            if (response.resarray is null || response.resarray.Length == 0)
            {
                throw new InvalidOperationException(
                    "NFSv4.1 " + operationName + " COMPOUND returned an empty result array.");
            }
        }
    }
}
