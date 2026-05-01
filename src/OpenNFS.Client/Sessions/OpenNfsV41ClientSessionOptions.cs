namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Net;

    /// <summary>
    /// Configures a single NFSv4.1 client session establishment.
    /// </summary>
    public sealed class OpenNfsV41ClientSessionOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41ClientSessionOptions"/> class.
        /// </summary>
        /// <param name="endpoint">The server endpoint to connect to.</param>
        /// <param name="clientOwner">The RFC 8881 §2.4 client owner identity.</param>
        public OpenNfsV41ClientSessionOptions(IPEndPoint endpoint, OpenNfsV41ClientOwner clientOwner)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(clientOwner);

            Endpoint = endpoint;
            ClientOwner = clientOwner;
            RequestedSlots = 8;
            ConnectTimeout = TimeSpan.FromSeconds(15);
            CallTimeout = TimeSpan.FromSeconds(30);
            AutoReconnect = false;
            MaximumReconnectAttempts = 1;
        }

        /// <summary>
        /// Gets the server endpoint to connect to.
        /// </summary>
        public IPEndPoint Endpoint { get; }

        /// <summary>
        /// Gets the RFC 8881 §2.4 client owner identity.
        /// </summary>
        public OpenNfsV41ClientOwner ClientOwner { get; }

        /// <summary>
        /// Gets or sets the requested fore-channel slot count.
        /// </summary>
        /// <remarks>
        /// The server is allowed to negotiate this value down. The session uses the negotiated value
        /// returned in <c>CREATE_SESSION4resok.csr_fore_chan_attrs.ca_maxrequests</c>.
        /// </remarks>
        public uint RequestedSlots { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the underlying TCP connection establishment.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout for each individual COMPOUND call.
        /// </summary>
        public TimeSpan CallTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the session should automatically reconnect and retry
        /// when a transport-level failure interrupts a <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/>
        /// call.
        /// </summary>
        /// <remarks>
        /// Per RFC 8881 §8.6, retrying with the same slot id and sequence id is safe: the server's slot
        /// table treats the retry as a replay (returning the cached reply when caching was requested,
        /// or surfacing <c>NFS4ERR_RETRY_UNCACHED_REP</c> otherwise) or, when the original request never
        /// reached the server, processes it normally.
        /// </remarks>
        public bool AutoReconnect { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of reconnect-and-retry attempts performed inside a single
        /// <see cref="OpenNfsV41ClientSession.SendCompoundAsync"/> call when <see cref="AutoReconnect"/>
        /// is enabled.
        /// </summary>
        public int MaximumReconnectAttempts { get; set; }
    }
}
