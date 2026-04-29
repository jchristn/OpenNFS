namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using OpenNFS.Server;

    internal sealed class Nfs3WriteStateTracker
    {
        private static readonly ConditionalWeakTable<OpenNfsServer, Nfs3WriteStateTracker> StateByServer = new ConditionalWeakTable<OpenNfsServer, Nfs3WriteStateTracker>();
        private readonly byte[] _writeVerifier;

        private Nfs3WriteStateTracker()
        {
            _writeVerifier = new byte[8];
            RandomNumberGenerator.Fill(_writeVerifier);
        }

        internal static Nfs3WriteStateTracker ForServer(OpenNfsServer server)
        {
            ArgumentNullException.ThrowIfNull(server);
            return StateByServer.GetValue(server, static _ => new Nfs3WriteStateTracker());
        }

        internal byte[] GetWriteVerifierBytes()
        {
            return _writeVerifier.AsSpan().ToArray();
        }
    }
}
