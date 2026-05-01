namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Apis;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;
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
                        caseId: "CompatibilityPrimarySurfaceShapeIsPresent",
                        displayName: "Client compatibility primary surface is present and advanced members are marked secondary",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            MethodInfo? connectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ConnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? tryConnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryConnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? disconnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.DisconnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? tryDisconnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryDisconnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? openAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.OpenAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? closeAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CloseAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? mountAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.MountAsync), new[] { typeof(string), typeof(CancellationToken) });
                            MethodInfo? tryMountAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryMountAsync), new[] { typeof(string), typeof(CancellationToken) });
                            MethodInfo? createMountSessionFromHandle = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CreateMountSession), new[] { typeof(string), typeof(byte[]) });
                            MethodInfo? createMountSessionFromResult = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CreateMountSession), new[] { typeof(string), typeof(OpenNfsMountV3Result) });
                            MethodInfo? prepareV3ProcedureAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.PrepareV3ProcedureAsync), new[] { typeof(OpenNfsV3ProcedureRequest), typeof(CancellationToken) });
                            MethodInfo? executeV3ProcedureAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ExecuteV3ProcedureAsync), new[] { typeof(OpenNfsV3ProcedureRequest), typeof(OpenNfsOperationIdempotency), typeof(CancellationToken) });
                            MethodInfo? prepareCompoundAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.PrepareCompoundAsync), new[] { typeof(OpenNfsCompoundRequest), typeof(CancellationToken) });
                            MethodInfo? executeCompoundAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ExecuteCompoundAsync), new[] { typeof(OpenNfsCompoundRequest), typeof(OpenNfsOperationIdempotency), typeof(CancellationToken) });
                            MethodInfo? tryListExportsV3Async = typeof(ExportApis).GetMethod(nameof(ExportApis.TryListExportsV3Async), new[] { typeof(CancellationToken) });

                            if (connectAsync is null
                                || tryConnectAsync is null
                                || disconnectAsync is null
                                || tryDisconnectAsync is null
                                || mountAsync is null
                                || tryMountAsync is null
                                || createMountSessionFromHandle is null
                                || createMountSessionFromResult is null
                                || prepareV3ProcedureAsync is null
                                || executeV3ProcedureAsync is null
                                || prepareCompoundAsync is null
                                || executeCompoundAsync is null
                                || tryListExportsV3Async is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Files)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Directories)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Metadata)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Locks)) is null)
                            {
                                throw new InvalidOperationException("Expected the aligned compatibility primary surface to expose ConnectAsync, DisconnectAsync, TryConnectAsync, TryDisconnectAsync, MountAsync, TryMountAsync, export bootstrap Try APIs, OpenNfsMountSession, and its grouped members.");
                            }

                            AssertEditorBrowsableState(openAsync, EditorBrowsableState.Never, nameof(OpenNfsClient.OpenAsync));
                            AssertEditorBrowsableState(closeAsync, EditorBrowsableState.Never, nameof(OpenNfsClient.CloseAsync));
                            AssertEditorBrowsableState(createMountSessionFromHandle, EditorBrowsableState.Advanced, "CreateMountSession(string, byte[])");
                            AssertEditorBrowsableState(createMountSessionFromResult, EditorBrowsableState.Advanced, "CreateMountSession(string, OpenNfsMountV3Result)");
                            AssertEditorBrowsableState(prepareV3ProcedureAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.PrepareV3ProcedureAsync));
                            AssertEditorBrowsableState(executeV3ProcedureAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.ExecuteV3ProcedureAsync));
                            AssertEditorBrowsableState(prepareCompoundAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.PrepareCompoundAsync));
                            AssertEditorBrowsableState(executeCompoundAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.ExecuteCompoundAsync));

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "BuilderCapturesAuthSysCredentialsAndMountTrafficUsesThem",
                        displayName: "Client builder captures AUTH_SYS credentials and mount traffic uses the configured machine name",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "ReadmeClientSnippetCompilesFromCleanConsumerApp",
                        displayName: "The canonical README client snippet compiles from a clean packaged consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync),

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
                        caseId: "MountSessionMaintainsSameSessionMutationConsistency",
                        displayName: "Mount sessions keep same-session mutation paths consistent",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigation",
                        displayName: "Mount sessions support concurrent path operations and reject relative navigation segments",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync),

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
                        caseId: "TryLifecycleAndMountSurfacesTypedResults",
                        displayName: "Client Try lifecycle and mount surfaces return typed result envelopes",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync),

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

        private static async Task ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            string programSource = ReadmeSnippetSupport.ExtractClientExampleSnippet();

            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                cancellationToken).ConfigureAwait(false);

            _ = await DotnetCli.RunCheckedAsync(
                new[]
                {
                    "build",
                    "--disable-build-servers",
                    project.ProjectPath,
                    "-c",
                    "Release",
                    "--no-restore",
                },
                project.ProjectDirectory,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(3)).ConfigureAwait(false);
        }

        private static void AssertEditorBrowsableState(MethodInfo? methodInfo, EditorBrowsableState expectedState, string displayName)
        {
            if (methodInfo is null)
            {
                throw new InvalidOperationException("Expected method '" + displayName + "' to exist on the current public client surface.");
            }

            EditorBrowsableAttribute? attribute = methodInfo.GetCustomAttribute<EditorBrowsableAttribute>();
            if (attribute is null || attribute.State != expectedState)
            {
                throw new InvalidOperationException(
                    "Expected method '"
                    + displayName
                    + "' to be marked with EditorBrowsableState."
                    + expectedState
                    + " on the current compatibility surface.");
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
                catch (OpenNfsMountV3StatusException exception)
                {
                    if (exception.Status != OpenNfsMountV3Status.AccessDenied
                        || exception.Category != OpenNfsErrorCategory.AccessDenied
                        || !string.Equals(exception.ExportPath, "/export", StringComparison.Ordinal)
                        || !exception.Message.Contains("AccessDenied", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected denied MountAsync failures to surface a typed MOUNT v3 status exception with the export path and normalized category.");
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
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task listenerTask = RunListenerAsync(listener);

        try
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", listenerPort)
                .WithMountPort(listenerPort)
                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpOnly)
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithResponseTimeout(TimeSpan.FromSeconds(2))
                .Build();

            await client.ConnectAsync(CancellationToken.None);

            await ExpectExecutionFailureAsync(
                async () => { _ = await client.Exports.MountV3Async("/export", CancellationToken.None); },
                "MOUNT v3");
            await ExpectExecutionFailureAsync(
                async () => { _ = await client.Directories.GetRootV40Async(CancellationToken.None); },
                "NFSv4.0 root discovery");

            Console.WriteLine("CLIENT PACKAGE EXECUTION OK");
            return 0;
        }
        finally
        {
            listener.Stop();
            await listenerTask;
        }
    }

    private static async Task ExpectExecutionFailureAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected " + operationName + " to fail against the disposable loopback listener.");
        }
        catch (Exception exception) when (string.Equals(exception.GetType().FullName, "OpenNFS.Client.OpenNfsClientIoException", StringComparison.Ordinal))
        {
            if (!exception.Message.Contains("failed", StringComparison.Ordinal)
                && !exception.Message.Contains("reset", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to reach the transport layer before failing, but received: "
                    + exception.Message,
                    exception);
            }
        }
        catch (Exception exception)
        {
            if (!string.Equals(exception.GetType().FullName, "OpenNFS.Client.OpenNfsClientProtocolException", StringComparison.Ordinal)
                && !exception.Message.Contains("RPC reply envelope", StringComparison.Ordinal)
                && !exception.Message.Contains("configured timeout", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to fail after entering the packaged execution path, but received "
                    + exception.GetType().FullName
                    + ": "
                    + exception.Message,
                    exception);
            }
        }
    }

    private static async Task RunListenerAsync(TcpListener listener)
    {
        for (int index = 0; index < 2; index++)
        {
            try
            {
                using TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
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

        private static async Task ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientTryMount", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourceRoot);

                OpenNfsServer server = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .UseMountAuthorization(new StaticMountAuthorization(
                        new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal)
                        {
                            ["/export"] = NfsMountAccessDisposition.Allow,
                            ["/denied"] = NfsMountAccessDisposition.Deny,
                        }))
                    .AddExport("/export", sourceRoot)
                    .AddExport("/denied", sourceRoot)
                    .Build();

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", host.NfsPort)
                    .WithMountPort(host.MountPort)
                    .Build();

                OpenNfsClientResult preConnectFailure = await client.TryMountAsync("/export", cancellationToken).ConfigureAwait(false);
                if (preConnectFailure.IsSuccess
                    || preConnectFailure.Exception is not OpenNfsClientStateException
                    || preConnectFailure.ErrorCategory != OpenNfsErrorCategory.Unknown)
                {
                    throw new InvalidOperationException("Expected TryMountAsync before ConnectAsync to return a typed client-state failure.");
                }

                OpenNfsClientResult connectResult = await client.TryConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected TryConnectAsync to succeed against the current in-memory host.");
                }

                OpenNfsClientResult<IReadOnlyList<OpenNfsExportV3Entry>> exportsResult =
                    await client.Exports.TryListExportsV3Async(cancellationToken).ConfigureAwait(false);
                if (!exportsResult.IsSuccess
                    || exportsResult.Value is null
                    || exportsResult.Value.Count != 1
                    || !string.Equals(exportsResult.Value[0].ExportPath, "/export", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected TryListExportsV3Async to return the visible export set on success.");
                }

                OpenNfsClientResult<OpenNfsMountSession> successMount =
                    await client.TryMountAsync("/export", cancellationToken).ConfigureAwait(false);
                if (!successMount.IsSuccess
                    || successMount.Value is null
                    || !string.Equals(successMount.Value.ExportPath, "/export", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected TryMountAsync to return a mounted session on success.");
                }

                await successMount.Value.DisposeAsync().ConfigureAwait(false);

                OpenNfsClientResult<OpenNfsMountSession> deniedMount =
                    await client.TryMountAsync("/denied", cancellationToken).ConfigureAwait(false);
                if (deniedMount.IsSuccess
                    || deniedMount.Exception is not OpenNfsMountV3StatusException mountException
                    || mountException.Status != OpenNfsMountV3Status.AccessDenied
                    || deniedMount.MountV3Status != OpenNfsMountV3Status.AccessDenied
                    || deniedMount.ErrorCategory != OpenNfsErrorCategory.AccessDenied)
                {
                    throw new InvalidOperationException("Expected TryMountAsync to return a typed MOUNT v3 access-denied failure envelope.");
                }

                OpenNfsClientResult disconnectResult = await client.TryDisconnectAsync(cancellationToken).ConfigureAwait(false);
                if (!disconnectResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected TryDisconnectAsync to succeed after a normal client lifetime.");
                }

                OpenNfsClientResult reconnectFailure = await client.TryConnectAsync(cancellationToken).ConfigureAwait(false);
                if (reconnectFailure.IsSuccess
                    || reconnectFailure.Exception is not OpenNfsClientStateException)
                {
                    throw new InvalidOperationException("Expected TryConnectAsync after CloseAsync/DisconnectAsync to return a typed client-state failure.");
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
    }
}
