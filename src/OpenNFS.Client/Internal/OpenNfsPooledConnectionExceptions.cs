namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;

    /// <summary>
    /// The selected pooled connection could not accept the call; nothing was sent, so the call can be sent on another connection.
    /// </summary>
    internal sealed class OpenNfsConnectionUnavailableException : IOException
    {
        internal OpenNfsConnectionUnavailableException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The server closed the connection gracefully (EOF at a record boundary) while the call was outstanding.
    /// A server closes gracefully after answering its last call (idle timeout, one-call-per-connection servers, orderly
    /// shutdown), so the outstanding call was not executed and can be retransmitted with the same xid on a new connection.
    /// </summary>
    internal sealed class OpenNfsConnectionClosedByPeerException : IOException
    {
        internal OpenNfsConnectionClosedByPeerException(string message)
            : base(message)
        {
        }

        internal OpenNfsConnectionClosedByPeerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
