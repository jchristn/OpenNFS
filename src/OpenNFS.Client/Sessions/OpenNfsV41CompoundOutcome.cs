namespace OpenNFS.Client.Sessions
{
    using System;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Represents the result of sending a COMPOUND through an established NFSv4.1 client session.
    /// </summary>
    public sealed class OpenNfsV41CompoundOutcome
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41CompoundOutcome"/> class.
        /// </summary>
        /// <param name="response">The decoded COMPOUND result.</param>
        /// <param name="slotId">The slot id that carried the request.</param>
        /// <param name="sequenceId">The sequence id that carried the request.</param>
        public OpenNfsV41CompoundOutcome(COMPOUND4res response, uint slotId, uint sequenceId)
        {
            ArgumentNullException.ThrowIfNull(response);

            Response = response;
            SlotId = slotId;
            SequenceId = sequenceId;
        }

        /// <summary>
        /// Gets the decoded COMPOUND result.
        /// </summary>
        public COMPOUND4res Response { get; }

        /// <summary>
        /// Gets the slot id that carried the request.
        /// </summary>
        public uint SlotId { get; }

        /// <summary>
        /// Gets the sequence id that carried the request.
        /// </summary>
        public uint SequenceId { get; }
    }
}
