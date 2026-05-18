namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using OpenNFS.Client;

    internal sealed class ClientConfiguration
    {
        private const int DefaultMountServerPort = 20048;
        private MountEndpointMode _mountEndpointMode = MountEndpointMode.DefaultDedicated;
        private string? _customMountServerHost;
        private int? _customMountServerPort;

        public OpenNfsAuthenticationFlavor AuthenticationFlavor { get; set; } = OpenNfsAuthenticationFlavor.AuthSys;

        public string AuthSysMachineName { get; set; } = Environment.MachineName;

        public uint AuthSysUserId { get; set; }

        public uint AuthSysGroupId { get; set; }

        public IReadOnlyList<uint> AuthSysSupplementaryGroupIds { get; set; } = Array.Empty<uint>();

        public bool HasExplicitMountEndpoint
        {
            get
            {
                return _mountEndpointMode != MountEndpointMode.Disabled;
            }
        }

        public string? MountServerHost => _mountEndpointMode switch
        {
            MountEndpointMode.Disabled => null,
            MountEndpointMode.Custom => _customMountServerHost,
            _ => ServerHost,
        };

        public int? MountServerPort => _mountEndpointMode switch
        {
            MountEndpointMode.Disabled => null,
            MountEndpointMode.Custom => _customMountServerPort,
            _ => DefaultMountServerPort,
        };

        public string PreferredExportPath { get; set; } = "/exports/test";

        public string ServerHost { get; set; } = "127.0.0.1";

        public int ServerPort { get; set; } = 2049;

        public OpenNfsClientTransportPolicy TransportPolicy { get; set; } = OpenNfsClientTransportPolicy.TcpOnly;

        public void ClearMountEndpoint()
        {
            _mountEndpointMode = MountEndpointMode.Disabled;
            _customMountServerHost = null;
            _customMountServerPort = null;
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

            _mountEndpointMode = MountEndpointMode.Custom;
            _customMountServerHost = host;
            _customMountServerPort = port;
        }

        public string GetAuthenticationFlavorDisplay()
        {
            return AuthenticationFlavor switch
            {
                OpenNfsAuthenticationFlavor.AuthNone => "none",
                OpenNfsAuthenticationFlavor.AuthSys => "authsys",
                OpenNfsAuthenticationFlavor.RpcSecGss => "rpcsecgss",
                _ => AuthenticationFlavor.ToString(),
            };
        }

        public string GetAuthSysDisplay()
        {
            string groups = AuthSysSupplementaryGroupIds.Count == 0
                ? "(none)"
                : string.Join(
                    ",",
                    AuthSysSupplementaryGroupIds.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
            return "machine="
                + AuthSysMachineName
                + ", uid="
                + AuthSysUserId.ToString(CultureInfo.InvariantCulture)
                + ", gid="
                + AuthSysGroupId.ToString(CultureInfo.InvariantCulture)
                + ", groups="
                + groups;
        }

        public string GetMountEndpointDisplay()
        {
            return HasExplicitMountEndpoint && MountServerHost is not null && MountServerPort.HasValue
                ? MountServerHost + ":" + MountServerPort.Value.ToString(CultureInfo.InvariantCulture)
                : "primary endpoint";
        }

        public string GetPrimaryEndpointDisplay()
        {
            return ServerHost + ":" + ServerPort.ToString(CultureInfo.InvariantCulture);
        }

        public string GetTransportDisplay()
        {
            return TransportPolicy == OpenNfsClientTransportPolicy.TcpOnly ? "tcp" : "tcpudp";
        }

        private enum MountEndpointMode
        {
            DefaultDedicated,
            Custom,
            Disabled,
        }
    }
}
