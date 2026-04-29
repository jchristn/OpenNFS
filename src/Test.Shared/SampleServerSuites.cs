namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the first sample-host-facing filehandle behaviors.
    /// </summary>
    public static class SampleServerSuites
    {
        /// <summary>
        /// Creates the shared sample-host and filehandle suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestSuiteDescriptor(
                suiteId: "SampleServerSuites",
                displayName: "Sample Server and Filehandle Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "IntrinsicFileHandleRoundTripsDeterministically",
                        displayName: "Intrinsic filehandles round-trip deterministically",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            IntrinsicHandleProvider provider = new IntrinsicHandleProvider();
                            NfsFileHandleTarget target = new NfsFileHandleTarget(
                                exportPath: "/exports/alpha",
                                sourcePath: @"C:\Exports\Alpha\File.txt",
                                stableIdentity: new NfsFileHandleIdentity("inode", "42"));

                            NfsCreateFileHandleResponse firstCreateResponse =
                                await provider.CreateAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsCreateFileHandleResponse secondCreateResponse =
                                await provider.CreateAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandle firstHandle = firstCreateResponse.FileHandle;
                            NfsFileHandle secondHandle = secondCreateResponse.FileHandle;

                            if (!firstHandle.Bytes.Span.SequenceEqual(secondHandle.Bytes.Span))
                            {
                                throw new InvalidOperationException("Expected intrinsic filehandles for the same target to be deterministic.");
                            }

                            NfsResolveFileHandleResponse resolveResponse =
                                await provider.ResolveAsync(new NfsResolveFileHandleRequest(firstHandle, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandleResolution resolution = resolveResponse.Resolution;

                            if (!resolution.Found
                                || resolution.Target is null
                                || !string.Equals(resolution.Target.ExportPath, target.ExportPath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.SourcePath, target.SourcePath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.StableIdentity?.Scheme, "inode", StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.StableIdentity?.Value, "42", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected intrinsic filehandles to round-trip their target and stable identity metadata.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "FileHandleSurvivesRestartWithPersistentProvider",
                        displayName: "Persistent filehandles survive provider restart and track stable identities",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string tempDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"));
                            string mappingFilePath = Path.Combine(tempDirectory, "filehandles.json");

                            try
                            {
                                NfsFileHandleIdentity stableIdentity = new NfsFileHandleIdentity("inode", "9001");
                                NfsFileHandleTarget originalTarget = new NfsFileHandleTarget(
                                    exportPath: "/exports/sample",
                                    sourcePath: @"C:\Exports\Sample\OldName.txt",
                                    stableIdentity: stableIdentity);
                                NfsFileHandleTarget renamedTarget = new NfsFileHandleTarget(
                                    exportPath: "/exports/sample",
                                    sourcePath: @"C:\Exports\Sample\NewName.txt",
                                    stableIdentity: stableIdentity);

                                PersistentMappingHandleProvider firstProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsCreateFileHandleResponse originalCreateResponse =
                                    await firstProvider.CreateAsync(new NfsCreateFileHandleRequest(originalTarget, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandle originalHandle = originalCreateResponse.FileHandle;

                                PersistentMappingHandleProvider secondProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsResolveFileHandleResponse restartResolveResponse =
                                    await secondProvider.ResolveAsync(new NfsResolveFileHandleRequest(originalHandle, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandleResolution restartResolution = restartResolveResponse.Resolution;

                                if (!restartResolution.Found
                                    || restartResolution.Target is null
                                    || !string.Equals(restartResolution.Target.SourcePath, originalTarget.SourcePath, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the persistent filehandle provider to resolve a handle after provider restart.");
                                }

                                NfsCreateFileHandleResponse renamedCreateResponse =
                                    await secondProvider.CreateAsync(new NfsCreateFileHandleRequest(renamedTarget, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandle renamedHandle = renamedCreateResponse.FileHandle;
                                if (!originalHandle.Bytes.Span.SequenceEqual(renamedHandle.Bytes.Span))
                                {
                                    throw new InvalidOperationException("Expected the persistent filehandle provider to reuse the same handle for the same stable identity after a path change.");
                                }

                                PersistentMappingHandleProvider thirdProvider = new PersistentMappingHandleProvider(mappingFilePath);
                                NfsResolveFileHandleResponse renamedResolveResponse =
                                    await thirdProvider.ResolveAsync(new NfsResolveFileHandleRequest(originalHandle, cancellationToken)).ConfigureAwait(false);
                                NfsFileHandleResolution renamedResolution = renamedResolveResponse.Resolution;

                                if (!renamedResolution.Found
                                    || renamedResolution.Target is null
                                    || !string.Equals(renamedResolution.Target.SourcePath, renamedTarget.SourcePath, StringComparison.Ordinal)
                                    || !string.Equals(renamedResolution.Target.StableIdentity?.Scheme, stableIdentity.Scheme, StringComparison.Ordinal)
                                    || !string.Equals(renamedResolution.Target.StableIdentity?.Value, stableIdentity.Value, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected persistent filehandle resolution to follow the latest path mapped to a stable identity across restarts.");
                                }
                            }
                            finally
                            {
                                if (Directory.Exists(tempDirectory))
                                {
                                    Directory.Delete(tempDirectory, recursive: true);
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "ServerBuildsWithDefaultIntrinsicFileHandleProvider",
                        displayName: "Server settings default to the intrinsic filehandle provider",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            DictionaryNfsFileSystem fileSystem =
                                new DictionaryNfsFileSystem(new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            if (server.Settings.FileHandleProvider is not IntrinsicHandleProvider)
                            {
                                throw new InvalidOperationException("Expected the public server surface to default to the intrinsic filehandle provider when no explicit provider is configured.");
                            }

                            NfsFileHandleTarget target = new NfsFileHandleTarget("/exports/default", @"C:\Exports\Default\One.txt");
                            NfsCreateFileHandleResponse createResponse =
                                await server.CreateFileHandleAsync(new NfsCreateFileHandleRequest(target, cancellationToken)).ConfigureAwait(false);
                            NfsResolveFileHandleResponse resolveResponse =
                                await server.ResolveFileHandleAsync(
                                    new NfsResolveFileHandleRequest(createResponse.FileHandle, cancellationToken)).ConfigureAwait(false);
                            NfsFileHandleResolution resolution = resolveResponse.Resolution;

                            if (!resolution.Found
                                || resolution.Target is null
                                || !string.Equals(resolution.Target.ExportPath, target.ExportPath, StringComparison.Ordinal)
                                || !string.Equals(resolution.Target.SourcePath, target.SourcePath, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the server wrapper to delegate filehandle creation and resolution through the configured default provider.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "SampleArtifactStartsFromConfigFileAndServesMountedSessionFlow",
                        displayName: "Sample artifact starts from config and serves the public mounted-session flow",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "SampleArtifactHonorsDeniedMountsFromConfigFile",
                        displayName: "Sample artifact honors denied mounts from config",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "LinuxMountReadWrite",
                        displayName: "Linux kernel client mounts, reads, and writes through the sample artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountReadWriteAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "LinuxMountDenied",
                        displayName: "Linux kernel client sees a denied mount from the sample artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountDeniedAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "PersistentFileHandleRestart",
                        displayName: "Sample artifact preserves mounted filehandles across restart when mappings persist",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePersistentFileHandleRestartAsync),

                    new TestCaseDescriptor(
                        suiteId: "SampleServerSuites",
                        caseId: "PersistentFileHandleRestartNegative",
                        displayName: "Sample artifact invalidates old filehandles when the persistent mapping file is replaced",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePersistentFileHandleRestartNegativeAsync),
                },
                beforeSuiteAsync: probe.IsAvailable
                    ? cancellationToken => new ValueTask(DockerInteropImages.EnsureBuiltAsync(cancellationToken))
                    : null);
        }

        private static async Task ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleConfig", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string resolvedSourceRoot = Path.Combine(configDirectory, "content", "export");
            string resolvedMappingPath = Path.Combine(configDirectory, "state", "handles.json");

            TcpListener reservedMountListener = CreateReservedListener();
            TcpListener reservedNfsListener = CreateReservedListener();
            TcpListener reservedNfs40Listener = CreateReservedListener();

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Configured Sample",
                        exportPath = "/exports/configured",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = ((IPEndPoint)reservedMountListener.LocalEndpoint).Port,
                        nfsPort = ((IPEndPoint)reservedNfsListener.LocalEndpoint).Port,
                        nfs40Port = ((IPEndPoint)reservedNfs40Listener.LocalEndpoint).Port,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                if (process.MountPort == ((IPEndPoint)reservedMountListener.LocalEndpoint).Port
                    || process.NfsPort == ((IPEndPoint)reservedNfsListener.LocalEndpoint).Port
                    || process.Nfs40Port == ((IPEndPoint)reservedNfs40Listener.LocalEndpoint).Port)
                {
                    throw new InvalidOperationException("Expected CLI port overrides to win over the config file when the sample process helper requests ephemeral ports.");
                }

                if (!Directory.Exists(resolvedSourceRoot)
                    || !File.Exists(Path.Combine(resolvedSourceRoot, "hello.txt"))
                    || !File.Exists(Path.Combine(resolvedSourceRoot, "docs", "nested.txt")))
                {
                    throw new InvalidOperationException("Expected config-relative source paths to resolve from the config file directory and be seeded by the sample artifact.");
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", process.NfsPort)
                    .WithMountPort(process.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsMountSession session =
                    await client.MountAsync("/exports/configured", cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsV3DirectoryEntry> rootEntries =
                    await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
                string[] rootNames = rootEntries
                    .Select(static entry => entry.Name)
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();

                if (!rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to expose the seeded root entries over the public mounted-session flow.");
                }

                byte[] fileBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Encoding.UTF8.GetString(fileBytes), "hello-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to return the seeded hello.txt payload.");
                }

                if (!File.Exists(resolvedMappingPath))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to persist filehandle mappings at the config-relative mapping path.");
                }
            }
            finally
            {
                reservedMountListener.Stop();
                reservedNfsListener.Stop();
                reservedNfs40Listener.Stop();

                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        private static async Task ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleConfigDenied", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Denied Sample",
                        exportPath = "/exports/denied",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = true,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", process.NfsPort)
                    .WithMountPort(process.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    _ = await client.MountAsync("/exports/denied", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected the sample artifact to deny mounts when denyMounts=true is supplied through the config file.");
                }
                catch (InvalidOperationException exception)
                {
                    if (!exception.Message.Contains("AccessDenied", StringComparison.Ordinal)
                        || !exception.Message.Contains("/exports/denied", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected the denied sample-config mount failure to include both the export path and the MOUNT v3 access status.");
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

        private static async Task ExecuteLinuxMountReadWriteAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleLinuxMount", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string resolvedSourceRoot = Path.Combine(configDirectory, "content", "export");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Linux Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxSampleMountCommand(sampleServer.MountPort, sampleServer.NfsPort),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel client container failed to mount or exercise the runnable Sample.OpenNfsServer artifact from its config-file bootstrap path."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (!combinedOutput.Contains("hello-from-sample-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("nested-from-sample-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal)
                    || !combinedOutput.Contains("docs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("hello.txt", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel client container to surface and update the sample export contents from the config-file bootstrap path. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string updatedHostContent =
                    await File.ReadAllTextAsync(Path.Combine(resolvedSourceRoot, "hello.txt"), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(updatedHostContent, "UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the sample export root file to reflect the Linux client write after config-file startup, but observed '"
                        + updatedHostContent
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

        private static async Task ExecuteLinuxMountDeniedAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleLinuxMountDenied", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Denied Linux Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = true,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxReadOnlyMountCommand(
                            sampleServer.MountPort,
                            sampleServer.NfsPort,
                            "/exports/sample",
                            "hello.txt",
                            "docs/nested.txt"),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    throw new InvalidOperationException(
                        "Expected the runnable Sample.OpenNfsServer artifact to deny the Linux kernel client mount from the config-file bootstrap path, but the container command succeeded."
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + result.StandardError
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (combinedOutput.Contains("hello-from-sample-opennfs", StringComparison.Ordinal)
                    || combinedOutput.Contains("nested-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the denied sample-server Linux variant not to surface mounted export contents. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
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

        private static async Task ExecutePersistentFileHandleRestartAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleRestart", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string mappingPath = Path.Combine(configDirectory, "state", "handles.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                byte[] initialRootHandle;
                byte[] helloFileHandle;

                await using (SampleOpenNfsServerProcess firstProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false))
                {
                    (byte[] RootHandle, byte[] HelloFileHandle) handles =
                        await ResolveSampleHandlesAsync(firstProcess, cancellationToken).ConfigureAwait(false);
                    initialRootHandle = handles.RootHandle;
                    helloFileHandle = handles.HelloFileHandle;
                }

                if (!File.Exists(mappingPath))
                {
                    throw new InvalidOperationException("Expected the sample artifact to persist a mapping file before the restart validation path.");
                }

                await using SampleOpenNfsServerProcess restartedProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                byte[] restartedRootHandle;
                await using (OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.MountPort)
                    .Build())
                {
                    await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    OpenNfsMountV3Result mountResult =
                        await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
                    if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                    {
                        throw new InvalidOperationException("Expected the restarted sample artifact to return a usable MOUNT v3 root filehandle.");
                    }

                    restartedRootHandle = mountResult.RootFileHandle.ToArray();
                }

                if (!initialRootHandle.AsSpan().SequenceEqual(restartedRootHandle))
                {
                    throw new InvalidOperationException("Expected the sample artifact to preserve the mounted root filehandle across restart when the mapping file is retained.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadResult readResult =
                    await nfsClient.Files.ReadV3Async(helloFileHandle, 0UL, 4096U, cancellationToken).ConfigureAwait(false);
                if (!readResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(readResult.Data.Span), "hello-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the pre-restart sample filehandle to remain readable after restart when the mapping file is retained.");
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

        private static async Task ExecutePersistentFileHandleRestartNegativeAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleRestartNegative", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string originalMappingPath = Path.Combine(configDirectory, "state", "handles-a.json");
            string replacementMappingPath = Path.Combine(configDirectory, "state", "handles-b.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Negative Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles-a.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                byte[] initialRootHandle;
                byte[] helloFileHandle;

                await using (SampleOpenNfsServerProcess firstProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false))
                {
                    (byte[] RootHandle, byte[] HelloFileHandle) handles =
                        await ResolveSampleHandlesAsync(firstProcess, cancellationToken).ConfigureAwait(false);
                    initialRootHandle = handles.RootHandle;
                    helloFileHandle = handles.HelloFileHandle;
                }

                if (!File.Exists(originalMappingPath))
                {
                    throw new InvalidOperationException("Expected the original sample mapping file to exist before the negative restart path.");
                }

                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Negative Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles-b.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess restartedProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                byte[] restartedRootHandle;
                await using (OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.MountPort)
                    .Build())
                {
                    await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    OpenNfsMountV3Result mountResult =
                        await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
                    if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                    {
                        throw new InvalidOperationException("Expected the restarted sample artifact to return a usable MOUNT v3 root filehandle after replacing the mapping file.");
                    }

                    restartedRootHandle = mountResult.RootFileHandle.ToArray();
                }

                if (initialRootHandle.AsSpan().SequenceEqual(restartedRootHandle))
                {
                    throw new InvalidOperationException("Expected the restarted sample artifact to issue a different root filehandle after the persistent mapping file was replaced.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadResult readResult =
                    await nfsClient.Files.ReadV3Async(helloFileHandle, 0UL, 4096U, cancellationToken).ConfigureAwait(false);
                if (readResult.Status != OpenNfsV3Status.Stale)
                {
                    throw new InvalidOperationException(
                        "Expected the pre-restart sample filehandle to become stale after replacing the persistent mapping file, but observed status '"
                        + readResult.Status.ToString()
                        + "'.");
                }

                if (File.Exists(replacementMappingPath) == false)
                {
                    throw new InvalidOperationException("Expected the replacement sample mapping file to be created during the negative restart path.");
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

        private static TcpListener CreateReservedListener()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            return listener;
        }

        private static string CreateLinuxReadOnlyMountCommand(
            int mountPort,
            int nfsPort,
            string exportPath,
            string primaryFilePath,
            string nestedFilePath)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs; ",
                "cat /mnt/opennfs/", primaryFilePath, "; ",
                "cat /mnt/opennfs/", nestedFilePath, "; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        private static string CreateLinuxSampleMountCommand(int mountPort, int nfsPort)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:/exports/sample /mnt/opennfs; ",
                "cat /mnt/opennfs/hello.txt; ",
                "cat /mnt/opennfs/docs/nested.txt; ",
                "printf 'UPDATED-FROM-LINUX-CLIENT' | dd of=/mnt/opennfs/hello.txt conv=notrunc status=none; ",
                "sync; ",
                "cat /mnt/opennfs/hello.txt; ",
                "ls -1 /mnt/opennfs; ",
                "ls -1 /mnt/opennfs/docs; ",
                "umount /mnt/opennfs");
        }

        private static async Task<(byte[] RootHandle, byte[] HelloFileHandle)> ResolveSampleHandlesAsync(
            SampleOpenNfsServerProcess process,
            System.Threading.CancellationToken cancellationToken)
        {
            await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", process.MountPort)
                .Build();
            await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsMountV3Result mountResult =
                await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
            if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to return a usable MOUNT v3 root filehandle.");
            }

            byte[] rootHandle = mountResult.RootFileHandle.ToArray();

            await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", process.NfsPort)
                .Build();
            await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV3LookupResult lookupResult =
                await nfsClient.Directories.LookupV3Async(rootHandle, "hello.txt", cancellationToken).ConfigureAwait(false);
            if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to resolve 'hello.txt' to a usable filehandle.");
            }

            return (rootHandle, lookupResult.ObjectFileHandle.ToArray());
        }

        private static async Task WriteSampleConfigurationAsync(
            string configPath,
            object configuration,
            System.Threading.CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);
        }
    }
}
