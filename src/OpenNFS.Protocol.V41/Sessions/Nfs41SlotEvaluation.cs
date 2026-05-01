namespace OpenNFS.Protocol.V41.Sessions
{
    using System;

    /// <summary>
    /// Represents the outcome of evaluating an inbound <c>SEQUENCE</c> request against a slot table.
    /// </summary>
    public sealed class Nfs41SlotEvaluation
    {
        private Nfs41SlotEvaluation(Nfs41SlotState state, ReadOnlyMemory<byte> cachedReply)
        {
            State = state;
            CachedReply = cachedReply.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(cachedReply.ToArray());
        }

        /// <summary>
        /// Gets the slot decision.
        /// </summary>
        public Nfs41SlotState State { get; }

        /// <summary>
        /// Gets the cached reply bytes when <see cref="State"/> is <see cref="Nfs41SlotState.Replay"/>.
        /// </summary>
        public ReadOnlyMemory<byte> CachedReply { get; }

        /// <summary>
        /// Builds a fresh-request evaluation.
        /// </summary>
        /// <returns>The evaluation.</returns>
        public static Nfs41SlotEvaluation Fresh()
        {
            return new Nfs41SlotEvaluation(Nfs41SlotState.Fresh, ReadOnlyMemory<byte>.Empty);
        }

        /// <summary>
        /// Builds a replay evaluation.
        /// </summary>
        /// <param name="cachedReply">The cached reply bytes.</param>
        /// <returns>The evaluation.</returns>
        public static Nfs41SlotEvaluation Replay(ReadOnlyMemory<byte> cachedReply)
        {
            return new Nfs41SlotEvaluation(Nfs41SlotState.Replay, cachedReply);
        }

        /// <summary>
        /// Builds a rejection evaluation.
        /// </summary>
        /// <param name="state">The rejection state.</param>
        /// <returns>The evaluation.</returns>
        public static Nfs41SlotEvaluation Reject(Nfs41SlotState state)
        {
            if (state == Nfs41SlotState.Fresh || state == Nfs41SlotState.Replay)
            {
                throw new ArgumentException("Reject evaluations require a rejection state.", nameof(state));
            }

            return new Nfs41SlotEvaluation(state, ReadOnlyMemory<byte>.Empty);
        }
    }
}
