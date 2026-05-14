namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using OpenNFS.Client;
    using static OpenNFS.TestClient.ClientInputSupport;
    using static OpenNFS.TestClient.ClientRuntimeState;

    internal static class ClientConfigurationSupport
    {
        internal static void SetServer(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            string host = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Server host");
            int port = arguments.Count > 2 ? ParsePort(arguments[2], "server port") : ReadPort("Server port", Configuration.ServerPort);
            Configuration.ServerHost = host;
            Configuration.ServerPort = port;
            Console.WriteLine("[OK] Primary server endpoint set to " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        internal static void SetMountEndpoint(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            if (arguments.Count > 1 && string.Equals(arguments[1], "clear", StringComparison.OrdinalIgnoreCase))
            {
                Configuration.ClearMountEndpoint();
                Console.WriteLine("[OK] Dedicated MOUNT endpoint cleared.");
                return;
            }

            string host = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("MOUNT host");
            int port = arguments.Count > 2 ? ParsePort(arguments[2], "MOUNT port") : ReadPort("MOUNT port", Configuration.MountServerPort ?? 20048);
            Configuration.SetMountEndpoint(host, port);
            Console.WriteLine("[OK] Dedicated MOUNT endpoint set to " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        internal static void SetTransport(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            string value = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Transport [tcp|tcpudp]");
            switch (value.Trim().ToLowerInvariant())
            {
                case "tcp":
                    Configuration.TransportPolicy = OpenNfsClientTransportPolicy.TcpOnly;
                    break;
                case "tcpudp":
                case "tcp+udp":
                case "udp":
                    Configuration.TransportPolicy = OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3;
                    break;
                default:
                    throw new ArgumentException("Transport must be 'tcp' or 'tcpudp'.", nameof(arguments));
            }

            Console.WriteLine("[OK] Transport policy set to " + Configuration.TransportPolicy + ".");
        }

        internal static void SetAuthenticationFlavor(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            string value = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Authentication flavor [none|authsys|rpcsecgss]");
            switch (value.Trim().ToLowerInvariant())
            {
                case "none":
                    Configuration.AuthenticationFlavor = OpenNfsAuthenticationFlavor.AuthNone;
                    break;
                case "authsys":
                    Configuration.AuthenticationFlavor = OpenNfsAuthenticationFlavor.AuthSys;
                    break;
                case "rpcsecgss":
                case "gss":
                case "krb5":
                    Configuration.AuthenticationFlavor = OpenNfsAuthenticationFlavor.RpcSecGss;
                    break;
                default:
                    throw new ArgumentException("Authentication flavor must be 'none', 'authsys', or 'rpcsecgss'.", nameof(arguments));
            }

            Console.WriteLine("[OK] Authentication flavor set to " + Configuration.AuthenticationFlavor + ".");
        }

        internal static void ConfigureAuthSys(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            string machineName = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("AUTH_SYS machine name");
            uint userId = arguments.Count > 2 ? ParseUInt32(arguments[2], "AUTH_SYS uid") : ReadUInt32("AUTH_SYS uid", Configuration.AuthSysUserId);
            uint groupId = arguments.Count > 3 ? ParseUInt32(arguments[3], "AUTH_SYS gid") : ReadUInt32("AUTH_SYS gid", Configuration.AuthSysGroupId);
            IReadOnlyList<uint> supplementaryGroups = arguments.Count > 4
                ? ParseUInt32List(arguments[4], "AUTH_SYS supplementary gids")
                : ParseUInt32List(ReadValue("AUTH_SYS supplementary gids (comma-separated, blank for none)") ?? string.Empty, "AUTH_SYS supplementary gids");

            Configuration.AuthenticationFlavor = OpenNfsAuthenticationFlavor.AuthSys;
            Configuration.AuthSysMachineName = machineName;
            Configuration.AuthSysUserId = userId;
            Configuration.AuthSysGroupId = groupId;
            Configuration.AuthSysSupplementaryGroupIds = supplementaryGroups.ToArray();
            Console.WriteLine("[OK] AUTH_SYS identity updated.");
        }

        internal static OpenNfsClient BuildClient()
        {
            OpenNfsClientBuilder builder = new OpenNfsClientBuilder()
                .WithServer(Configuration.ServerHost, Configuration.ServerPort)
                .WithTransportPolicy(Configuration.TransportPolicy);

            if (Configuration.HasExplicitMountEndpoint && Configuration.MountServerHost is not null && Configuration.MountServerPort.HasValue)
            {
                builder.WithMountEndpoint(Configuration.MountServerHost, Configuration.MountServerPort.Value);
            }

            switch (Configuration.AuthenticationFlavor)
            {
                case OpenNfsAuthenticationFlavor.AuthNone:
                    builder.WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.AuthNone);
                    break;
                case OpenNfsAuthenticationFlavor.AuthSys:
                    builder.WithAuthSysCredentials(
                        Configuration.AuthSysMachineName,
                        Configuration.AuthSysUserId,
                        Configuration.AuthSysGroupId,
                        Configuration.AuthSysSupplementaryGroupIds);
                    break;
                case OpenNfsAuthenticationFlavor.RpcSecGss:
                    builder.WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.RpcSecGss);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported authentication flavor '" + Configuration.AuthenticationFlavor + "'.");
            }

            return builder.Build();
        }

        internal static OpenNfsClient RequireConnectedClient()
        {
            if (Client is null || Client.State != OpenNfsClientState.Open)
            {
                throw new InvalidOperationException("The client must be connected before this command can run.");
            }

            return Client;
        }

        internal static OpenNfsMountSession RequireMountedSession()
        {
            if (Session is null)
            {
                throw new InvalidOperationException("An export must be mounted before this command can run.");
            }

            return Session;
        }

        internal static void EnsureConfigurationCanChange()
        {
            if (Client is not null && Client.State == OpenNfsClientState.Open)
            {
                throw new InvalidOperationException("Disconnect the client before changing connection or authentication settings.");
            }
        }

        internal static int ReadPort(string prompt, int currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParsePort(value, prompt);
        }

        internal static int ParsePort(string value, string fieldName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(fieldName, "Port values must be between 1 and 65535.");
            }

            return port;
        }

        internal static uint ReadUInt32(string prompt, uint currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParseUInt32(value, prompt);
        }

        internal static uint ParseUInt32(string value, string fieldName)
        {
            if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
            {
                throw new ArgumentOutOfRangeException(fieldName, "Expected an unsigned 32-bit integer value.");
            }

            return parsed;
        }

        internal static IReadOnlyList<uint> ParseUInt32List(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<uint>();
            }

            string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            uint[] values = new uint[parts.Length];
            for (int index = 0; index < parts.Length; index++)
            {
                values[index] = ParseUInt32(parts[index], fieldName);
            }

            return values;
        }
    }
}
