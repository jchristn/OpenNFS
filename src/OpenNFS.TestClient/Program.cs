namespace OpenNFS.TestClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    internal static class Program
    {
        private static readonly ClientConfiguration Configuration = new ClientConfiguration();
        private static OpenNfsClient? _client;
        private static string _currentDirectory = "/";
        private static string? _mountedExportPath;
        private static bool _runForever = true;
        private static OpenNfsMountSession? _session;

        private static Task Main(string[] args)
        {
            return MainAsync(args, CancellationToken.None);
        }

        private static async Task MainAsync(string[] args, CancellationToken cancellationToken)
        {
            Console.WriteLine("OpenNFS.TestClient");
            Console.WriteLine("Type ? for help.");

            while (_runForever)
            {
                string prompt = _session is null
                    ? "Command [? for help]: "
                    : "Command " + _currentDirectory + " [? for help]: ";
                string? input = ReadLine(prompt);
                if (input is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                try
                {
                    await ExecuteCommandAsync(input, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[WARN] Operation canceled.");
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[ERROR] " + exception.Message);
                }
            }

            await DisconnectAsync(unmountIfNeeded: true, CancellationToken.None).ConfigureAwait(false);
        }

        internal static async Task ExecuteCommandAsync(string input, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> arguments = Tokenize(input);
            if (arguments.Count == 0)
            {
                return;
            }

            string command = arguments[0].ToLowerInvariant();

            switch (command)
            {
                case "?":
                case "help":
                    ShowMenu();
                    return;
                case "q":
                case "quit":
                case "exit":
                    _runForever = false;
                    return;
                case "cls":
                case "clear":
                    Console.Clear();
                    return;
                case "show":
                case "status":
                    ShowStatus();
                    return;
                case "server":
                    SetServer(arguments);
                    return;
                case "mountendpoint":
                    SetMountEndpoint(arguments);
                    return;
                case "transport":
                    SetTransport(arguments);
                    return;
                case "auth":
                    SetAuthenticationFlavor(arguments);
                    return;
                case "authsys":
                    ConfigureAuthSys(arguments);
                    return;
                case "connect":
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "disconnect":
                    await DisconnectAsync(unmountIfNeeded: true, cancellationToken).ConfigureAwait(false);
                    return;
                case "exports":
                    await ListExportsAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "mounts":
                    await ListMountsAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "mount":
                    await MountAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "umount":
                case "unmount":
                    await UnmountAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "pwd":
                    Console.WriteLine(_currentDirectory);
                    return;
                case "cd":
                    await ChangeDirectoryAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "ls":
                case "dir":
                    await ListDirectoryAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "stat":
                    await ShowMetadataAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "cat":
                case "read":
                    await ReadFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "write":
                    await WriteTextFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "touch":
                    await CreateFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "mkdir":
                    await CreateDirectoryAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "rm":
                case "delete":
                    await DeleteFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "rmdir":
                    await DeleteDirectoryAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "put":
                case "upload":
                    await UploadFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                case "get":
                case "download":
                    await DownloadFileAsync(arguments, cancellationToken).ConfigureAwait(false);
                    return;
                default:
                    Console.WriteLine("[WARN] Unknown command '" + arguments[0] + "'. Type ? for help.");
                    return;
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            WriteCommandLine("? / help", "show this menu");
            WriteCommandLine("q / quit / exit", "quit");
            WriteCommandLine("cls / clear", "clear the screen");
            WriteCommandLine("show", "show current client configuration and mount state");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            WriteCommandLine("server [host] [port]", "set the primary NFS endpoint", Configuration.GetPrimaryEndpointDisplay());
            WriteCommandLine("mountendpoint [host] [port]", "set the dedicated MOUNT v3 endpoint", Configuration.GetMountEndpointDisplay());
            WriteCommandLine("mountendpoint clear", "clear the dedicated MOUNT endpoint");
            WriteCommandLine("transport [tcp|tcpudp]", "set the transport policy", Configuration.GetTransportDisplay());
            WriteCommandLine("auth [none|authsys|rpcsecgss]", "set the authentication flavor", Configuration.GetAuthenticationFlavorDisplay());
            WriteCommandLine("authsys [machine] [uid] [gid] [groups]", "set AUTH_SYS identity values", Configuration.GetAuthSysDisplay());
            Console.WriteLine();
            Console.WriteLine("Connection and export bootstrap:");
            WriteCommandLine("connect", "build and open the client");
            WriteCommandLine("disconnect", "close and dispose the client");
            WriteCommandLine("exports", "list available exports through MOUNT v3");
            WriteCommandLine("mounts", "list current server mount records through MOUNT v3");
            WriteCommandLine("mount [exportPath]", "mount an export and open a path-first session", Configuration.PreferredExportPath);
            WriteCommandLine("umount", "unmount the current export and close the session");
            Console.WriteLine();
            Console.WriteLine("Mounted-session operations:");
            WriteCommandLine("pwd", "show the current export-relative directory");
            WriteCommandLine("cd [path]", "change the current export-relative directory");
            WriteCommandLine("ls [path]", "list directory contents");
            WriteCommandLine("stat [path]", "show file or directory metadata");
            WriteCommandLine("cat [path]", "read and print a UTF-8 file");
            WriteCommandLine("write [path]", "overwrite a file with prompted UTF-8 text");
            WriteCommandLine("touch [path]", "create a file if it does not exist");
            WriteCommandLine("mkdir [path]", "create a directory");
            WriteCommandLine("rm [path]", "delete a file");
            WriteCommandLine("rmdir [path]", "delete an empty directory");
            WriteCommandLine("put [local] [remote]", "upload a local file");
            WriteCommandLine("get [remote] [local]", "download a remote file");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - The current test client exercises the best-covered NFSv3 mounted-session path.");
            Console.WriteLine("  - AUTH_SYS lets you configure machine, uid, gid, and supplementary gids.");
            Console.WriteLine("  - RPCSEC_GSS remains unimplemented in the current public client surface.");
            Console.WriteLine();
        }

        private static void ShowStatus()
        {
            Console.WriteLine();
            Console.WriteLine("Client configuration:");
            Console.WriteLine("  Primary endpoint : " + Configuration.GetPrimaryEndpointDisplay());
            Console.WriteLine("  MOUNT endpoint   : " + Configuration.GetMountEndpointDisplay());

            Console.WriteLine("  Transport        : " + Configuration.TransportPolicy);
            Console.WriteLine("  Auth flavor      : " + Configuration.AuthenticationFlavor);

            if (Configuration.AuthenticationFlavor == OpenNfsAuthenticationFlavor.AuthSys)
            {
                Console.WriteLine("  AUTH_SYS machine : " + Configuration.AuthSysMachineName);
                Console.WriteLine("  AUTH_SYS uid/gid : "
                    + Configuration.AuthSysUserId.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + Configuration.AuthSysGroupId.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("  AUTH_SYS groups  : " + FormatUIntList(Configuration.AuthSysSupplementaryGroupIds));
            }

            Console.WriteLine("  Connected        : " + (_client is not null ? _client.State.ToString() : "No"));
            Console.WriteLine("  Mounted export   : " + (_mountedExportPath ?? "(none)"));
            Console.WriteLine("  Current path     : " + _currentDirectory);
            Console.WriteLine();
        }

        private static void SetServer(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            string host = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Server host");
            int port = arguments.Count > 2 ? ParsePort(arguments[2], "server port") : ReadPort("Server port", Configuration.ServerPort);
            Configuration.ServerHost = host;
            Configuration.ServerPort = port;
            Console.WriteLine("[OK] Primary server endpoint set to " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        private static void SetMountEndpoint(IReadOnlyList<string> arguments)
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

        private static void SetTransport(IReadOnlyList<string> arguments)
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

        private static void SetAuthenticationFlavor(IReadOnlyList<string> arguments)
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

        private static void ConfigureAuthSys(IReadOnlyList<string> arguments)
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

        private static async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_client is not null && _client.State == OpenNfsClientState.Open)
            {
                Console.WriteLine("[INFO] Client is already connected.");
                return;
            }

            await DisconnectAsync(unmountIfNeeded: false, CancellationToken.None).ConfigureAwait(false);

            _client = BuildClient();
            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Client connected.");
        }

        private static async Task DisconnectAsync(bool unmountIfNeeded, CancellationToken cancellationToken)
        {
            if (unmountIfNeeded && _session is not null && _client is not null && !string.IsNullOrWhiteSpace(_mountedExportPath))
            {
                try
                {
                    await _client.Exports.UnmountV3Async(_mountedExportPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[WARN] Unmount cleanup failed: " + exception.Message);
                }
            }

            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
                _session = null;
                _mountedExportPath = null;
                _currentDirectory = "/";
            }

            if (_client is not null)
            {
                try
                {
                    if (_client.State == OpenNfsClientState.Open)
                    {
                        await _client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await _client.DisposeAsync().ConfigureAwait(false);
                    _client = null;
                }
            }
        }

        private static async Task ListExportsAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
            if (exports.Count == 0)
            {
                Console.WriteLine("[INFO] No exports were returned.");
                return;
            }

            Console.WriteLine("Exports:");
            for (int index = 0; index < exports.Count; index++)
            {
                OpenNfsExportV3Entry export = exports[index];
                Console.WriteLine("  " + export.ExportPath);
                if (export.AuthorizedClientGroups.Count > 0)
                {
                    Console.WriteLine("    groups: " + string.Join(", ", export.AuthorizedClientGroups));
                }
            }
        }

        private static async Task ListMountsAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            IReadOnlyList<OpenNfsMountedExportV3Entry> mounts = await client.Exports.ListMountsV3Async(cancellationToken).ConfigureAwait(false);
            if (mounts.Count == 0)
            {
                Console.WriteLine("[INFO] No mounted exports were returned.");
                return;
            }

            Console.WriteLine("Mounted exports:");
            for (int index = 0; index < mounts.Count; index++)
            {
                OpenNfsMountedExportV3Entry mount = mounts[index];
                Console.WriteLine("  " + mount.HostName + " -> " + mount.ExportPath);
            }
        }

        private static async Task MountAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            if (_session is not null)
            {
                throw new InvalidOperationException("An export is already mounted. Use umount before mounting another export.");
            }

            string exportPath = arguments.Count > 1 ? arguments[1] : Configuration.PreferredExportPath;
            _session = await client.MountAsync(exportPath, cancellationToken).ConfigureAwait(false);
            _mountedExportPath = _session.ExportPath;
            Configuration.PreferredExportPath = _session.ExportPath;
            _currentDirectory = "/";
            Console.WriteLine("[OK] Mounted " + _mountedExportPath + ".");
        }

        private static async Task UnmountAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = RequireConnectedClient();
            if (_session is null || string.IsNullOrWhiteSpace(_mountedExportPath))
            {
                throw new InvalidOperationException("No export is currently mounted.");
            }

            await client.Exports.UnmountV3Async(_mountedExportPath, cancellationToken).ConfigureAwait(false);
            await _session.DisposeAsync().ConfigureAwait(false);
            Console.WriteLine("[OK] Unmounted " + _mountedExportPath + ".");
            _session = null;
            _mountedExportPath = null;
            _currentDirectory = "/";
        }

        private static async Task ChangeDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string inputPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Directory path");
            string resolvedPath = ResolveRemotePath(inputPath, allowCurrentDirectory: true);
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            if (attributes.FileType != OpenNfsV3FileType.Directory)
            {
                throw new InvalidOperationException("Path '" + resolvedPath + "' is not a directory.");
            }

            _currentDirectory = resolvedPath;
            Console.WriteLine("[OK] Current directory is now " + _currentDirectory + ".");
        }

        private static async Task ListDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: true) : _currentDirectory;
            IReadOnlyList<OpenNfsV3DirectoryEntry> entries = await session.Directories.ListAsync(targetPath, cancellationToken).ConfigureAwait(false);

            if (entries.Count == 0)
            {
                Console.WriteLine("[INFO] Directory is empty.");
                return;
            }

            Console.WriteLine("Listing " + targetPath + ":");
            for (int index = 0; index < entries.Count; index++)
            {
                OpenNfsV3DirectoryEntry entry = entries[index];
                string childPath = CombineRemotePath(targetPath, entry.Name);
                string detail = string.Empty;

                try
                {
                    OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(childPath, cancellationToken).ConfigureAwait(false);
                    detail = " [" + attributes.FileType + "] " + attributes.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes";
                }
                catch (Exception exception)
                {
                    detail = " [unknown] " + exception.Message;
                }

                Console.WriteLine("  " + entry.Name + detail);
            }
        }

        private static async Task ShowMetadataAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: true) : _currentDirectory;
            OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(targetPath, cancellationToken).ConfigureAwait(false);

            Console.WriteLine("Metadata for " + targetPath + ":");
            Console.WriteLine("  Type        : " + attributes.FileType);
            Console.WriteLine("  Mode        : 0" + Convert.ToString(attributes.Mode, 8));
            Console.WriteLine("  Links       : " + attributes.LinkCount.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Uid/Gid     : " + attributes.UserId.ToString(CultureInfo.InvariantCulture) + "/" + attributes.GroupId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  Size        : " + attributes.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes");
            Console.WriteLine("  Used        : " + attributes.UsedBytes.ToString(CultureInfo.InvariantCulture) + " bytes");
            Console.WriteLine("  FileSystemId: " + attributes.FileSystemId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  FileId      : " + attributes.FileId.ToString(CultureInfo.InvariantCulture));
            Console.WriteLine("  AccessTime  : " + FormatNfsTime(attributes.AccessTime));
            Console.WriteLine("  ModifyTime  : " + FormatNfsTime(attributes.ModifyTime));
            Console.WriteLine("  ChangeTime  : " + FormatNfsTime(attributes.ChangeTime));
        }

        private static async Task ReadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            byte[] bytes = await session.Files.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Read " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) from " + targetPath + ".");
            Console.WriteLine();
            Console.WriteLine(Encoding.UTF8.GetString(bytes));
        }

        private static async Task WriteTextFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            string content = ReadMultilineBlock("Enter file contents. Finish with a single line containing only '.'");
            await EnsureRemoteFileExistsAsync(session, targetPath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(targetPath, Encoding.UTF8.GetBytes(content), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Wrote " + targetPath + ".");
        }

        private static async Task CreateFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            await session.Directories.CreateFileAsync(targetPath, failIfExists: false, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Created file " + targetPath + ".");
        }

        private static async Task CreateDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote directory path");
            await session.Directories.CreateDirectoryAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Created directory " + targetPath + ".");
        }

        private static async Task DeleteFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            await session.Directories.DeleteFileAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Deleted file " + targetPath + ".");
        }

        private static async Task DeleteDirectoryAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string targetPath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote directory path");
            await session.Directories.DeleteDirectoryAsync(targetPath, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Deleted directory " + targetPath + ".");
        }

        private static async Task UploadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string localPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Local file path");
            string remotePath = arguments.Count > 2 ? ResolveRemotePath(arguments[2], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            byte[] bytes = File.ReadAllBytes(localPath);
            await EnsureRemoteFileExistsAsync(session, remotePath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(remotePath, bytes, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[OK] Uploaded " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) to " + remotePath + ".");
        }

        private static async Task DownloadFileAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            OpenNfsMountSession session = RequireMountedSession();
            string remotePath = arguments.Count > 1 ? ResolveRemotePath(arguments[1], allowCurrentDirectory: false) : ReadRequiredRemotePath("Remote file path");
            string localPath = arguments.Count > 2 ? arguments[2] : ReadRequiredValue("Local destination path");
            byte[] bytes = await session.Files.ReadAllBytesAsync(remotePath, cancellationToken).ConfigureAwait(false);
            string? localDirectory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(localDirectory))
            {
                Directory.CreateDirectory(localDirectory);
            }

            File.WriteAllBytes(localPath, bytes);
            Console.WriteLine("[OK] Downloaded " + bytes.Length.ToString(CultureInfo.InvariantCulture) + " byte(s) to " + localPath + ".");
        }

        private static async Task EnsureRemoteFileExistsAsync(OpenNfsMountSession session, string remotePath, CancellationToken cancellationToken)
        {
            try
            {
                _ = await session.Metadata.GetAttributesAsync(remotePath, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("NoEnt", StringComparison.Ordinal))
            {
                await session.Directories.CreateFileAsync(remotePath, failIfExists: false, cancellationToken).ConfigureAwait(false);
            }
        }

        private static OpenNfsClient BuildClient()
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

        private static OpenNfsClient RequireConnectedClient()
        {
            if (_client is null || _client.State != OpenNfsClientState.Open)
            {
                throw new InvalidOperationException("The client must be connected before this command can run.");
            }

            return _client;
        }

        private static OpenNfsMountSession RequireMountedSession()
        {
            if (_session is null)
            {
                throw new InvalidOperationException("An export must be mounted before this command can run.");
            }

            return _session;
        }

        private static void EnsureConfigurationCanChange()
        {
            if (_client is not null && _client.State == OpenNfsClientState.Open)
            {
                throw new InvalidOperationException("Disconnect the client before changing connection or authentication settings.");
            }
        }

        private static string ResolveRemotePath(string inputPath, bool allowCurrentDirectory)
        {
            string candidate = string.IsNullOrWhiteSpace(inputPath)
                ? (allowCurrentDirectory ? _currentDirectory : throw new ArgumentException("A non-empty remote path is required.", nameof(inputPath)))
                : inputPath.Trim().Replace('\\', '/');

            bool absolute = candidate.StartsWith("/", StringComparison.Ordinal);
            List<string> segments = absolute
                ? new List<string>()
                : SplitSegments(_currentDirectory);

            foreach (string segment in candidate.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    continue;
                }

                segments.Add(segment);
            }

            return segments.Count == 0 ? "/" : "/" + string.Join('/', segments);
        }

        private static string ReadRequiredRemotePath(string prompt)
        {
            return ResolveRemotePath(ReadRequiredValue(prompt), allowCurrentDirectory: false);
        }

        private static string CombineRemotePath(string basePath, string name)
        {
            if (string.Equals(basePath, "/", StringComparison.Ordinal))
            {
                return "/" + name;
            }

            return basePath.TrimEnd('/') + "/" + name;
        }

        private static List<string> SplitSegments(string path)
        {
            return path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static string ReadMultilineBlock(string prompt)
        {
            Console.WriteLine(prompt);
            StringBuilder builder = new StringBuilder();

            while (true)
            {
                string? line = Console.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (string.Equals(line, ".", StringComparison.Ordinal))
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        private static IReadOnlyList<string> Tokenize(string input)
        {
            input = input.TrimStart('\uFEFF');
            List<string> tokens = new List<string>();
            StringBuilder builder = new StringBuilder();
            bool inQuotes = false;

            for (int index = 0; index < input.Length; index++)
            {
                char current = input[index];
                if (current == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(current) && !inQuotes)
                {
                    if (builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                    }

                    continue;
                }

                builder.Append(current);
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
            }

            return tokens;
        }

        private static string? ReadLine(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        private static string ReadRequiredValue(string prompt)
        {
            string? value = ReadValue(prompt);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("A non-empty value is required.");
            }

            return value;
        }

        private static string? ReadValue(string prompt)
        {
            Console.Write(prompt + ": ");
            return Console.ReadLine();
        }

        private static int ReadPort(string prompt, int currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParsePort(value, prompt);
        }

        private static int ParsePort(string value, string fieldName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(fieldName, "Port values must be between 1 and 65535.");
            }

            return port;
        }

        private static uint ReadUInt32(string prompt, uint currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParseUInt32(value, prompt);
        }

        private static uint ParseUInt32(string value, string fieldName)
        {
            if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
            {
                throw new ArgumentOutOfRangeException(fieldName, "Expected an unsigned 32-bit integer value.");
            }

            return parsed;
        }

        private static IReadOnlyList<uint> ParseUInt32List(string value, string fieldName)
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

        private static string FormatUIntList(IReadOnlyList<uint> values)
        {
            if (values.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", values.Select(static value => value.ToString(CultureInfo.InvariantCulture)));
        }

        private static string FormatNfsTime(OpenNfsV3Time value)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(value.Seconds)
                .AddTicks(value.Nanoseconds / 100U);
            return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
        }

        private static void WriteCommandLine(string command, string description, string? currentValue = null)
        {
            string line = "  " + command.PadRight(40) + description;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                line += " (current: " + currentValue + ")";
            }

            Console.WriteLine(line);
        }
    }

}
