namespace OpenNFS.TestServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class Program
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly byte[] Utf8Bom = Encoding.UTF8.GetPreamble();
        private static readonly ServerConfiguration Configuration = ServerConfiguration.CreateDefault();
        private static OpenNfsServerApplication? _application;
        private static bool _runForever = true;

        private static Task Main(string[] args)
        {
            return MainAsync(args, CancellationToken.None);
        }

        private static async Task MainAsync(string[] args, CancellationToken cancellationToken)
        {
            EnsureBackingStoreReady();

            Console.WriteLine("OpenNFS.TestServer");
            Console.WriteLine("Temporary backing store: " + Configuration.RootPath);
            Console.WriteLine("Type ? for help.");

            while (_runForever)
            {
                string? input = ReadLine("Command [? for help]: ");
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

            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            CleanupBackingStore();
        }

        private static async Task ExecuteCommandAsync(string input, CancellationToken cancellationToken)
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
                    SetServerName(arguments);
                    return;
                case "address":
                    SetListenerAddress(arguments);
                    return;
                case "mountport":
                    SetPort(arguments, PortTarget.Mount);
                    return;
                case "nfsport":
                    SetPort(arguments, PortTarget.NfsV3);
                    return;
                case "nfs40port":
                    SetPort(arguments, PortTarget.NfsV40);
                    return;
                case "export":
                    SetExportPath(arguments);
                    return;
                case "readonly":
                    SetToggle(arguments, ToggleTarget.ReadOnly);
                    return;
                case "denymounts":
                    SetToggle(arguments, ToggleTarget.DenyMounts);
                    return;
                case "preserve":
                    SetToggle(arguments, ToggleTarget.PreserveOnExit);
                    return;
                case "v3":
                    SetToggle(arguments, ToggleTarget.EnableNfsV3);
                    return;
                case "v40":
                    SetToggle(arguments, ToggleTarget.EnableNfsV40);
                    return;
                case "root":
                    Console.WriteLine(Configuration.RootPath);
                    return;
                case "seed":
                    EnsureBackingStoreReady();
                    Console.WriteLine("[OK] Seed content ensured.");
                    return;
                case "tree":
                    ShowBackingTree();
                    return;
                case "mkdir":
                    CreateBackingDirectory(arguments);
                    return;
                case "write":
                    WriteBackingFile(arguments);
                    return;
                case "delete":
                case "rm":
                    DeleteBackingEntry(arguments);
                    return;
                case "resetroot":
                    await ResetBackingStoreAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "start":
                    await StartAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "stop":
                    await StopAsync(cancellationToken).ConfigureAwait(false);
                    return;
                case "restart":
                    await StopAsync(cancellationToken).ConfigureAwait(false);
                    await StartAsync(cancellationToken).ConfigureAwait(false);
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
            WriteCommandLine("show", "show server configuration and runtime state");
            Console.WriteLine();
            Console.WriteLine("Listener and export configuration:");
            WriteCommandLine("server [name]", "set the server display name", Configuration.ServerName);
            WriteCommandLine("address [value]", "set the listener address", Configuration.ListenerAddress);
            WriteCommandLine("mountport [port]", "set the MOUNT v3 TCP port (0 allowed)", Configuration.MountPort.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfsport [port]", "set the NFSv3 TCP port (0 allowed)", Configuration.NfsPort.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("nfs40port [port]", "set the NFSv4.0 TCP port (0 allowed)", Configuration.Nfs40Port.ToString(CultureInfo.InvariantCulture));
            WriteCommandLine("export [path]", "set the client-visible export path", Configuration.ExportPath);
            WriteCommandLine("readonly [on|off]", "toggle read-only export mode", FormatToggle(Configuration.ReadOnly));
            WriteCommandLine("denymounts [on|off]", "toggle explicit mount denial", FormatToggle(Configuration.DenyMounts));
            WriteCommandLine("v3 [on|off]", "enable or disable the NFSv3-era listener surface", FormatToggle(Configuration.EnableNfsV3));
            WriteCommandLine("v40 [on|off]", "enable or disable the NFSv4.0 listener surface", FormatToggle(Configuration.EnableNfsV40));
            WriteCommandLine("preserve [on|off]", "preserve the temporary backing store on exit", FormatToggle(Configuration.PreserveOnExit));
            Console.WriteLine();
            Console.WriteLine("Backing store helpers:");
            WriteCommandLine("root", "show the temporary backing-store path");
            WriteCommandLine("seed", "ensure sample seed content exists");
            WriteCommandLine("tree", "show the current backing-store tree");
            WriteCommandLine("mkdir [path]", "create a backing-store directory");
            WriteCommandLine("write [path]", "write a UTF-8 backing-store file");
            WriteCommandLine("delete [path]", "delete a backing-store file or directory");
            WriteCommandLine("resetroot", "clear and reseed the temporary backing store");
            Console.WriteLine();
            Console.WriteLine("Server lifecycle:");
            WriteCommandLine("start", "start the server");
            WriteCommandLine("stop", "stop the server");
            WriteCommandLine("restart", "restart the server");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - The backing store is a temporary local directory with persistent filehandle mappings.");
            Console.WriteLine("  - Both NFSv3-era and NFSv4.0 listeners can be started from the public server package.");
            Console.WriteLine("  - This tool is meant for manual exercise and remote-client integration, not conformance claims.");
            Console.WriteLine();
        }

        private static void ShowStatus()
        {
            Console.WriteLine();
            Console.WriteLine("Server configuration:");
            Console.WriteLine("  Name           : " + Configuration.ServerName);
            Console.WriteLine("  Address        : " + Configuration.ListenerAddress);
            Console.WriteLine("  Export path    : " + Configuration.ExportPath);
            Console.WriteLine("  Backing root   : " + Configuration.RootPath);
            Console.WriteLine("  Handle map     : " + Configuration.MappingPath);
            Console.WriteLine("  Read-only      : " + Configuration.ReadOnly);
            Console.WriteLine("  Deny mounts    : " + Configuration.DenyMounts);
            Console.WriteLine("  Preserve root  : " + Configuration.PreserveOnExit);
            Console.WriteLine("  Enable NFSv3   : " + Configuration.EnableNfsV3);
            Console.WriteLine("  Enable NFSv4.0 : " + Configuration.EnableNfsV40);

            if (_application is null || !_application.IsRunning)
            {
                Console.WriteLine("  Mount port     : " + Configuration.MountPort.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv3 port     : " + Configuration.NfsPort.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  NFSv4.0 port   : " + Configuration.Nfs40Port.ToString(CultureInfo.InvariantCulture) + " (configured)");
                Console.WriteLine("  Running        : false");
            }
            else
            {
                Console.WriteLine("  Mount port     : " + _application.MountPort.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv3 port     : " + _application.NfsPort.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  NFSv4.0 port   : " + _application.Nfs40Port.ToString(CultureInfo.InvariantCulture) + " (bound)");
                Console.WriteLine("  Running        : true");
            }

            Console.WriteLine();
        }

        private static void SetServerName(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ServerName = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Server name");
            Console.WriteLine("[OK] Server name set to " + Configuration.ServerName + ".");
        }

        private static void SetListenerAddress(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ListenerAddress = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Listener address");
            Console.WriteLine("[OK] Listener address set to " + Configuration.ListenerAddress + ".");
        }

        private static void SetPort(IReadOnlyList<string> arguments, PortTarget target)
        {
            EnsureConfigurationCanChange();
            string prompt = target switch
            {
                PortTarget.Mount => "MOUNT port",
                PortTarget.NfsV3 => "NFSv3 port",
                PortTarget.NfsV40 => "NFSv4.0 port",
                _ => "Port",
            };

            int currentValue = target switch
            {
                PortTarget.Mount => Configuration.MountPort,
                PortTarget.NfsV3 => Configuration.NfsPort,
                PortTarget.NfsV40 => Configuration.Nfs40Port,
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
            }

            Console.WriteLine("[OK] " + prompt + " set to " + port.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static void SetExportPath(IReadOnlyList<string> arguments)
        {
            EnsureConfigurationCanChange();
            Configuration.ExportPath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Export path");
            Console.WriteLine("[OK] Export path set to " + Configuration.ExportPath + ".");
        }

        private static void SetToggle(IReadOnlyList<string> arguments, ToggleTarget target)
        {
            EnsureConfigurationCanChange();
            string name = target switch
            {
                ToggleTarget.ReadOnly => "Read-only",
                ToggleTarget.DenyMounts => "Deny mounts",
                ToggleTarget.PreserveOnExit => "Preserve-on-exit",
                ToggleTarget.EnableNfsV3 => "Enable NFSv3",
                ToggleTarget.EnableNfsV40 => "Enable NFSv4.0",
                _ => "Toggle",
            };

            bool currentValue = target switch
            {
                ToggleTarget.ReadOnly => Configuration.ReadOnly,
                ToggleTarget.DenyMounts => Configuration.DenyMounts,
                ToggleTarget.PreserveOnExit => Configuration.PreserveOnExit,
                ToggleTarget.EnableNfsV3 => Configuration.EnableNfsV3,
                ToggleTarget.EnableNfsV40 => Configuration.EnableNfsV40,
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
            }

            Console.WriteLine("[OK] " + name + " set to " + value + ".");
        }

        private static async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_application is not null && _application.IsRunning)
            {
                Console.WriteLine("[INFO] Server is already running.");
                return;
            }

            EnsureBackingStoreReady();

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .WithServerName(Configuration.ServerName)
                .WithListenerAddress(Configuration.ListenerAddress)
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(Configuration.MappingPath))
                .AddExport(Configuration.ExportPath, Configuration.RootPath, Configuration.ReadOnly);

            if (Configuration.DenyMounts)
            {
                builder.UseMountAuthorization(DenyAllMountAuthorization.Instance);
            }

            _application = builder.BuildApplication(
                new OpenNfsServerApplicationOptions
                {
                    EnableNfsV3 = Configuration.EnableNfsV3,
                    EnableNfs40 = Configuration.EnableNfsV40,
                    ListenerAddress = Configuration.ListenerAddress,
                    MountPort = Configuration.MountPort,
                    NfsPort = Configuration.NfsPort,
                    Nfs40Port = Configuration.Nfs40Port,
                });

            try
            {
                await _application.StartAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("[OK] Server started.");
                Console.WriteLine(
                    "READY mountPort="
                    + _application.MountPort.ToString(CultureInfo.InvariantCulture)
                    + " nfsPort="
                    + _application.NfsPort.ToString(CultureInfo.InvariantCulture)
                    + " nfs40Port="
                    + _application.Nfs40Port.ToString(CultureInfo.InvariantCulture)
                    + " exportPath="
                    + Configuration.ExportPath
                    + " rootPath="
                    + Configuration.RootPath);
            }
            catch
            {
                await _application.DisposeAsync().ConfigureAwait(false);
                _application = null;
                throw;
            }
        }

        private static async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_application is null)
            {
                return;
            }

            await _application.StopAsync(cancellationToken).ConfigureAwait(false);
            await _application.DisposeAsync().ConfigureAwait(false);
            _application = null;
            Console.WriteLine("[OK] Server stopped.");
        }

        private static async Task ResetBackingStoreAsync(CancellationToken cancellationToken)
        {
            bool wasRunning = _application is not null && _application.IsRunning;
            if (wasRunning)
            {
                await StopAsync(cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists(Configuration.RootPath))
            {
                Directory.Delete(Configuration.RootPath, recursive: true);
            }

            if (File.Exists(Configuration.MappingPath))
            {
                File.Delete(Configuration.MappingPath);
            }

            EnsureBackingStoreReady();
            Console.WriteLine("[OK] Backing store reset and reseeded.");

            if (wasRunning)
            {
                await StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static void ShowBackingTree()
        {
            EnsureBackingStoreReady();
            Console.WriteLine("Backing store tree:");
            PrintTree(Configuration.RootPath, depth: 0);
        }

        private static void CreateBackingDirectory(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing directory path");
            string fullPath = ResolveBackingPath(relativePath);
            Directory.CreateDirectory(fullPath);
            Console.WriteLine("[OK] Created " + fullPath + ".");
        }

        private static void WriteBackingFile(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing file path");
            string fullPath = ResolveBackingPath(relativePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string content = ReadMultilineBlock("Enter file contents. Finish with a single line containing only '.'");
            File.WriteAllText(fullPath, content, Utf8WithoutBom);
            Console.WriteLine("[OK] Wrote " + fullPath + ".");
        }

        private static void DeleteBackingEntry(IReadOnlyList<string> arguments)
        {
            string relativePath = arguments.Count > 1 ? arguments[1] : ReadRequiredValue("Backing file or directory path");
            string fullPath = ResolveBackingPath(relativePath);

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                Console.WriteLine("[OK] Deleted directory " + fullPath + ".");
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Console.WriteLine("[OK] Deleted file " + fullPath + ".");
                return;
            }

            throw new FileNotFoundException("The specified backing-store path does not exist.", fullPath);
        }

        private static void EnsureBackingStoreReady()
        {
            Directory.CreateDirectory(Configuration.RootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Configuration.MappingPath) ?? Configuration.RootPath);
            Directory.CreateDirectory(Path.Combine(Configuration.RootPath, "docs"));
            Directory.CreateDirectory(Path.Combine(Configuration.RootPath, "uploads"));

            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "hello.txt"), "hello from OpenNFS.TestServer\n");
            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "docs", "readme.txt"), "This is the OpenNFS.TestServer backing store.\n");
            WriteSeedFileIfMissing(Path.Combine(Configuration.RootPath, "uploads", ".keep"), string.Empty);
        }

        private static void CleanupBackingStore()
        {
            if (Configuration.PreserveOnExit)
            {
                Console.WriteLine("[INFO] Preserving backing store at " + Configuration.RootPath + ".");
                return;
            }

            try
            {
                if (Directory.Exists(Configuration.RootPath))
                {
                    Directory.Delete(Configuration.RootPath, recursive: true);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("[WARN] Failed to delete temporary backing store: " + exception.Message);
            }
        }

        private static void PrintTree(string path, int depth)
        {
            string indent = new string(' ', depth * 2);
            string displayName = depth == 0 ? path : Path.GetFileName(path);
            Console.WriteLine(indent + displayName);

            string[] directories = Directory.GetDirectories(path);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < directories.Length; index++)
            {
                PrintTree(directories[index], depth + 1);
            }

            string[] files = Directory.GetFiles(path);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < files.Length; index++)
            {
                FileInfo fileInfo = new FileInfo(files[index]);
                Console.WriteLine(
                    indent
                    + "  "
                    + fileInfo.Name
                    + " ("
                    + fileInfo.Length.ToString(CultureInfo.InvariantCulture)
                    + " bytes)");
            }
        }

        private static string ResolveBackingPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("A non-empty backing-store path is required.", nameof(inputPath));
            }

            string candidate = inputPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string combined = Path.GetFullPath(Path.Combine(Configuration.RootPath, candidate));
            string root = Path.GetFullPath(Configuration.RootPath);
            if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backing-store paths must remain beneath the temporary root.");
            }

            return combined;
        }

        private static void WriteSeedFileIfMissing(string path, string content)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, content, Utf8WithoutBom);
                return;
            }

            if (IsLegacyBomSeedFile(path, content))
            {
                File.WriteAllText(path, content, Utf8WithoutBom);
            }
        }

        private static bool IsLegacyBomSeedFile(string path, string expectedContent)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < Utf8Bom.Length || !bytes.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom))
            {
                return false;
            }

            string decoded = Encoding.UTF8.GetString(bytes, Utf8Bom.Length, bytes.Length - Utf8Bom.Length);
            return string.Equals(decoded, expectedContent, StringComparison.Ordinal);
        }

        private static void EnsureConfigurationCanChange()
        {
            if (_application is not null && _application.IsRunning)
            {
                throw new InvalidOperationException("Stop the server before changing configuration.");
            }
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

        private static int ReadPortAllowZero(string prompt, int currentValue)
        {
            string? value = ReadValue(prompt + " [" + currentValue.ToString(CultureInfo.InvariantCulture) + "]");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParsePortAllowZero(value, prompt);
        }

        private static int ParsePortAllowZero(string value, string fieldName)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) || port < 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(fieldName, "Port values must be between 0 and 65535.");
            }

            return port;
        }

        private static bool ReadBoolean(string prompt, bool currentValue)
        {
            string? value = ReadValue(prompt + " [on|off] (" + (currentValue ? "on" : "off") + ")");
            return string.IsNullOrWhiteSpace(value) ? currentValue : ParseBoolean(value, prompt);
        }

        private static bool ParseBoolean(string value, string fieldName)
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

        private static string FormatToggle(bool value)
        {
            return value ? "on" : "off";
        }

        private static void WriteCommandLine(string command, string description, string? currentValue = null)
        {
            string line = "  " + command.PadRight(30) + description;
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                line += " (current: " + currentValue + ")";
            }

            Console.WriteLine(line);
        }
    }

}
