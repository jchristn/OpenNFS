namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client;

    internal sealed class ClientConfiguration
    {
        public OpenNfsAuthenticationFlavor AuthenticationFlavor { get; set; } = OpenNfsAuthenticationFlavor.AuthSys;

        public string AuthSysMachineName { get; set; } = Environment.MachineName;

        public uint AuthSysUserId { get; set; }

        public uint AuthSysGroupId { get; set; }

        public IReadOnlyList<uint> AuthSysSupplementaryGroupIds { get; set; } = Array.Empty<uint>();

        public bool HasExplicitMountEndpoint
        {
            get
            {
                return !string.IsNullOrWhiteSpace(MountServerHost) && MountServerPort.HasValue;
            }
        }

        public string? MountServerHost { get; private set; }

        public int? MountServerPort { get; private set; }

        public string ServerHost { get; set; } = "localhost";

        public int ServerPort { get; set; } = 2049;

        public OpenNfsClientTransportPolicy TransportPolicy { get; set; } = OpenNfsClientTransportPolicy.TcpOnly;

        public void ClearMountEndpoint()
        {
            MountServerHost = null;
            MountServerPort = null;
        }

        public void SetMountEndpoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("The MOUNT host must contain a non-empty value.", nameof(host));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Port values must be between 1 and 65535.");
            }

            MountServerHost = host;
            MountServerPort = port;
        }
    }
}
