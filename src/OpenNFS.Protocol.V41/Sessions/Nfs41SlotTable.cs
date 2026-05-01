namespace OpenNFS.Protocol.V41.Sessions
{
    using System;

    /// <summary>
    /// Implements the RFC 8881 §2.10.6 slot table, including replay caching and exactly-once semantics.
    /// </summary>
    /// <remarks>
    /// Each slot tracks the last <c>sequenceid</c> the server has processed for that slot and, when the
    /// client asked for caching, the encoded reply bytes. Inbound <c>SEQUENCE</c> evaluations follow these
    /// rules from RFC 8881 §18.46.3:
    /// <list type="bullet">
    ///   <item>If the slot id is not within the table, return <see cref="Nfs41SlotState.BadSlot"/>.</item>
    ///   <item>If the sequenceid equals the last-observed value plus one, the request is fresh.</item>
    ///   <item>If the sequenceid equals the last-observed value, the request is a retry. Return the cached
    ///   reply when caching was requested, otherwise return <see cref="Nfs41SlotState.RetryUncached"/>.</item>
    ///   <item>Any other sequenceid is misordered.</item>
    /// </list>
    /// The first valid <c>sequenceid</c> after <c>CREATE_SESSION</c> is <c>1</c>; the slot's last-observed
    /// value starts at <c>0</c>.
    /// </remarks>
    public sealed class Nfs41SlotTable
    {
        private readonly object gate;
        private readonly SlotEntry[] slots;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41SlotTable"/> class.
        /// </summary>
        /// <param name="size">The slot count.</param>
        public Nfs41SlotTable(uint size)
        {
            if (size == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "A slot table must contain at least one slot.");
            }

            gate = new object();
            slots = new SlotEntry[size];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new SlotEntry();
            }
        }

        /// <summary>
        /// Gets the slot count.
        /// </summary>
        public uint Size => (uint)slots.Length;

        /// <summary>
        /// Evaluates a <c>SEQUENCE</c> request against the slot table without modifying state.
        /// </summary>
        /// <param name="slotId">The slot id from the request.</param>
        /// <param name="sequenceId">The sequenceid from the request.</param>
        /// <returns>The evaluation outcome.</returns>
        public Nfs41SlotEvaluation Evaluate(uint slotId, uint sequenceId)
        {
            if (slotId >= slots.Length)
            {
                return Nfs41SlotEvaluation.Reject(Nfs41SlotState.BadSlot);
            }

            lock (gate)
            {
                SlotEntry slot = slots[(int)slotId];
                uint expectedFresh = unchecked(slot.LastObservedSequenceId + 1u);

                if (sequenceId == expectedFresh)
                {
                    return Nfs41SlotEvaluation.Fresh();
                }

                if (slot.HasObservation && sequenceId == slot.LastObservedSequenceId)
                {
                    if (!slot.IsCached)
                    {
                        return Nfs41SlotEvaluation.Reject(Nfs41SlotState.RetryUncached);
                    }

                    return Nfs41SlotEvaluation.Replay(slot.CachedReply);
                }

                return Nfs41SlotEvaluation.Reject(Nfs41SlotState.Misordered);
            }
        }

        /// <summary>
        /// Records a fresh request as observed and stores its reply bytes when caching was requested.
        /// </summary>
        /// <param name="slotId">The slot id from the request.</param>
        /// <param name="sequenceId">The sequenceid from the request.</param>
        /// <param name="cacheReply">Whether the client asked for the reply to be cached.</param>
        /// <param name="replyBytes">The reply bytes to cache when <paramref name="cacheReply"/> is <c>true</c>.</param>
        public void RecordFreshRequest(uint slotId, uint sequenceId, bool cacheReply, ReadOnlyMemory<byte> replyBytes)
        {
            if (slotId >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), slotId, "Slot id is outside the table.");
            }

            lock (gate)
            {
                SlotEntry slot = slots[(int)slotId];
                uint expectedFresh = unchecked(slot.LastObservedSequenceId + 1u);
                if (sequenceId != expectedFresh)
                {
                    throw new InvalidOperationException(
                        "Cannot record a fresh request whose sequenceid does not match the slot's expected next value.");
                }

                slot.HasObservation = true;
                slot.LastObservedSequenceId = sequenceId;
                slot.IsCached = cacheReply;
                slot.CachedReply = cacheReply
                    ? new ReadOnlyMemory<byte>(replyBytes.ToArray())
                    : ReadOnlyMemory<byte>.Empty;
            }
        }

        private sealed class SlotEntry
        {
            public bool HasObservation;
            public uint LastObservedSequenceId;
            public bool IsCached;
            public ReadOnlyMemory<byte> CachedReply;
        }
    }
}
