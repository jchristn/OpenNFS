namespace OpenNFS.Rpc.Replay
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides an in-memory replay cache with explicit timestamp-based expiry.
    /// </summary>
    /// <typeparam name="TKey">The correlation-key type.</typeparam>
    /// <typeparam name="TValue">The cached reply value type.</typeparam>
    public sealed class RpcReplayCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly object gate;
        private readonly Dictionary<TKey, ReplayCacheEntry> entries;

        /// <summary>
        /// Initializes a new instance of the <see cref="RpcReplayCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="entryLifetime">The lifetime assigned to each cached entry.</param>
        public RpcReplayCache(TimeSpan entryLifetime)
        {
            if (entryLifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(entryLifetime), entryLifetime, "The replay-cache entry lifetime must be greater than zero.");
            }

            gate = new object();
            entries = new Dictionary<TKey, ReplayCacheEntry>();
            EntryLifetime = entryLifetime;
        }

        /// <summary>
        /// Gets the configured replay-cache entry lifetime.
        /// </summary>
        public TimeSpan EntryLifetime { get; }

        /// <summary>
        /// Gets the current number of cached entries.
        /// </summary>
        public int Count
        {
            get
            {
                lock (gate)
                {
                    return entries.Count;
                }
            }
        }

        /// <summary>
        /// Purges all expired entries at or before the supplied timestamp.
        /// </summary>
        /// <param name="now">The timestamp used for expiry evaluation.</param>
        /// <returns>The number of entries removed.</returns>
        public int PurgeExpired(DateTimeOffset now)
        {
            lock (gate)
            {
                List<TKey> expiredKeys = new List<TKey>();
                foreach (KeyValuePair<TKey, ReplayCacheEntry> pair in entries)
                {
                    if (pair.Value.ExpiresAt <= now)
                    {
                        expiredKeys.Add(pair.Key);
                    }
                }

                for (int index = 0; index < expiredKeys.Count; index++)
                {
                    entries.Remove(expiredKeys[index]);
                }

                return expiredKeys.Count;
            }
        }

        /// <summary>
        /// Stores or replaces a replay-cache entry.
        /// </summary>
        /// <param name="key">The correlation key for the cached value.</param>
        /// <param name="value">The cached reply value.</param>
        /// <param name="storedAt">The timestamp at which the value is considered stored.</param>
        public void Store(TKey key, TValue value, DateTimeOffset storedAt)
        {
            lock (gate)
            {
                entries[key] = new ReplayCacheEntry(value, storedAt + EntryLifetime);
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached value that has not expired at the supplied timestamp.
        /// </summary>
        /// <param name="key">The correlation key to look up.</param>
        /// <param name="now">The timestamp used for expiry evaluation.</param>
        /// <param name="value">When this method returns, contains the cached value when found.</param>
        /// <returns><c>true</c> when a non-expired entry was found; otherwise <c>false</c>.</returns>
        public bool TryGet(TKey key, DateTimeOffset now, out TValue? value)
        {
            lock (gate)
            {
                if (!entries.TryGetValue(key, out ReplayCacheEntry? entry))
                {
                    value = default;
                    return false;
                }

                if (entry.ExpiresAt <= now)
                {
                    entries.Remove(key);
                    value = default;
                    return false;
                }

                value = entry.Value;
                return true;
            }
        }

        private sealed class ReplayCacheEntry
        {
            public ReplayCacheEntry(TValue value, DateTimeOffset expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }

            public DateTimeOffset ExpiresAt { get; }

            public TValue Value { get; }
        }
    }
}
