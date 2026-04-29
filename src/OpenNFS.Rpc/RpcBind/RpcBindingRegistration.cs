namespace OpenNFS.Rpc.RpcBind
{
    using System;
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Represents a normalized rpcbind or portmap registration entry.
    /// </summary>
    public sealed class RpcBindingRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcBindingRegistration"/> class.
        /// </summary>
        /// <param name="programNumber">The RPC program number.</param>
        /// <param name="versionNumber">The RPC version number.</param>
        /// <param name="protocol">The transport protocol.</param>
        /// <param name="port">The registered transport port.</param>
        /// <param name="netId">The rpcbind transport netid.</param>
        /// <param name="universalAddress">The rpcbind universal address.</param>
        /// <param name="owner">The rpcbind registration owner string.</param>
        public RpcBindingRegistration(
            uint programNumber,
            uint versionNumber,
            RpcBindingProtocol protocol,
            uint port,
            string netId,
            string universalAddress,
            string owner)
        {
            if (port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "The registration port must be between 0 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(netId))
            {
                throw new ArgumentException("The rpcbind netid must contain a non-empty value.", nameof(netId));
            }

            if (string.IsNullOrWhiteSpace(universalAddress))
            {
                throw new ArgumentException("The rpcbind universal address must contain a non-empty value.", nameof(universalAddress));
            }

            ArgumentNullException.ThrowIfNull(owner);

            ProgramNumber = programNumber;
            VersionNumber = versionNumber;
            Protocol = protocol;
            Port = port;
            NetId = netId;
            UniversalAddress = universalAddress;
            Owner = owner;
        }

        /// <summary>
        /// Gets the RPC program number.
        /// </summary>
        public uint ProgramNumber { get; }

        /// <summary>
        /// Gets the RPC version number.
        /// </summary>
        public uint VersionNumber { get; }

        /// <summary>
        /// Gets the transport protocol.
        /// </summary>
        public RpcBindingProtocol Protocol { get; }

        /// <summary>
        /// Gets the registered transport port.
        /// </summary>
        public uint Port { get; }

        /// <summary>
        /// Gets the rpcbind netid.
        /// </summary>
        public string NetId { get; }

        /// <summary>
        /// Gets the rpcbind universal address.
        /// </summary>
        public string UniversalAddress { get; }

        /// <summary>
        /// Gets the rpcbind owner string.
        /// </summary>
        public string Owner { get; }

        /// <summary>
        /// Converts the normalized registration into a portmap payload.
        /// </summary>
        /// <returns>The generated portmap registration payload.</returns>
        public mapping ToPortmapMapping()
        {
            return new mapping
            {
                prog = ProgramNumber,
                vers = VersionNumber,
                prot = (uint)Protocol,
                port = Port,
            };
        }

        /// <summary>
        /// Converts the normalized registration into an rpcbind payload.
        /// </summary>
        /// <returns>The generated rpcbind registration payload.</returns>
        public rpcb ToRpcbindRegistration()
        {
            return new rpcb
            {
                r_prog = ProgramNumber,
                r_vers = VersionNumber,
                r_netid = NetId,
                r_addr = UniversalAddress,
                r_owner = Owner,
            };
        }
    }
}
