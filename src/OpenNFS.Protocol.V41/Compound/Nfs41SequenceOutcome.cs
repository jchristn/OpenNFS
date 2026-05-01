namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;

    /// <summary>
    /// Represents the outcome of evaluating a <c>SEQUENCE</c> operation.
    /// </summary>
    public sealed class Nfs41SequenceOutcome
    {
        private Nfs41SequenceOutcome(
            Nfs41SlotState state,
            SEQUENCE4res result,
            Nfs41Session? session,
            uint slotId,
            uint sequenceId,
            bool cacheRequested,
            ReadOnlyMemory<byte> cachedReply)
        {
            State = state;
            Result = result;
            Session = session;
            SlotId = slotId;
            SequenceId = sequenceId;
            CacheRequested = cacheRequested;
            CachedReply = cachedReply.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cachedReply.ToArray());
        }

        /// <summary>
        /// Gets the slot decision.
        /// </summary>
        public Nfs41SlotState State { get; }

        /// <summary>
        /// Gets the SEQUENCE result the caller should include in the reply.
        /// </summary>
        public SEQUENCE4res Result { get; }

        /// <summary>
        /// Gets the resolved session when <see cref="State"/> is <see cref="Nfs41SlotState.Fresh"/>
        /// or <see cref="Nfs41SlotState.Replay"/>.
        /// </summary>
        public Nfs41Session? Session { get; }

        /// <summary>
        /// Gets the slot id from the request.
        /// </summary>
        public uint SlotId { get; }

        /// <summary>
        /// Gets the sequence id from the request.
        /// </summary>
        public uint SequenceId { get; }

        /// <summary>
        /// Gets a value indicating whether the client requested reply caching.
        /// </summary>
        public bool CacheRequested { get; }

        /// <summary>
        /// Gets the cached reply bytes when <see cref="State"/> is <see cref="Nfs41SlotState.Replay"/>.
        /// </summary>
        public ReadOnlyMemory<byte> CachedReply { get; }

        /// <summary>
        /// Builds a Fresh outcome.
        /// </summary>
        /// <param name="result">The SEQUENCE result to surface.</param>
        /// <param name="session">The resolved session.</param>
        /// <param name="slotId">The slot id.</param>
        /// <param name="sequenceId">The sequence id.</param>
        /// <param name="cacheRequested">Whether the client asked for caching.</param>
        /// <returns>The outcome.</returns>
        public static Nfs41SequenceOutcome Fresh(
            SEQUENCE4res result,
            Nfs41Session session,
            uint slotId,
            uint sequenceId,
            bool cacheRequested)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(session);

            return new Nfs41SequenceOutcome(
                Nfs41SlotState.Fresh,
                result,
                session,
                slotId,
                sequenceId,
                cacheRequested,
                ReadOnlyMemory<byte>.Empty);
        }

        /// <summary>
        /// Builds a Replay outcome.
        /// </summary>
        /// <param name="result">The cached SEQUENCE result.</param>
        /// <param name="session">The resolved session.</param>
        /// <param name="slotId">The slot id.</param>
        /// <param name="sequenceId">The sequence id.</param>
        /// <param name="cachedReply">The cached reply bytes for the rest of the COMPOUND.</param>
        /// <returns>The outcome.</returns>
        public static Nfs41SequenceOutcome Replay(
            SEQUENCE4res result,
            Nfs41Session session,
            uint slotId,
            uint sequenceId,
            ReadOnlyMemory<byte> cachedReply)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(session);

            return new Nfs41SequenceOutcome(
                Nfs41SlotState.Replay,
                result,
                session,
                slotId,
                sequenceId,
                cacheRequested: true,
                cachedReply);
        }

        /// <summary>
        /// Builds a rejected outcome.
        /// </summary>
        /// <param name="state">The slot state.</param>
        /// <param name="result">The SEQUENCE result that surfaces the rejection.</param>
        /// <returns>The outcome.</returns>
        public static Nfs41SequenceOutcome Reject(Nfs41SlotState state, SEQUENCE4res result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (state == Nfs41SlotState.Fresh || state == Nfs41SlotState.Replay)
            {
                throw new ArgumentException("Reject outcomes require a rejection state.", nameof(state));
            }

            return new Nfs41SequenceOutcome(
                state,
                result,
                session: null,
                slotId: 0,
                sequenceId: 0,
                cacheRequested: false,
                cachedReply: ReadOnlyMemory<byte>.Empty);
        }
    }
}
