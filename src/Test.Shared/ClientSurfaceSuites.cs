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
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the public OpenNFS client builder and lifetime surface.
    /// </summary>
    public static class ClientSurfaceSuites
    {
        /// <summary>
        /// Creates the shared public client surface suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClientSurfaceSuites",
                displayName: "Client Surface Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "BuilderCapturesTransportEndpointsAndRetry",
                        displayName: "Client builder captures transport, endpoint, timeout, and retry settings",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsRetryPolicy retryPolicy = new OpenNfsRetryPolicy(
                                maximumAttempts: 5,
                                initialDelay: TimeSpan.FromMilliseconds(200),
                                maximumDelay: TimeSpan.FromSeconds(3),
                                useExponentialBackoff: true);

                            OpenNfsClientSettings settings = new OpenNfsClientBuilder()
                                .WithServer("primary.example", 3049)
                                .WithMountEndpoint("mount.example", 20048)
                                .AddAlternateEndpoint("failover-a.example", 4049)
                                .AddAlternateEndpoint("failover-b.example", 5049)
                                .WithEndpointSelectionMode(OpenNfsEndpointSelectionMode.SequentialFailover)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithConnectionTimeout(TimeSpan.FromSeconds(12))
                                .WithResponseTimeout(TimeSpan.FromSeconds(45))
                                .WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.RpcSecGss)
                                .WithRetryPolicy(retryPolicy)
                                .BuildSettings();

                            if (!string.Equals(settings.ServerHost, "primary.example", StringComparison.Ordinal)
                                || settings.ServerPort != 3049
                                || !string.Equals(settings.PrimaryEndpoint.Host, "primary.example", StringComparison.Ordinal)
                                || settings.PrimaryEndpoint.Port != 3049
                                || !string.Equals(settings.MountEndpoint.Host, "mount.example", StringComparison.Ordinal)
                                || settings.MountEndpoint.Port != 20048
                                || !settings.HasExplicitMountEndpoint
                                || settings.TransportPolicy != OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                                || !settings.EnableUdpForNfsV3
                                || settings.ConnectionTimeout != TimeSpan.FromSeconds(12)
                                || settings.ResponseTimeout != TimeSpan.FromSeconds(45)
                                || settings.AuthenticationFlavor != OpenNfsAuthenticationFlavor.RpcSecGss
                                || !ReferenceEquals(settings.RetryPolicy, retryPolicy)
                                || settings.EndpointSelectionMode != OpenNfsEndpointSelectionMode.SequentialFailover)
                            {
                                throw new InvalidOperationException("Expected the client builder to round-trip the configured transport, timeout, endpoint, authentication, and retry settings.");
                            }

                            if (settings.AlternateEndpoints.Count != 2
                                || settings.CandidateEndpoints.Count != 3
                                || !string.Equals(settings.CandidateEndpoints[1].Host, "failover-a.example", StringComparison.Ordinal)
                                || settings.CandidateEndpoints[1].Port != 4049
                                || !string.Equals(settings.CandidateEndpoints[2].Host, "failover-b.example", StringComparison.Ordinal)
                                || settings.CandidateEndpoints[2].Port != 5049)
                            {
                                throw new InvalidOperationException("Expected the client settings to preserve alternate endpoints and resolved candidate ordering.");
                            }

                            if (settings.RetryPolicy.GetDelayForRetry(1) != TimeSpan.FromMilliseconds(200)
                                || settings.RetryPolicy.GetDelayForRetry(2) != TimeSpan.FromMilliseconds(400)
                                || settings.RetryPolicy.GetDelayForRetry(4) != TimeSpan.FromSeconds(1.6))
                            {
                                throw new InvalidOperationException("Expected the client retry policy to expose stable exponential backoff timing.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "DuplicateAlternateEndpointsAreRejected",
                        displayName: "Client settings reject duplicate alternate endpoints",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                OpenNfsClientSettings settings = new OpenNfsClientBuilder()
                                    .WithPrimaryEndpoint("dup.example", 2049)
                                    .AddAlternateEndpoint("dup.example", 2049)
                                    .BuildSettings();

                                if (settings.ServerPort == 0)
                                {
                                    throw new InvalidOperationException("The duplicate-endpoint validation path unexpectedly returned a settings object.");
                                }

                                throw new InvalidOperationException("Expected duplicate alternate endpoints to be rejected.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!exception.Message.Contains("alternate endpoints", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected duplicate endpoint validation to explain the alternate-endpoint constraint.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "LifetimeOpenCloseAndDisposeBehavePredictably",
                        displayName: "Client lifetime honors cancellation, close, and disposal",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithServer("lifetime.example", 2049)
                                .Build();

                            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                            cancellationTokenSource.Cancel();

                            try
                            {
                                await client.ConnectAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected client connect to honor cancellation before state changes.");
                            }
                            catch (OperationCanceledException)
                            {
                                if (client.State != OpenNfsClientState.Created)
                                {
                                    throw new InvalidOperationException("Expected canceled client connect attempts to leave the client in the created state.");
                                }
                            }

                            await client.ConnectAsync(CancellationToken.None).ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Open || client.LifetimeCancellationToken.IsCancellationRequested)
                            {
                                throw new InvalidOperationException("Expected a successfully connected client to enter the open state without canceling its lifetime token.");
                            }

                            await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Closed || !client.LifetimeCancellationToken.IsCancellationRequested)
                            {
                                throw new InvalidOperationException("Expected client disconnect to transition to the closed state and cancel the lifetime token.");
                            }

                            try
                            {
                                await client.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected a closed client not to reopen.");
                            }
                            catch (InvalidOperationException exception)
                            {
                                if (!exception.Message.Contains("cannot be reopened", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the closed-client reopen failure to explain the lifetime constraint.");
                                }
                            }

                            await client.DisposeAsync().ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Disposed)
                            {
                                throw new InvalidOperationException("Expected asynchronous disposal to transition the client to the disposed state.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountSessionSupportsPathFirstReadWriteAndMetadata",
                        displayName: "Mount sessions support path-first browse, read, write, and metadata flows",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountAsyncUsesDedicatedMountEndpoint",
                        displayName: "Client MountAsync uses a dedicated mount endpoint and returns a disposable mounted session",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountAsyncUsesDedicatedMountEndpointAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountAsyncThrowsForDeniedMount",
                        displayName: "Client MountAsync throws a clear failure for denied mounts",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountAsyncThrowsForDeniedMountAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackageExecutesFromCleanConsumerApp",
                        displayName: "Packed client package restores and executes from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "PackedClientPackagePreservesNegativeLifetimeFailures",
                        displayName: "Packed client package preserves clear negative lifetime failures from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync),
                });
        }

        private static async Task ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteMountAsyncUsesDedicatedMountEndpointAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientMountAsync", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourceRoot);
                await File.WriteAllBytesAsync(Path.Combine(sourceRoot, "hello.txt"), Encoding.UTF8.GetBytes("mount-async"), cancellationToken).ConfigureAwait(false);

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.NfsPort)
                    .WithMountPort(host.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsMountSession session = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                byte[] fileBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);

                if (!string.Equals(Encoding.UTF8.GetString(fileBytes), "mount-async", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected MountAsync to bootstrap through the dedicated mount endpoint and return a working mounted session.");
                }

                if (!string.Equals(session.ExportPath, "/export", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected MountAsync to preserve the mounted export path on the created session.");
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

        private static async Task ExecuteMountAsyncThrowsForDeniedMountAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientMountAsyncDenied", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");

            try
            {
                Directory.CreateDirectory(sourceRoot);

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseMountAuthorization(new StaticMountAuthorization(
                        new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal)
                        {
                            ["/export"] = NfsMountAccessDisposition.Deny,
                        }))
                    .AddExport("/export", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.NfsPort)
                    .WithMountPort(host.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    _ = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected MountAsync to throw when the server denies the mount.");
                }
                catch (InvalidOperationException exception)
                {
                    if (!exception.Message.Contains("AccessDenied", StringComparison.Ordinal)
                        || !exception.Message.Contains("/export", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected denied MountAsync failures to include both the export path and the MOUNT v3 status.");
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

        private static async Task ExecutePackedClientPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        int closedPort = ReserveClosedPort();

        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer("127.0.0.1", closedPort)
            .WithMountPort(closedPort)
            .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpOnly)
            .Build();

        await client.ConnectAsync(CancellationToken.None);

        await ExpectTransportFailureAsync(
            async () => { _ = await client.Exports.MountV3Async("/export", CancellationToken.None); },
            "MOUNT v3");
        await ExpectTransportFailureAsync(
            async () => { _ = await client.Directories.GetRootV40Async(CancellationToken.None); },
            "NFSv4.0 root discovery");

        Console.WriteLine("CLIENT PACKAGE EXECUTION OK");
        return 0;
    }

    private static async Task ExpectTransportFailureAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected " + operationName + " to fail against a deliberately closed loopback port.");
        }
        catch (IOException exception)
        {
            if (!exception.Message.Contains("failed", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to reach the transport layer before failing, but received: "
                    + exception.Message,
                    exception);
            }
        }
    }

    private static int ReserveClosedPort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE EXECUTION OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to report successful packaged execution."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }

        private static async Task ExecutePackedClientPackagePreservesNegativeLifetimeFailuresAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Client;

public static class Program
{
    public static async Task<int> Main()
    {
        await using OpenNfsClient client = new OpenNfsClientBuilder()
            .WithServer("127.0.0.1", 2049)
            .Build();

        await ExpectLifetimeFailureAsync(
            async () => { _ = await client.Exports.PrepareMountV3Async("/export", CancellationToken.None); },
            "MOUNT planning");
        await ExpectLifetimeFailureAsync(
            async () => { _ = await client.Directories.GetRootV40Async(CancellationToken.None); },
            "NFSv4.0 root discovery");

        Console.WriteLine("CLIENT PACKAGE NEGATIVE OK");
        return 0;
    }

    private static async Task ExpectLifetimeFailureAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected " + operationName + " to reject usage before ConnectAsync.");
        }
        catch (InvalidOperationException exception)
        {
            if (!exception.Message.Contains("ConnectAsync", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the packaged client lifetime failure to direct consumers toward ConnectAsync/OpenAsync, but received: "
                    + exception.Message,
                    exception);
            }
        }
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("CLIENT PACKAGE NEGATIVE OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external client consumer app to report successful negative-path validation."
                    + Environment.NewLine
                    + "stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "stderr:"
                    + Environment.NewLine
                    + result.StandardError);
            }
        }
    }
}
