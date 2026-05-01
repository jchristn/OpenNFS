namespace OpenNFS.Client.Sessions
{
    /// <summary>
    /// Represents the per-slot client-side state used when allocating NFSv4.1 SEQUENCE arguments.
    /// </summary>
    internal sealed class OpenNfsV41ClientSlot
    {
        internal OpenNfsV41ClientSlot(uint slotId)
        {
            SlotId = slotId;
            NextSequenceId = 1;
            IsAvailable = true;
        }

        internal uint SlotId { get; }

        internal uint NextSequenceId { get; set; }

        internal bool IsAvailable { get; set; }
    }
}
