namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using OpenNFS.Server;

    internal sealed class Nfs40WriteStateTracker
    {
        private static readonly ConditionalWeakTable<OpenNfsServer, Nfs40WriteStateTracker> StateByServer =
            new ConditionalWeakTable<OpenNfsServer, Nfs40WriteStateTracker>();

        private readonly byte[] _writeVerifier;

        private Nfs40WriteStateTracker()
        {
            _writeVerifier = new byte[8];
            RandomNumberGenerator.Fill(_writeVerifier);
        }

        internal static Nfs40WriteStateTracker ForServer(OpenNfsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            return StateByServer.GetValue(server, static _ => new Nfs40WriteStateTracker());
        }

        internal byte[] GetWriteVerifierBytes()
        {
            return _writeVerifier.AsSpan().ToArray();
        }
    }
}
