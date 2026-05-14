namespace OpenNFS.Client.Sessions
{
    internal sealed class OpenNfsV41SessionEstablishmentResult
    {
        internal OpenNfsV41SessionEstablishmentResult(
            OpenNfsV41ClientConnection connection,
            byte[] sessionId,
            ulong clientId,
            uint negotiatedSlotCount,
            ulong serverMinorId,
            byte[] serverMajorId,
            byte[] serverScope)
        {
            Connection = connection;
            SessionId = sessionId;
            ClientId = clientId;
            NegotiatedSlotCount = negotiatedSlotCount;
            ServerMinorId = serverMinorId;
            ServerMajorId = serverMajorId;
            ServerScope = serverScope;
        }

        internal OpenNfsV41ClientConnection Connection { get; }

        internal byte[] SessionId { get; }

        internal ulong ClientId { get; }

        internal uint NegotiatedSlotCount { get; }

        internal ulong ServerMinorId { get; }

        internal byte[] ServerMajorId { get; }

        internal byte[] ServerScope { get; }
    }
}
