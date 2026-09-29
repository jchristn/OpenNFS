namespace OpenNFS.Client.Internal
{
    using System.Security.Cryptography;

    /// <summary>
    /// Produces the initial ONC RPC transaction identifier for a client instance.
    /// </summary>
    /// <remarks>
    /// Seeding from <c>Environment.TickCount</c> caused clients created within the same timer tick on one host to issue
    /// identical xid sequences. Servers key their duplicate-request caches on host, credential, xid, and procedure, so a
    /// second client could be answered with a replayed (stale) reply that was cached for the first client's request.
    /// A cryptographically random seed makes such collisions practically impossible.
    /// </remarks>
    internal static class OpenNfsClientXidSeed
    {
        internal static int Create()
        {
            return RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        }
    }
}
