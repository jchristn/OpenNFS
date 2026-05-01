namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V41.State;

    /// <summary>
    /// Implements the NFSv4.1 session-management operations <c>EXCHANGE_ID</c>, <c>CREATE_SESSION</c>,
    /// <c>DESTROY_SESSION</c>, <c>DESTROY_CLIENTID</c>, <c>SEQUENCE</c>, and <c>BIND_CONN_TO_SESSION</c>
    /// over the in-memory client and session registries.
    /// </summary>
    /// <remarks>
    /// This processor runs at the typed argument/result level so it can be exercised without a full
    /// COMPOUND-over-RPC dispatcher. The wire-level dispatcher that decodes COMPOUND4args and emits
    /// COMPOUND4res is a follow-on task that wraps this processor.
    /// </remarks>
    public sealed class Nfs41SessionOperationProcessor
    {
        private readonly Nfs41ServerConfiguration configuration;
        private readonly Nfs41ClientRegistry clientRegistry;
        private readonly Nfs41SessionRegistry sessionRegistry;
        private readonly Func<byte[]> sessionIdAllocator;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41SessionOperationProcessor"/> class.
        /// </summary>
        /// <param name="configuration">The server configuration.</param>
        /// <param name="clientRegistry">The client registry.</param>
        /// <param name="sessionRegistry">The session registry.</param>
        /// <param name="sessionIdAllocator">
        /// A delegate that produces new 16-byte session-id values. Tests can supply a deterministic
        /// allocator; production servers can supply a cryptographic random source.
        /// </param>
        public Nfs41SessionOperationProcessor(
            Nfs41ServerConfiguration configuration,
            Nfs41ClientRegistry clientRegistry,
            Nfs41SessionRegistry sessionRegistry,
            Func<byte[]> sessionIdAllocator)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(clientRegistry);
            ArgumentNullException.ThrowIfNull(sessionRegistry);
            ArgumentNullException.ThrowIfNull(sessionIdAllocator);

            this.configuration = configuration;
            this.clientRegistry = clientRegistry;
            this.sessionRegistry = sessionRegistry;
            this.sessionIdAllocator = sessionIdAllocator;
        }

        /// <summary>
        /// Processes an <c>EXCHANGE_ID</c> request.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <returns>The result.</returns>
        public EXCHANGE_ID4res ProcessExchangeId(EXCHANGE_ID4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            client_owner4? rawOwner = arguments.eia_clientowner;
            if (rawOwner is null
                || rawOwner.co_verifier?.Value is null
                || rawOwner.co_verifier.Value.Length != 8
                || rawOwner.co_ownerid is null
                || rawOwner.co_ownerid.Length == 0)
            {
                return new EXCHANGE_ID4res
                {
                    eir_status = nfsstat4.NFS4ERR_INVAL,
                };
            }

            Nfs41ClientOwner owner = new Nfs41ClientOwner(rawOwner.co_verifier.Value, rawOwner.co_ownerid);
            Nfs41ClientRegistration registration = clientRegistry.RegisterOrRefresh(owner);

            return new EXCHANGE_ID4res
            {
                eir_status = nfsstat4.NFS4_OK,
                eir_resok4 = new EXCHANGE_ID4resok
                {
                    eir_clientid = new clientid4 { Value = registration.ClientId },
                    eir_sequenceid = new sequenceid4 { Value = registration.SequenceId },
                    eir_flags = 0,
                    eir_state_protect = new state_protect4_r
                    {
                        spr_how = state_protect_how4.SP4_NONE,
                    },
                    eir_server_owner = new server_owner4
                    {
                        so_minor_id = configuration.ServerOwner.MinorId,
                        so_major_id = configuration.ServerOwner.GetMajorId(),
                    },
                    eir_server_scope = configuration.ServerScope.ToArray(),
                    eir_server_impl_id = Array.Empty<nfs_impl_id4>(),
                },
            };
        }

        /// <summary>
        /// Processes a <c>CREATE_SESSION</c> request.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <param name="context">The connection context.</param>
        /// <returns>The result.</returns>
        public CREATE_SESSION4res ProcessCreateSession(
            CREATE_SESSION4args arguments,
            Nfs41OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            if (arguments.csa_clientid is null || arguments.csa_sequence is null
                || arguments.csa_fore_chan_attrs is null || arguments.csa_back_chan_attrs is null)
            {
                return new CREATE_SESSION4res { csr_status = nfsstat4.NFS4ERR_INVAL };
            }

            ulong clientId = arguments.csa_clientid.Value;
            if (!clientRegistry.Exists(clientId))
            {
                return new CREATE_SESSION4res { csr_status = nfsstat4.NFS4ERR_STALE_CLIENTID };
            }

            Nfs41ChannelAttributes requestedFore = ToChannelAttributes(arguments.csa_fore_chan_attrs);
            Nfs41ChannelAttributes requestedBack = ToChannelAttributes(arguments.csa_back_chan_attrs);
            Nfs41ChannelAttributes negotiatedFore = Nfs41ChannelAttributes.Negotiate(requestedFore, configuration.ForeChannelMaximums);
            Nfs41ChannelAttributes negotiatedBack = Nfs41ChannelAttributes.Negotiate(requestedBack, configuration.BackChannelMaximums);

            byte[] sessionIdBytes = sessionIdAllocator();
            Nfs41SessionId sessionId = new Nfs41SessionId(sessionIdBytes);
            Nfs41Session session = new Nfs41Session(
                sessionId,
                clientId,
                negotiatedFore,
                negotiatedBack,
                arguments.csa_cb_program,
                context.ConnectionIdentity);
            sessionRegistry.Register(session);

            return new CREATE_SESSION4res
            {
                csr_status = nfsstat4.NFS4_OK,
                csr_resok4 = new CREATE_SESSION4resok
                {
                    csr_sessionid = new sessionid4 { Value = sessionId.ToBytes() },
                    csr_sequence = arguments.csa_sequence,
                    csr_flags = 0,
                    csr_fore_chan_attrs = ToWire(negotiatedFore),
                    csr_back_chan_attrs = ToWire(negotiatedBack),
                },
            };
        }

        /// <summary>
        /// Processes a <c>DESTROY_SESSION</c> request.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <returns>The result.</returns>
        public DESTROY_SESSION4res ProcessDestroySession(DESTROY_SESSION4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            if (arguments.dsa_sessionid?.Value is null || arguments.dsa_sessionid.Value.Length != Nfs41SessionId.Length)
            {
                return new DESTROY_SESSION4res { dsr_status = nfsstat4.NFS4ERR_BADSESSION };
            }

            Nfs41SessionId sessionId = new Nfs41SessionId(arguments.dsa_sessionid.Value);
            return new DESTROY_SESSION4res
            {
                dsr_status = sessionRegistry.Remove(sessionId) ? nfsstat4.NFS4_OK : nfsstat4.NFS4ERR_BADSESSION,
            };
        }

        /// <summary>
        /// Processes a <c>DESTROY_CLIENTID</c> request.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <returns>The result.</returns>
        public DESTROY_CLIENTID4res ProcessDestroyClientId(DESTROY_CLIENTID4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            if (arguments.dca_clientid is null)
            {
                return new DESTROY_CLIENTID4res { dcr_status = nfsstat4.NFS4ERR_INVAL };
            }

            return new DESTROY_CLIENTID4res
            {
                dcr_status = clientRegistry.Remove(arguments.dca_clientid.Value)
                    ? nfsstat4.NFS4_OK
                    : nfsstat4.NFS4ERR_STALE_CLIENTID,
            };
        }

        /// <summary>
        /// Processes a <c>BIND_CONN_TO_SESSION</c> request.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <param name="context">The connection context.</param>
        /// <returns>The result.</returns>
        public BIND_CONN_TO_SESSION4res ProcessBindConnToSession(
            BIND_CONN_TO_SESSION4args arguments,
            Nfs41OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            if (arguments.bctsa_sessid?.Value is null || arguments.bctsa_sessid.Value.Length != Nfs41SessionId.Length)
            {
                return new BIND_CONN_TO_SESSION4res { bctsr_status = nfsstat4.NFS4ERR_BADSESSION };
            }

            Nfs41SessionId sessionId = new Nfs41SessionId(arguments.bctsa_sessid.Value);
            if (!sessionRegistry.TryGet(sessionId, out Nfs41Session? session) || session is null)
            {
                return new BIND_CONN_TO_SESSION4res { bctsr_status = nfsstat4.NFS4ERR_BADSESSION };
            }

            session.BindConnection(context.ConnectionIdentity);

            channel_dir_from_server4 direction = MapBindDirection(arguments.bctsa_dir);
            return new BIND_CONN_TO_SESSION4res
            {
                bctsr_status = nfsstat4.NFS4_OK,
                bctsr_resok4 = new BIND_CONN_TO_SESSION4resok
                {
                    bctsr_sessid = new sessionid4 { Value = sessionId.ToBytes() },
                    bctsr_dir = direction,
                    bctsr_use_conn_in_rdma_mode = arguments.bctsa_use_conn_in_rdma_mode,
                },
            };
        }

        /// <summary>
        /// Processes a <c>SEQUENCE</c> request and returns the slot evaluation outcome.
        /// </summary>
        /// <param name="arguments">The request arguments.</param>
        /// <param name="context">The connection context. The processor sets <see cref="Nfs41OperationContext.CurrentSession"/>
        /// to the resolved session on Fresh and Replay outcomes.</param>
        /// <returns>The outcome.</returns>
        public Nfs41SequenceOutcome ProcessSequence(SEQUENCE4args arguments, Nfs41OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            if (arguments.sa_sessionid?.Value is null || arguments.sa_sessionid.Value.Length != Nfs41SessionId.Length
                || arguments.sa_sequenceid is null || arguments.sa_slotid is null || arguments.sa_highest_slotid is null)
            {
                return Nfs41SequenceOutcome.Reject(
                    Nfs41SlotState.BadSlot,
                    new SEQUENCE4res { sr_status = nfsstat4.NFS4ERR_BADSESSION });
            }

            Nfs41SessionId sessionId = new Nfs41SessionId(arguments.sa_sessionid.Value);
            if (!sessionRegistry.TryGet(sessionId, out Nfs41Session? session) || session is null)
            {
                return Nfs41SequenceOutcome.Reject(
                    Nfs41SlotState.BadSlot,
                    new SEQUENCE4res { sr_status = nfsstat4.NFS4ERR_BADSESSION });
            }

            uint slotId = arguments.sa_slotid.Value;
            uint sequenceId = arguments.sa_sequenceid.Value;
            Nfs41SlotEvaluation evaluation = session.ForeChannelSlotTable.Evaluate(slotId, sequenceId);

            switch (evaluation.State)
            {
                case Nfs41SlotState.Fresh:
                    context.CurrentSession = session;
                    return Nfs41SequenceOutcome.Fresh(
                        new SEQUENCE4res
                        {
                            sr_status = nfsstat4.NFS4_OK,
                            sr_resok4 = new SEQUENCE4resok
                            {
                                sr_sessionid = new sessionid4 { Value = sessionId.ToBytes() },
                                sr_sequenceid = new sequenceid4 { Value = sequenceId },
                                sr_slotid = new slotid4 { Value = slotId },
                                sr_highest_slotid = new slotid4 { Value = arguments.sa_highest_slotid.Value },
                                sr_target_highest_slotid = new slotid4 { Value = session.ForeChannelSlotTable.Size - 1u },
                                sr_status_flags = 0,
                            },
                        },
                        session,
                        slotId,
                        sequenceId,
                        arguments.sa_cachethis);

                case Nfs41SlotState.Replay:
                    context.CurrentSession = session;
                    return Nfs41SequenceOutcome.Replay(
                        new SEQUENCE4res
                        {
                            sr_status = nfsstat4.NFS4_OK,
                            sr_resok4 = new SEQUENCE4resok
                            {
                                sr_sessionid = new sessionid4 { Value = sessionId.ToBytes() },
                                sr_sequenceid = new sequenceid4 { Value = sequenceId },
                                sr_slotid = new slotid4 { Value = slotId },
                                sr_highest_slotid = new slotid4 { Value = arguments.sa_highest_slotid.Value },
                                sr_target_highest_slotid = new slotid4 { Value = session.ForeChannelSlotTable.Size - 1u },
                                sr_status_flags = 0,
                            },
                        },
                        session,
                        slotId,
                        sequenceId,
                        evaluation.CachedReply);

                case Nfs41SlotState.BadSlot:
                    return Nfs41SequenceOutcome.Reject(
                        Nfs41SlotState.BadSlot,
                        new SEQUENCE4res { sr_status = nfsstat4.NFS4ERR_BADSLOT });

                case Nfs41SlotState.RetryUncached:
                    return Nfs41SequenceOutcome.Reject(
                        Nfs41SlotState.RetryUncached,
                        new SEQUENCE4res { sr_status = nfsstat4.NFS4ERR_RETRY_UNCACHED_REP });

                case Nfs41SlotState.Misordered:
                default:
                    return Nfs41SequenceOutcome.Reject(
                        Nfs41SlotState.Misordered,
                        new SEQUENCE4res { sr_status = nfsstat4.NFS4ERR_BAD_SEQID });
            }
        }

        /// <summary>
        /// Records the post-COMPOUND reply bytes for the slot referenced by <paramref name="outcome"/>
        /// when caching was requested. Call this exactly once after processing a Fresh outcome.
        /// </summary>
        /// <param name="outcome">The Fresh outcome returned by <see cref="ProcessSequence"/>.</param>
        /// <param name="replyBytes">The encoded post-SEQUENCE reply bytes to cache.</param>
        public void RecordSequenceReply(Nfs41SequenceOutcome outcome, ReadOnlyMemory<byte> replyBytes)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            if (outcome.State != Nfs41SlotState.Fresh)
            {
                throw new InvalidOperationException("Reply caching only applies to Fresh SEQUENCE outcomes.");
            }

            if (outcome.Session is null)
            {
                throw new InvalidOperationException("A Fresh outcome must carry the resolved session.");
            }

            outcome.Session.ForeChannelSlotTable.RecordFreshRequest(
                outcome.SlotId,
                outcome.SequenceId,
                outcome.CacheRequested,
                replyBytes);
        }

        private static Nfs41ChannelAttributes ToChannelAttributes(channel_attrs4 wire)
        {
            return new Nfs41ChannelAttributes(
                headerPadSize: ValueOrZero(wire.ca_headerpadsize),
                maximumRequestSize: ValueOrZero(wire.ca_maxrequestsize),
                maximumResponseSize: ValueOrZero(wire.ca_maxresponsesize),
                maximumCachedResponseSize: ValueOrZero(wire.ca_maxresponsesize_cached),
                maximumOperations: NonZero(wire.ca_maxoperations),
                maximumRequests: NonZero(wire.ca_maxrequests));
        }

        private static channel_attrs4 ToWire(Nfs41ChannelAttributes attributes)
        {
            return new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = attributes.HeaderPadSize },
                ca_maxrequestsize = new count4 { Value = attributes.MaximumRequestSize },
                ca_maxresponsesize = new count4 { Value = attributes.MaximumResponseSize },
                ca_maxresponsesize_cached = new count4 { Value = attributes.MaximumCachedResponseSize },
                ca_maxoperations = new count4 { Value = attributes.MaximumOperations },
                ca_maxrequests = new count4 { Value = attributes.MaximumRequests },
                ca_rdma_ird = Array.Empty<uint>(),
            };
        }

        private static channel_dir_from_server4 MapBindDirection(channel_dir_from_client4? requested)
        {
            return requested switch
            {
                channel_dir_from_client4.CDFC4_FORE => channel_dir_from_server4.CDFS4_FORE,
                channel_dir_from_client4.CDFC4_BACK => channel_dir_from_server4.CDFS4_BACK,
                channel_dir_from_client4.CDFC4_FORE_OR_BOTH => channel_dir_from_server4.CDFS4_BOTH,
                channel_dir_from_client4.CDFC4_BACK_OR_BOTH => channel_dir_from_server4.CDFS4_BOTH,
                _ => channel_dir_from_server4.CDFS4_FORE,
            };
        }

        private static uint ValueOrZero(count4? wire)
        {
            return wire?.Value ?? 0u;
        }

        private static uint NonZero(count4? wire)
        {
            uint value = wire?.Value ?? 0u;
            return value == 0u ? 1u : value;
        }
    }
}
