namespace OpenNFS.Client.Sessions
{
    using System;

    /// <summary>
    /// Tracks the client-side fore-channel slot state for an established NFSv4.1 session.
    /// </summary>
    /// <remarks>
    /// The client picks a free slot id, increments its per-slot sequenceid, sends the request, and
    /// returns the slot to the free pool when the reply arrives. This implementation serializes slot
    /// allocation under a single lock; that is sufficient for the current single-call-at-a-time test
    /// surface, and the public layer can grow concurrent slot leasing later without changing the wire
    /// shape.
    /// </remarks>
    internal sealed class OpenNfsV41ClientSlotTable
    {
        private readonly object gate;
        private readonly OpenNfsV41ClientSlot[] slots;

        internal OpenNfsV41ClientSlotTable(uint size)
        {
            if (size == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "A client slot table must contain at least one slot.");
            }

            gate = new object();
            slots = new OpenNfsV41ClientSlot[size];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = new OpenNfsV41ClientSlot((uint)index);
            }
        }

        internal uint Size => (uint)slots.Length;

        internal OpenNfsV41SequenceLease AcquireLease()
        {
            lock (gate)
            {
                for (int index = 0; index < slots.Length; index++)
                {
                    OpenNfsV41ClientSlot slot = slots[index];
                    if (!slot.IsAvailable)
                    {
                        continue;
                    }

                    slot.IsAvailable = false;
                    return new OpenNfsV41SequenceLease(slot.SlotId, slot.NextSequenceId, (uint)slots.Length - 1u);
                }
            }

            throw new InvalidOperationException("All NFSv4.1 client slots are currently in use; the slot table is exhausted.");
        }

        internal void Release(OpenNfsV41SequenceLease lease, bool advanceSequence)
        {
            ArgumentNullException.ThrowIfNull(lease);

            lock (gate)
            {
                OpenNfsV41ClientSlot slot = slots[(int)lease.SlotId];
                if (advanceSequence)
                {
                    slot.NextSequenceId = unchecked(slot.NextSequenceId + 1u);
                }

                slot.IsAvailable = true;
            }
        }
    }
}
