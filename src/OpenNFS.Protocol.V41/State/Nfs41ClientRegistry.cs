namespace OpenNFS.Protocol.V41.State
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Tracks NFSv4.1 clients that have completed <c>EXCHANGE_ID</c>.
    /// </summary>
    /// <remarks>
    /// RFC 8881 §18.35 defines the matrix of cases that drive whether <c>EXCHANGE_ID</c> issues a new
    /// clientid, returns the existing one, or renews after a client reboot. This registry implements the
    /// same-owner / same-verifier-and-different-owner cases needed to support the in-process suite tests:
    /// repeated <c>EXCHANGE_ID</c> with identical owner returns the same clientid and increments the
    /// sequence id; a new owner gets a new clientid; a new verifier with a known owner refreshes both
    /// the clientid and sequence id (client reboot path).
    /// </remarks>
    public sealed class Nfs41ClientRegistry
    {
        private readonly object gate;
        private readonly Dictionary<string, RegistryEntry> ownerIndex;
        private readonly Dictionary<ulong, RegistryEntry> clientIdIndex;
        private long lastIssuedClientId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ClientRegistry"/> class.
        /// </summary>
        public Nfs41ClientRegistry()
        {
            gate = new object();
            ownerIndex = new Dictionary<string, RegistryEntry>(StringComparer.Ordinal);
            clientIdIndex = new Dictionary<ulong, RegistryEntry>();
            lastIssuedClientId = 0;
        }

        /// <summary>
        /// Registers or refreshes a client based on <paramref name="owner"/> and returns the resulting
        /// registration.
        /// </summary>
        /// <param name="owner">The client owner from the request.</param>
        /// <returns>The registration that should be surfaced to the client.</returns>
        public Nfs41ClientRegistration RegisterOrRefresh(Nfs41ClientOwner owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            string ownerKey = ToOwnerKey(owner.GetOwnerId());

            lock (gate)
            {
                if (ownerIndex.TryGetValue(ownerKey, out RegistryEntry? existing))
                {
                    if (existing.Owner.Equals(owner))
                    {
                        existing.SequenceId++;
                        return new Nfs41ClientRegistration(existing.ClientId, existing.SequenceId, existing.Owner);
                    }

                    clientIdIndex.Remove(existing.ClientId);
                }

                ulong newClientId = (ulong)Interlocked.Increment(ref lastIssuedClientId);
                RegistryEntry newEntry = new RegistryEntry(newClientId, sequenceId: 1, owner);
                ownerIndex[ownerKey] = newEntry;
                clientIdIndex[newClientId] = newEntry;
                return new Nfs41ClientRegistration(newClientId, newEntry.SequenceId, newEntry.Owner);
            }
        }

        /// <summary>
        /// Returns a value indicating whether <paramref name="clientId"/> is registered.
        /// </summary>
        /// <param name="clientId">The clientid to inspect.</param>
        /// <returns><c>true</c> when the clientid is registered.</returns>
        public bool Exists(ulong clientId)
        {
            lock (gate)
            {
                return clientIdIndex.ContainsKey(clientId);
            }
        }

        /// <summary>
        /// Removes the client with <paramref name="clientId"/>.
        /// </summary>
        /// <param name="clientId">The clientid to remove.</param>
        /// <returns><c>true</c> when the client was removed.</returns>
        public bool Remove(ulong clientId)
        {
            lock (gate)
            {
                if (!clientIdIndex.TryGetValue(clientId, out RegistryEntry? entry))
                {
                    return false;
                }

                clientIdIndex.Remove(clientId);
                ownerIndex.Remove(ToOwnerKey(entry.Owner.GetOwnerId()));
                return true;
            }
        }

        /// <summary>
        /// Returns the number of registered clients.
        /// </summary>
        public int Count
        {
            get
            {
                lock (gate)
                {
                    return clientIdIndex.Count;
                }
            }
        }

        private static string ToOwnerKey(byte[] ownerId)
        {
            return Convert.ToHexString(ownerId);
        }

        private sealed class RegistryEntry
        {
            public RegistryEntry(ulong clientId, uint sequenceId, Nfs41ClientOwner owner)
            {
                ClientId = clientId;
                SequenceId = sequenceId;
                Owner = owner;
            }

            public ulong ClientId { get; }

            public uint SequenceId { get; set; }

            public Nfs41ClientOwner Owner { get; }
        }
    }
}
