namespace OpenNFS.Protocol.V41.Backchannel
{
    using System;
    using OpenNFS.Protocol.V41.Generated;

    /// <summary>
    /// Represents the outcome of issuing a server-to-client <c>CB_COMPOUND</c> via
    /// <see cref="Nfs41CallbackInvoker"/>.
    /// </summary>
    public sealed class Nfs41CallbackOutcome
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41CallbackOutcome"/> class.
        /// </summary>
        /// <param name="response">The decoded callback response.</param>
        /// <param name="slotId">The back-channel slot id used.</param>
        /// <param name="sequenceId">The back-channel sequence id used.</param>
        public Nfs41CallbackOutcome(CB_COMPOUND4res response, uint slotId, uint sequenceId)
        {
            ArgumentNullException.ThrowIfNull(response);

            Response = response;
            SlotId = slotId;
            SequenceId = sequenceId;
        }

        /// <summary>
        /// Gets the decoded callback response.
        /// </summary>
        public CB_COMPOUND4res Response { get; }

        /// <summary>
        /// Gets the back-channel slot id used to send the request.
        /// </summary>
        public uint SlotId { get; }

        /// <summary>
        /// Gets the back-channel sequence id used to send the request.
        /// </summary>
        public uint SequenceId { get; }
    }
}
