namespace OpenNFS.Client.Sessions
{
    /// <summary>
    /// Represents a leased slot id and sequence id pair issued to an outbound COMPOUND.
    /// </summary>
    internal sealed class OpenNfsV41SequenceLease
    {
        internal OpenNfsV41SequenceLease(uint slotId, uint sequenceId, uint highestSlotId)
        {
            SlotId = slotId;
            SequenceId = sequenceId;
            HighestSlotId = highestSlotId;
        }

        internal uint SlotId { get; }

        internal uint SequenceId { get; }

        internal uint HighestSlotId { get; }
    }
}
