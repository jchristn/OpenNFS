namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Mounted-session and builder execution flows for the public client surface suites.
    /// </summary>
    internal static class ClientSurfaceMountedSessionSupport
    {
        internal static async Task ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync(CancellationToken cancellationToken)
        {
            const string expectedMachineName = "opennfs-test-client";
            OpenNfsClientSettings settings = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", 20048)
                .WithAuthSysCredentials(expectedMachineName, 1001, 1002, new uint[] { 1003, 1004 })
                .BuildSettings();

            if (settings.AuthenticationFlavor != OpenNfsAuthenticationFlavor.AuthSys
                || !string.Equals(settings.AuthSysCredentials.MachineName, expectedMachineName, StringComparison.Ordinal)
                || settings.AuthSysCredentials.UserId != 1001
                || settings.AuthSysCredentials.GroupId != 1002
                || settings.AuthSysCredentials.SupplementaryGroupIds.Count != 2
                || settings.AuthSysCredentials.SupplementaryGroupIds[0] != 1003
                || settings.AuthSysCredentials.SupplementaryGroupIds[1] != 1004)
            {
                throw new InvalidOperationException("Expected the client builder to preserve configured AUTH_SYS identity values.");
            }

            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientAuthSys", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourceRoot);

                Dictionary<string, NfsPathKind> pathKinds = new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [sourceRoot] = NfsPathKind.Directory,
                };

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseFileSystem(new DictionaryNfsFileSystem(pathKinds, new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)))
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.MountPort)
                    .WithAuthSysCredentials(expectedMachineName, 1001, 1002, new uint[] { 1003, 1004 })
                    .Build();

                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsMountV3Result mountResult = await client.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the AUTH_SYS credential test to mount the export successfully.");
                }

                IReadOnlyList<OpenNfsMountedExportV3Entry> mountedExports = await client.Exports.ListMountsV3Async(cancellationToken).ConfigureAwait(false);
                OpenNfsMountedExportV3Entry? mountedExport = mountedExports.SingleOrDefault(
                    static entry => string.Equals(entry.ExportPath, "/export", StringComparison.Ordinal));

                if (mountedExport is null)
                {
                    throw new InvalidOperationException("Expected MOUNT v3 DUMP to include the mounted export entry.");
                }

                if (!string.Equals(mountedExport.HostName, expectedMachineName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected MOUNT v3 DUMP to surface the configured AUTH_SYS machine name '"
                        + expectedMachineName
                        + "', but found '"
                        + mountedExport.HostName
                        + "'.");
                }
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        internal static async Task ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientSurface", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourceRoot);
                Directory.CreateDirectory(Path.Combine(sourceRoot, "docs"));

                Dictionary<string, NfsPathKind> pathKinds = new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [sourceRoot] = NfsPathKind.Directory,
                    [Path.Combine(sourceRoot, "docs")] = NfsPathKind.Directory,
                    [Path.Combine(sourceRoot, "docs", "readme.txt")] = NfsPathKind.File,
                    [Path.Combine(sourceRoot, "hello.txt")] = NfsPathKind.File,
                };

                Dictionary<string, byte[]> fileContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [Path.Combine(sourceRoot, "docs", "readme.txt")] = Encoding.UTF8.GetBytes("session-readme"),
                    [Path.Combine(sourceRoot, "hello.txt")] = Encoding.UTF8.GetBytes("session-hello"),
                };

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseFileSystem(new DictionaryNfsFileSystem(pathKinds, fileContents))
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.MountPort)
                    .Build();
                await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected mounted-session setup to obtain a successful MOUNT v3 root handle.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsMountSession session = nfsClient.CreateMountSession("/export", mountResult);

                IReadOnlyList<OpenNfsV3DirectoryEntry> rootEntries = await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
                string[] rootNames = rootEntries.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
                if (!rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
                {
                    throw new InvalidOperationException("Expected the mounted-session root listing to contain 'docs' and 'hello.txt'.");
                }

                OpenNfsV3Attributes readmeAttributes =
                    await session.Metadata.GetAttributesAsync("/docs/readme.txt", cancellationToken).ConfigureAwait(false);
                if (readmeAttributes.SizeBytes != 14UL)
                {
                    throw new InvalidOperationException("Expected mounted-session metadata reads to surface the current file size.");
                }

                byte[] initialBytes = await session.Files.ReadAllBytesAsync("/docs/readme.txt", cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Encoding.UTF8.GetString(initialBytes), "session-readme", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected mounted-session file reads to return the seeded payload.");
                }

                await session.Directories.CreateFileAsync("/notes.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
                await session.Files.WriteAllBytesAsync(
                    "/notes.txt",
                    Encoding.UTF8.GetBytes("written-through-session"),
                    OpenNfsWriteStability.FileSync,
                    cancellationToken).ConfigureAwait(false);

                byte[] writtenBytes = await session.Files.ReadAllBytesAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Encoding.UTF8.GetString(writtenBytes), "written-through-session", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected mounted-session file writes to round-trip through the live NFSv3 server path.");
                }

                await session.Directories.DeleteFileAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
                IReadOnlyList<OpenNfsV3DirectoryEntry> updatedRootEntries = await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
                if (updatedRootEntries.Any(static entry => string.Equals(entry.Name, "notes.txt", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Expected mounted-session file deletion to remove the created entry.");
                }
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        internal static async Task ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientSessionConsistency", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(Path.Combine(sourceRoot, "docs"));

                Dictionary<string, NfsPathKind> pathKinds = new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [sourceRoot] = NfsPathKind.Directory,
                    [Path.Combine(sourceRoot, "docs")] = NfsPathKind.Directory,
                    [Path.Combine(sourceRoot, "docs", "seed.txt")] = NfsPathKind.File,
                };

                Dictionary<string, byte[]> fileContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [Path.Combine(sourceRoot, "docs", "seed.txt")] = Encoding.UTF8.GetBytes("seed-value"),
                };

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseFileSystem(new SerializedNfsFileSystem(new DictionaryNfsFileSystem(pathKinds, fileContents)))
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                await using MountedSessionContext mountedSession = await CreateMountedSessionAsync(host, cancellationToken).ConfigureAwait(false);
                OpenNfsMountSession session = mountedSession.Session;

                await session.Directories.CreateFileAsync("/docs/live.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
                await session.Files.WriteAllBytesAsync(
                    "/docs/live.txt",
                    Encoding.UTF8.GetBytes("session-live"),
                    OpenNfsWriteStability.FileSync,
                    cancellationToken).ConfigureAwait(false);

                byte[] currentBytes = await session.Files.ReadAllBytesAsync("/docs/live.txt", cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Encoding.UTF8.GetString(currentBytes), "session-live", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected a mounted session to observe its own newly written file contents without reopening the export.");
                }

                IReadOnlyList<OpenNfsV3DirectoryEntry> entriesAfterCreate = await session.Directories.ListAsync("/docs", cancellationToken).ConfigureAwait(false);
                if (!entriesAfterCreate.Any(static entry => string.Equals(entry.Name, "live.txt", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Expected a mounted session to observe its own newly created directory entry.");
                }

                await session.Directories.DeleteFileAsync("/docs/live.txt", cancellationToken).ConfigureAwait(false);
                IReadOnlyList<OpenNfsV3DirectoryEntry> entriesAfterDelete = await session.Directories.ListAsync("/docs", cancellationToken).ConfigureAwait(false);
                if (entriesAfterDelete.Any(static entry => string.Equals(entry.Name, "live.txt", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Expected a mounted session to stop surfacing a deleted entry immediately after the same-session delete.");
                }

                try
                {
                    _ = await session.Files.ReadAllBytesAsync("/docs/live.txt", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected a deleted mounted-session path read to fail with a clear NFSv3 status.");
                }
                catch (OpenNfsV3StatusException exception)
                {
                    if (exception.Status != OpenNfsV3Status.NoEntry
                        || exception.Category != OpenNfsErrorCategory.NotFound
                        || !exception.Message.Contains("NoEntry", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected the deleted mounted-session path read to preserve a typed NFSv3 NoEntry failure.");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        internal static async Task ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientSessionConcurrent", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(Path.Combine(sourceRoot, "docs"));

                Dictionary<string, NfsPathKind> pathKinds = new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [sourceRoot] = NfsPathKind.Directory,
                    [Path.Combine(sourceRoot, "docs")] = NfsPathKind.Directory,
                };
                Dictionary<string, byte[]> fileContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

                for (int index = 0; index < 8; index++)
                {
                    string filePath = Path.Combine(sourceRoot, "docs", "file-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt");
                    pathKinds[filePath] = NfsPathKind.File;
                    fileContents[filePath] = Encoding.UTF8.GetBytes("payload-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseFileSystem(new SerializedNfsFileSystem(new DictionaryNfsFileSystem(pathKinds, fileContents)))
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                await using MountedSessionContext mountedSession = await CreateMountedSessionAsync(host, cancellationToken).ConfigureAwait(false);
                OpenNfsMountSession session = mountedSession.Session;

                List<Task> operations = new List<Task>();
                for (int index = 0; index < 8; index++)
                {
                    int capturedIndex = index;
                    operations.Add(Task.Run(async () =>
                    {
                        byte[] payload = await session.Files.ReadAllBytesAsync(
                            "/docs/file-" + capturedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt",
                            cancellationToken).ConfigureAwait(false);

                        if (!string.Equals(
                            Encoding.UTF8.GetString(payload),
                            "payload-" + capturedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected concurrent mounted-session reads to preserve the correct payload for each path.");
                        }
                    }, cancellationToken));
                }

                for (int index = 0; index < 4; index++)
                {
                    int capturedIndex = index;
                    operations.Add(Task.Run(async () =>
                    {
                        string path = "/concurrent-" + capturedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt";
                        string payloadText = "concurrent-write-" + capturedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

                        await session.Directories.CreateFileAsync(path, failIfExists: true, cancellationToken).ConfigureAwait(false);
                        await session.Files.WriteAllBytesAsync(
                            path,
                            Encoding.UTF8.GetBytes(payloadText),
                            OpenNfsWriteStability.FileSync,
                            cancellationToken).ConfigureAwait(false);

                        byte[] payload = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                        if (!string.Equals(Encoding.UTF8.GetString(payload), payloadText, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected concurrent mounted-session writes to be readable through the same session.");
                        }

                        await session.Directories.DeleteFileAsync(path, cancellationToken).ConfigureAwait(false);
                    }, cancellationToken));
                }

                await Task.WhenAll(operations).ConfigureAwait(false);

                try
                {
                    _ = await session.Files.ReadAllBytesAsync("../escape.txt", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected mounted sessions to reject relative navigation segments.");
                }
                catch (ArgumentException exception)
                {
                    if (!exception.Message.Contains("Relative path navigation segments", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected the mounted-session relative-path failure to explain that '.' and '..' are unsupported.");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        private static async Task<MountedSessionContext> CreateMountedSessionAsync(
            OpenNfsTcpInteropHost host,
            CancellationToken cancellationToken)
        {
            await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.MountPort)
                .Build();
            await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsMountV3Result mountResult =
                await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
            if (!mountResult.IsSuccess)
            {
                throw new InvalidOperationException("Expected mounted-session setup to obtain a successful MOUNT v3 root handle.");
            }

            OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.NfsPort)
                .Build();
            await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return new MountedSessionContext(nfsClient, nfsClient.CreateMountSession("/export", mountResult));
        }

        private sealed class MountedSessionContext : IAsyncDisposable
        {
            private readonly OpenNfsClient _client;

            internal MountedSessionContext(OpenNfsClient client, OpenNfsMountSession session)
            {
                _client = client;
                Session = session;
            }

            internal OpenNfsMountSession Session { get; }

            public async ValueTask DisposeAsync()
            {
                await Session.DisposeAsync().ConfigureAwait(false);
                await _client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
