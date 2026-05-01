namespace OpenNFS.Protocol.V41.Sessions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tracks established NFSv4.1 sessions keyed by their 16-byte session identifier.
    /// </summary>
    public sealed class Nfs41SessionRegistry
    {
        private readonly object gate;
        private readonly Dictionary<string, Nfs41Session> sessions;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41SessionRegistry"/> class.
        /// </summary>
        public Nfs41SessionRegistry()
        {
            gate = new object();
            sessions = new Dictionary<string, Nfs41Session>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Registers <paramref name="session"/> as an active session.
        /// </summary>
        /// <param name="session">The session to register.</param>
        public void Register(Nfs41Session session)
        {
            ArgumentNullException.ThrowIfNull(session);

            string key = session.SessionId.ToString();
            lock (gate)
            {
                sessions[key] = session;
            }
        }

        /// <summary>
        /// Attempts to retrieve the session whose identifier matches <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="session">Receives the matching session when one is registered.</param>
        /// <returns><c>true</c> when a matching session was found.</returns>
        public bool TryGet(Nfs41SessionId sessionId, out Nfs41Session? session)
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            string key = sessionId.ToString();
            lock (gate)
            {
                if (sessions.TryGetValue(key, out Nfs41Session? entry))
                {
                    session = entry;
                    return true;
                }
            }

            session = null;
            return false;
        }

        /// <summary>
        /// Removes the session whose identifier matches <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns><c>true</c> when a matching session was removed.</returns>
        public bool Remove(Nfs41SessionId sessionId)
        {
            ArgumentNullException.ThrowIfNull(sessionId);

            string key = sessionId.ToString();
            lock (gate)
            {
                return sessions.Remove(key);
            }
        }

        /// <summary>
        /// Returns the number of registered sessions.
        /// </summary>
        public int Count
        {
            get
            {
                lock (gate)
                {
                    return sessions.Count;
                }
            }
        }
    }
}
