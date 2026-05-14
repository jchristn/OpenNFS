namespace Test.Shared
{
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal readonly struct LockingServiceContext
    {
        internal LockingServiceContext(OpenNfsServer server, Nfs40CompoundService service, NfsFileHandle docsHandle, NfsFileHandle notesHandle)
        {
            Server = server;
            Service = service;
            DocsHandle = docsHandle;
            NotesHandle = notesHandle;
        }

        internal OpenNfsServer Server { get; }

        internal Nfs40CompoundService Service { get; }

        internal NfsFileHandle DocsHandle { get; }

        internal NfsFileHandle NotesHandle { get; }
    }

    internal readonly struct ClientSessionContext
    {
        internal ClientSessionContext(ulong clientId, byte[] confirmationVerifier)
        {
            ClientId = clientId;
            ConfirmationVerifier = confirmationVerifier;
        }

        internal ulong ClientId { get; }

        internal byte[] ConfirmationVerifier { get; }
    }

    internal readonly struct ConfirmedOpenStateContext
    {
        internal ConfirmedOpenStateContext(ulong clientId, stateid4 confirmedOpenStateId)
        {
            ClientId = clientId;
            ConfirmedOpenStateId = confirmedOpenStateId;
        }

        internal ulong ClientId { get; }

        internal stateid4 ConfirmedOpenStateId { get; }
    }
}
