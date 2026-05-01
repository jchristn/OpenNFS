namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V41.State;

    /// <summary>
    /// Captures the immutable configuration that drives the NFSv4.1 session-management dispatcher.
    /// </summary>
    public sealed class Nfs41ServerConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41ServerConfiguration"/> class.
        /// </summary>
        /// <param name="serverOwner">The server owner advertised through <c>EXCHANGE_ID</c>.</param>
        /// <param name="serverScope">The server scope advertised through <c>EXCHANGE_ID</c>.</param>
        /// <param name="foreChannelMaximums">The maximum fore-channel limits the server is willing to accept.</param>
        /// <param name="backChannelMaximums">The maximum back-channel limits the server is willing to accept.</param>
        public Nfs41ServerConfiguration(
            Nfs41ServerOwner serverOwner,
            ReadOnlyMemory<byte> serverScope,
            Nfs41ChannelAttributes foreChannelMaximums,
            Nfs41ChannelAttributes backChannelMaximums)
        {
            ArgumentNullException.ThrowIfNull(serverOwner);
            ArgumentNullException.ThrowIfNull(foreChannelMaximums);
            ArgumentNullException.ThrowIfNull(backChannelMaximums);

            ServerOwner = serverOwner;
            ServerScope = serverScope.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(serverScope.ToArray());
            ForeChannelMaximums = foreChannelMaximums;
            BackChannelMaximums = backChannelMaximums;
        }

        /// <summary>
        /// Gets the server owner advertised through <c>EXCHANGE_ID</c>.
        /// </summary>
        public Nfs41ServerOwner ServerOwner { get; }

        /// <summary>
        /// Gets the server scope advertised through <c>EXCHANGE_ID</c>.
        /// </summary>
        public ReadOnlyMemory<byte> ServerScope { get; }

        /// <summary>
        /// Gets the maximum fore-channel limits the server is willing to accept.
        /// </summary>
        public Nfs41ChannelAttributes ForeChannelMaximums { get; }

        /// <summary>
        /// Gets the maximum back-channel limits the server is willing to accept.
        /// </summary>
        public Nfs41ChannelAttributes BackChannelMaximums { get; }
    }
}
