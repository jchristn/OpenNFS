namespace Sample.OpenNfsServer
{
    using System;
    using System.IO;
    using System.Text.Json;

    internal sealed class SampleServerConfiguration
    {
        private SampleServerConfiguration(
            bool showHelp,
            string serverName,
            string exportPath,
            string sourcePath,
            string owner,
            string ownerGroup,
            string listenerAddress,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            string mappingPath,
            bool denyMounts)
        {
            ShowHelp = showHelp;
            ServerName = serverName;
            ExportPath = exportPath;
            SourcePath = sourcePath;
            Owner = owner;
            OwnerGroup = ownerGroup;
            ListenerAddress = listenerAddress;
            MountPort = mountPort;
            NfsPort = nfsPort;
            Nfs40Port = nfs40Port;
            MappingPath = mappingPath;
            DenyMounts = denyMounts;
        }

        internal bool DenyMounts { get; }

        internal string ExportPath { get; }

        internal string ListenerAddress { get; }

        internal string MappingPath { get; }

        internal int MountPort { get; }

        internal int NfsPort { get; }

        internal int Nfs40Port { get; }

        internal string Owner { get; }

        internal string OwnerGroup { get; }

        internal string ServerName { get; }

        internal bool ShowHelp { get; }

        internal string SourcePath { get; }

        internal static SampleServerConfiguration Parse(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            string defaultSourcePath = Path.Combine(Path.GetTempPath(), "OpenNFS.Sample", "Export");
            bool showHelp = false;
            bool denyMounts = false;
            string serverName = "OpenNFS Sample";
            string exportPath = "/exports/sample";
            string sourcePath = defaultSourcePath;
            string owner = "sample-owner@example.test";
            string ownerGroup = "sample-group@example.test";
            string listenerAddress = "0.0.0.0";
            int mountPort = 20048;
            int nfsPort = 2049;
            int nfs40Port = 3049;
            string? mappingPath = null;
            string? configPath = null;

            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--config", StringComparison.Ordinal))
                {
                    configPath = ReadRequiredValue(args, ref index, "--config");
                }
            }

            if (!string.IsNullOrWhiteSpace(configPath))
            {
                string normalizedConfigPath = Path.GetFullPath(configPath);
                string configDirectory = Path.GetDirectoryName(normalizedConfigPath) ?? Environment.CurrentDirectory;
                SampleServerConfigurationFile fileConfiguration = LoadFileConfiguration(normalizedConfigPath);

                serverName = ReadConfiguredText(fileConfiguration.ServerName, serverName, nameof(fileConfiguration.ServerName));
                exportPath = ReadConfiguredText(fileConfiguration.ExportPath, exportPath, nameof(fileConfiguration.ExportPath));
                sourcePath = ResolveConfiguredPath(fileConfiguration.SourcePath, configDirectory) ?? sourcePath;
                owner = ReadConfiguredText(fileConfiguration.Owner, owner, nameof(fileConfiguration.Owner));
                ownerGroup = ReadConfiguredText(fileConfiguration.OwnerGroup, ownerGroup, nameof(fileConfiguration.OwnerGroup));
                listenerAddress = ReadConfiguredText(fileConfiguration.ListenerAddress, listenerAddress, nameof(fileConfiguration.ListenerAddress));
                mountPort = ReadConfiguredPort(fileConfiguration.MountPort, mountPort, "mountPort");
                nfsPort = ReadConfiguredPort(fileConfiguration.NfsPort, nfsPort, "nfsPort");
                nfs40Port = ReadConfiguredPort(fileConfiguration.Nfs40Port, nfs40Port, "nfs40Port");
                mappingPath = ResolveConfiguredPath(fileConfiguration.MappingPath, configDirectory) ?? mappingPath;
                denyMounts = fileConfiguration.DenyMounts ?? denyMounts;
            }

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        showHelp = true;
                        break;

                    case "--deny-mounts":
                        denyMounts = true;
                        break;

                    case "--server-name":
                        serverName = ReadRequiredValue(args, ref index, "--server-name");
                        break;

                    case "--config":
                        _ = ReadRequiredValue(args, ref index, "--config");
                        break;

                    case "--export-path":
                        exportPath = ReadRequiredValue(args, ref index, "--export-path");
                        break;

                    case "--source-path":
                        sourcePath = ReadRequiredValue(args, ref index, "--source-path");
                        break;

                    case "--owner":
                        owner = ReadRequiredValue(args, ref index, "--owner");
                        break;

                    case "--owner-group":
                        ownerGroup = ReadRequiredValue(args, ref index, "--owner-group");
                        break;

                    case "--listener-address":
                        listenerAddress = ReadRequiredValue(args, ref index, "--listener-address");
                        break;

                    case "--mount-port":
                        mountPort = ParsePort(ReadRequiredValue(args, ref index, "--mount-port"), "--mount-port");
                        break;

                    case "--nfs-port":
                        nfsPort = ParsePort(ReadRequiredValue(args, ref index, "--nfs-port"), "--nfs-port");
                        break;

                    case "--nfs40-port":
                        nfs40Port = ParsePort(ReadRequiredValue(args, ref index, "--nfs40-port"), "--nfs40-port");
                        break;

                    case "--mapping-path":
                        mappingPath = ReadRequiredValue(args, ref index, "--mapping-path");
                        break;

                    default:
                        throw new ArgumentException("Unknown Sample.OpenNfsServer argument '" + args[index] + "'.");
                }
            }

            string normalizedSourcePath = Path.GetFullPath(sourcePath);
            string normalizedMappingPath = Path.GetFullPath(
                mappingPath
                ?? (normalizedSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".handles.json"));

            return new SampleServerConfiguration(
                showHelp,
                serverName,
                exportPath,
                normalizedSourcePath,
                owner,
                ownerGroup,
                listenerAddress,
                mountPort,
                nfsPort,
                nfs40Port,
                normalizedMappingPath,
                denyMounts);
        }

        internal static string GetUsage()
        {
            return string.Join(
                Environment.NewLine,
                "Sample.OpenNfsServer options:",
                "  --config <path>            Optional JSON config file. Relative paths resolve from the config file directory.",
                "  --server-name <name>       Friendly server name. Default: OpenNFS Sample",
                "  --export-path <path>       Export path to advertise. Default: /exports/sample",
                "  --source-path <path>       Host source directory to export. Default: %TEMP%\\OpenNFS.Sample\\Export",
                "  --owner <value>            Advertised owner identity for NFSv4 owner attributes. Default: sample-owner@example.test",
                "  --owner-group <value>      Advertised owner-group identity for NFSv4 owner_group attributes. Default: sample-group@example.test",
                "  --listener-address <addr>  Listener bind address. Default: 0.0.0.0",
                "  --mount-port <port>        MOUNT v3 TCP port. Use 0 for an ephemeral port. Default: 20048",
                "  --nfs-port <port>          NFSv3 TCP port. Use 0 for an ephemeral port. Default: 2049",
                "  --nfs40-port <port>        NFSv4.0 TCP port. Use 0 for an ephemeral port. Default: 3049",
                "  --mapping-path <path>      Persistent filehandle mapping path. Default: <source-path>.handles.json",
                "  --deny-mounts              Deny all mount requests while still starting the sample host.",
                "  --help                     Show this help text.");
        }

        private static SampleServerConfigurationFile LoadFileConfiguration(string configPath)
        {
            try
            {
                using FileStream stream = File.OpenRead(configPath);
                SampleServerConfigurationFile? configuration = JsonSerializer.Deserialize<SampleServerConfigurationFile>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });

                return configuration ?? new SampleServerConfigurationFile();
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "The sample configuration file '" + configPath + "' is not valid JSON.",
                    exception);
            }
        }

        private static int ParsePort(string value, string argumentName)
        {
            if (!int.TryParse(value, out int parsedPort) || parsedPort < 0 || parsedPort > 65535)
            {
                throw new ArgumentOutOfRangeException(argumentName, value, "The port value must be between 0 and 65535.");
            }

            return parsedPort;
        }

        private static string ReadRequiredValue(string[] args, ref int index, string argumentName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("The argument '" + argumentName + "' requires a following value.");
            }

            string value = args[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The argument '" + argumentName + "' must contain a non-empty value.");
            }

            return value;
        }

        private static int ReadConfiguredPort(int? configuredPort, int fallbackPort, string fieldName)
        {
            if (!configuredPort.HasValue)
            {
                return fallbackPort;
            }

            int port = configuredPort.Value;
            if (port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(fieldName, port, "The configured port value must be between 0 and 65535.");
            }

            return port;
        }

        private static string ReadConfiguredText(string? configuredValue, string fallbackValue, string fieldName)
        {
            if (configuredValue is null)
            {
                return fallbackValue;
            }

            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                throw new ArgumentException("The configured field '" + fieldName + "' must contain a non-empty value.", fieldName);
            }

            return configuredValue;
        }

        private static string? ResolveConfiguredPath(string? configuredPath, string configDirectory)
        {
            if (configuredPath is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new ArgumentException("Configured path values must contain a non-empty value.", nameof(configuredPath));
            }

            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(configDirectory, configuredPath));
        }
    }
}
