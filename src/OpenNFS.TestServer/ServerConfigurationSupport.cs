namespace OpenNFS.TestServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using static OpenNFS.TestServer.ServerInputSupport;
    using static OpenNFS.TestServer.ServerRuntimeState;

    internal static class ServerConfigurationSupport
    {
        internal static void SetServerName(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ServerName = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Server name");
            Console.WriteLine("[OK] Server name set to " + Configuration.ServerName + ".");
        }

        internal static void SetListenerAddress(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ListenerAddress = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Listener address");
            Console.WriteLine("[OK] Listener address set to " + Configuration.ListenerAddress + ".");
        }

        internal static void SetPort(IReadOnlyList<string> arguments, PortTarget target)
        {
            EnsureConfigurationCanChange();
            string prompt = target switch
            {
                PortTarget.Mount => "MOUNT port",
                PortTarget.NfsV3 => "NFSv3 port",
                PortTarget.NfsV40 => "NFSv4.0 port",
                PortTarget.NfsV41 => "NFSv4.1 port",
                PortTarget.NfsV42 => "NFSv4.2 port",
                _ => "Port",
            };

            int currentValue = target switch
            {
                PortTarget.Mount => Configuration.MountPort,
                PortTarget.NfsV3 => Configuration.NfsPort,
                PortTarget.NfsV40 => Configuration.Nfs40Port,
                PortTarget.NfsV41 => Configuration.Nfs41Port,
                PortTarget.NfsV42 => Configuration.Nfs42Port,
                _ => 0,
            };

            int port = arguments.Count > 1 ? ParsePortAllowZero(arguments[1], prompt) : ReadPortAllowZero(prompt, currentValue);

            switch (target)
            {
                case PortTarget.Mount:
                    Configuration.MountPort = port;
                    break;
                case PortTarget.NfsV3:
                    Configuration.NfsPort = port;
                    break;
                case PortTarget.NfsV40:
                    Configuration.Nfs40Port = port;
                    break;
                case PortTarget.NfsV41:
                    Configuration.Nfs41Port = port;
                    break;
                case PortTarget.NfsV42:
                    Configuration.Nfs42Port = port;
                    break;
            }

            Console.WriteLine("[OK] " + prompt + " set to " + port.ToString(CultureInfo.InvariantCulture) + ".");
        }

        internal static void SetExportPath(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ExportPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Export path");
            Console.WriteLine("[OK] Export path set to " + Configuration.ExportPath + ".");
        }

        internal static void SetToggle(IReadOnlyList<string> arguments, ToggleTarget target)
        {
            EnsureConfigurationCanChange();
            string name = target switch
            {
                ToggleTarget.ReadOnly => "Read-only",
                ToggleTarget.DenyMounts => "Deny mounts",
                ToggleTarget.PreserveOnExit => "Preserve-on-exit",
                ToggleTarget.EnableNfsV3 => "Enable NFSv3",
                ToggleTarget.EnableNfsV40 => "Enable NFSv4.0",
                ToggleTarget.EnableNfsV41 => "Enable NFSv4.1",
                ToggleTarget.EnableNfsV42 => "Enable NFSv4.2",
                _ => "Toggle",
            };

            bool currentValue = target switch
            {
                ToggleTarget.ReadOnly => Configuration.ReadOnly,
                ToggleTarget.DenyMounts => Configuration.DenyMounts,
                ToggleTarget.PreserveOnExit => Configuration.PreserveOnExit,
                ToggleTarget.EnableNfsV3 => Configuration.EnableNfsV3,
                ToggleTarget.EnableNfsV40 => Configuration.EnableNfsV40,
                ToggleTarget.EnableNfsV41 => Configuration.EnableNfsV41,
                ToggleTarget.EnableNfsV42 => Configuration.EnableNfsV42,
                _ => false,
            };

            bool value = arguments.Count > 1 ? ParseBoolean(arguments[1], name) : ReadBoolean(name, currentValue);

            switch (target)
            {
                case ToggleTarget.ReadOnly:
                    Configuration.ReadOnly = value;
                    break;
                case ToggleTarget.DenyMounts:
                    Configuration.DenyMounts = value;
                    break;
                case ToggleTarget.PreserveOnExit:
                    Configuration.PreserveOnExit = value;
                    break;
                case ToggleTarget.EnableNfsV3:
                    Configuration.EnableNfsV3 = value;
                    break;
                case ToggleTarget.EnableNfsV40:
                    Configuration.EnableNfsV40 = value;
                    break;
                case ToggleTarget.EnableNfsV41:
                    Configuration.EnableNfsV41 = value;
                    break;
                case ToggleTarget.EnableNfsV42:
                    Configuration.EnableNfsV42 = value;
                    break;
            }

            Console.WriteLine("[OK] " + name + " set to " + value + ".");
        }

        internal static void EnsureConfigurationCanChange()
        {
            if (Application is not null && Application.IsRunning)
            {
                throw new InvalidOperationException("Stop the server before changing configuration.");
            }
        }

        internal static int ReadPortAllowZero(string prompt, int currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParsePortAllowZero(value, prompt);
        }

        internal static int ParsePortAllowZero(string value, string fieldName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(fieldName, "Port values must be between 0 and 65535.");
            }

            return port;
        }

        internal static bool ReadBoolean(string prompt, bool currentValue)
        {
            string? value = ReadValue(prompt + " [on|off] (" + (currentValue ? "on" : "off") + ")");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParseBoolean(value, prompt);
        }

        internal static bool ParseBoolean(string value, string fieldName)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "y":
                case "on":
                    return true;
                case "0":
                case "false":
                case "no":
                case "n":
                case "off":
                    return false;
                default:
                    throw new ArgumentException("Expected on/off, yes/no, or true/false for " + fieldName + ".", fieldName);
            }
        }
    }
}
