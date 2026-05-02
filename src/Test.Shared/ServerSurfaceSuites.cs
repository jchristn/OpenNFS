namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering the public OpenNFS server builder and host contracts.
    /// </summary>
    public static class ServerSurfaceSuites
    {
        /// <summary>
        /// Creates the shared public server surface suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "ServerSurfaceSuites",
                displayName: "Server Surface Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuildRequiresFileSystem",
                        displayName: "Server builder requires a file system contract",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                OpenNfsServer server = new OpenNfsServerBuilder()
                                    .AddExport("/exports/sample", @"C:\Exports\Sample")
                                    .Build();

                                throw new InvalidOperationException("Expected the server builder to reject construction without a file system contract.");
                            }
                            catch (InvalidOperationException exception)
                            {
                                if (!exception.Message.Contains("UseFileSystem", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the missing-file-system failure to direct the host toward UseFileSystem.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "StaticExportsResolve",
                        displayName: "Server builder constructs validated static exports",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string exportRoot = @"C:\Exports\Alpha";
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [exportRoot] = NfsPathKind.Directory,
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .WithServerName("Alpha")
                                .WithListenerAddress("127.0.0.1")
                                .WithListenerPort(3049)
                                .WithUdpForNfsV3(true)
                                .WithMaximumConnections(64)
                                .UseFileSystem(fileSystem)
                                .AddExport("/exports/alpha", exportRoot, readOnly: true)
                                .Build();

                            NfsGetExportsResponse exportResponse =
                                await server.GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);
                            IReadOnlyList<OpenNfsExportDefinition> exports = exportResponse.Exports;

                            if (!string.Equals(server.Settings.ServerName, "Alpha", StringComparison.Ordinal)
                                || !string.Equals(server.Settings.ListenerAddress, "127.0.0.1", StringComparison.Ordinal)
                                || server.Settings.ListenerPort != 3049
                                || !server.Settings.EnableUdpForNfsV3
                                || server.Settings.MaximumConnections != 64)
                            {
                                throw new InvalidOperationException("Expected server builder settings to round-trip into the constructed server wrapper.");
                            }

                            if (!ReferenceEquals(server.Settings.FileSystem, fileSystem))
                            {
                                throw new InvalidOperationException("Expected the configured file system contract to be retained by the constructed server settings.");
                            }

                            if (exports.Count != 1
                                || !string.Equals(exports[0].ExportPath, "/exports/alpha", StringComparison.Ordinal)
                                || !string.Equals(exports[0].SourcePath, exportRoot, StringComparison.Ordinal)
                                || !exports[0].ReadOnly)
                            {
                                throw new InvalidOperationException("Expected the constructed server to resolve the configured static export definition.");
                            }

                            if (fileSystem.RequestedPaths.Count != 1
                                || !string.Equals(fileSystem.RequestedPaths[0], exportRoot, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected export resolution to validate the configured source path through the file system contract.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "StaticAndDynamicExportsCompose",
                        displayName: "Server builder composes static exports with a host export provider",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string staticRoot = @"C:\Exports\Static";
                            string dynamicRoot = @"C:\Exports\Dynamic";
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [staticRoot] = NfsPathKind.Directory,
                                    [dynamicRoot] = NfsPathKind.Directory,
                                });

                            StaticTestNfsExportProvider dynamicProvider = new StaticTestNfsExportProvider(
                                new OpenNfsExportDefinition[]
                                {
                                    new OpenNfsExportDefinition("/exports/dynamic", dynamicRoot),
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/exports/static", staticRoot)
                                .UseExportProvider(dynamicProvider)
                                .Build();

                            NfsGetExportsResponse exportResponse =
                                await server.GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);
                            IReadOnlyList<OpenNfsExportDefinition> exports = exportResponse.Exports;

                            if (exports.Count != 2)
                            {
                                throw new InvalidOperationException("Expected static and dynamic export sources to compose into a single export set.");
                            }

                            if (dynamicProvider.InvocationCount != 1)
                            {
                                throw new InvalidOperationException("Expected the configured dynamic export provider to be invoked during export resolution.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "NonDirectoryExportRootFails",
                        displayName: "Server export resolution rejects non-directory export roots",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string exportRoot = @"C:\Exports\File.txt";
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [exportRoot] = NfsPathKind.File,
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/exports/file", exportRoot)
                                .Build();

                            try
                            {
                                _ = await server.GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected non-directory export roots to be rejected during export resolution.");
                            }
                            catch (InvalidOperationException exception)
                            {
                                if (!exception.Message.Contains("/exports/file", StringComparison.Ordinal)
                                    || !exception.Message.Contains("directory", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected non-directory export validation failures to include export-path and directory context.");
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "OperationContextsHonorCancellation",
                        displayName: "Server operation contexts honor cancellation tokens",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [@"C:\Exports\Canceled"] = NfsPathKind.Directory,
                                });

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/exports/canceled", @"C:\Exports\Canceled")
                                .Build();

                            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                            cancellationTokenSource.Cancel();

                            try
                            {
                                NfsGetExportsResponse response =
                                    await server.GetExportsAsync(new NfsGetExportsRequest(cancellationTokenSource.Token)).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected a canceled export-resolution request context to abort the operation.");
                            }
                            catch (OperationCanceledException)
                            {
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "MountAuthorizationDefaultsToAllowAllAndPreservesExplicitService",
                        displayName: "Server mount authorization defaults to allow-all and preserves explicit services",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));
                            StaticMountAuthorization mountAuthorization = new StaticMountAuthorization(
                                new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal));

                            OpenNfsServer defaultServer = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            OpenNfsServer configuredServer = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .UseMountAuthorization(mountAuthorization)
                                .Build();

                            if (defaultServer.Settings.MountAuthorization is null
                                || defaultServer.Settings.MountAuthorization is StaticMountAuthorization)
                            {
                                throw new InvalidOperationException("Expected the public server surface to provide a non-null default mount-authorization contract.");
                            }

                            if (!ReferenceEquals(configuredServer.Settings.MountAuthorization, mountAuthorization))
                            {
                                throw new InvalidOperationException("Expected an explicitly configured mount-authorization contract to be preserved by immutable server settings.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "OptionalCapabilitiesDefaultToAbsent",
                        displayName: "Unconfigured optional capabilities stay absent",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .Build();

                            if (server.Capabilities.AdvertisedCapabilities.Count != 0)
                            {
                                throw new InvalidOperationException("Expected a server with no optional capability registrations to advertise no optional capabilities.");
                            }

                            foreach (NfsCapabilityKind capabilityKind in (NfsCapabilityKind[])Enum.GetValues(typeof(NfsCapabilityKind)))
                            {
                                if (server.Capabilities.Supports(capabilityKind))
                                {
                                    throw new InvalidOperationException("Expected capability '" + capabilityKind + "' to remain absent when the host did not register it.");
                                }
                            }

                            if (server.Capabilities.Locking is not null
                                || server.Capabilities.Acls is not null
                                || server.Capabilities.Delegations is not null
                                || server.Capabilities.CopyClone is not null
                                || server.Capabilities.Sparse is not null
                                || server.Capabilities.IdMapper is not null)
                            {
                                throw new InvalidOperationException("Expected the optional capability catalog to expose null service references when no capabilities were configured.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "OptionalCapabilitiesAreDiscoveredAndAdvertised",
                        displayName: "Optional capabilities are auto-discovered and explicitly advertised",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));
                            TestNfsAcls acls = new TestNfsAcls();
                            TestNfsCopyClone copyClone = new TestNfsCopyClone();
                            TestNfsIdMapper idMapper = new TestNfsIdMapper();

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .UseAcls(acls)
                                .UseCopyClone(copyClone)
                                .UseIdMapper(idMapper)
                                .Build();

                            if (!ReferenceEquals(server.Capabilities, server.Settings.Capabilities))
                            {
                                throw new InvalidOperationException("Expected the server capability catalog to be surfaced directly from immutable server settings.");
                            }

                            if (!ReferenceEquals(server.Capabilities.Locking, fileSystem)
                                || !ReferenceEquals(server.Capabilities.Delegations, fileSystem)
                                || !ReferenceEquals(server.Capabilities.Sparse, fileSystem))
                            {
                                throw new InvalidOperationException("Expected optional capabilities implemented by the configured file system to be auto-discovered.");
                            }

                            if (!ReferenceEquals(server.Capabilities.Acls, acls)
                                || !ReferenceEquals(server.Capabilities.CopyClone, copyClone)
                                || !ReferenceEquals(server.Capabilities.IdMapper, idMapper))
                            {
                                throw new InvalidOperationException("Expected explicitly registered optional capability services to be preserved.");
                            }

                            NfsCapabilityKind[] expectedCapabilities = new[]
                            {
                                NfsCapabilityKind.Locking,
                                NfsCapabilityKind.Acls,
                                NfsCapabilityKind.Delegations,
                                NfsCapabilityKind.CopyClone,
                                NfsCapabilityKind.SparseFiles,
                                NfsCapabilityKind.IdMapping,
                            };

                            if (server.Capabilities.AdvertisedCapabilities.Count != expectedCapabilities.Length)
                            {
                                throw new InvalidOperationException("Expected every configured optional capability to appear in the advertised capability catalog.");
                            }

                            for (int index = 0; index < expectedCapabilities.Length; index++)
                            {
                                NfsCapabilityKind expectedCapability = expectedCapabilities[index];

                                if (server.Capabilities.AdvertisedCapabilities[index] != expectedCapability)
                                {
                                    throw new InvalidOperationException("Expected advertised capability order to remain stable for later protocol advertisement decisions.");
                                }

                                if (!server.Capabilities.Supports(expectedCapability))
                                {
                                    throw new InvalidOperationException("Expected configured capability '" + expectedCapability + "' to be discoverable.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltInLocalFileSystemSupportsRealDiskOperations",
                        displayName: "Built-in local file system supports real disk-backed export operations",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string exportRoot = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"), "export");
                            string seedFilePath = Path.Combine(exportRoot, "seed.txt");
                            string createdFilePath = Path.Combine(exportRoot, "created.txt");
                            string renamedFilePath = Path.Combine(exportRoot, "renamed.txt");

                            try
                            {
                                Directory.CreateDirectory(exportRoot);
                                await File.WriteAllBytesAsync(seedFilePath, Encoding.UTF8.GetBytes("seed"), cancellationToken).ConfigureAwait(false);

                                OpenNfsServer server = new OpenNfsServerBuilder()
                                    .UseLocalFileSystem()
                                    .AddExport("/exports/disk", exportRoot)
                                    .Build();

                                if (!ReferenceEquals(server.Settings.FileSystem, LocalNfsFileSystem.Default))
                                {
                                    throw new InvalidOperationException("Expected UseLocalFileSystem() to wire the built-in public local file system.");
                                }

                                NfsGetExportsResponse exportResponse =
                                    await server.GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);
                                if (exportResponse.Exports.Count != 1
                                    || !string.Equals(exportResponse.Exports[0].SourcePath, exportRoot, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to validate and expose the configured disk-backed export.");
                                }

                                NfsLookupPathResponse lookupResponse =
                                    await server.Settings.FileSystem.LookupPathAsync(
                                        new NfsLookupPathRequest(exportRoot, "seed.txt", cancellationToken)).ConfigureAwait(false);
                                if (!lookupResponse.PathInfo.Exists || lookupResponse.PathInfo.Kind != NfsPathKind.File)
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to resolve a real child file beneath the export root.");
                                }

                                NfsReadFileResponse readResponse =
                                    await server.Settings.FileSystem.ReadFileAsync(
                                        new NfsReadFileRequest(seedFilePath, 0, 16, cancellationToken)).ConfigureAwait(false);
                                if (Encoding.UTF8.GetString(readResponse.Data.ToArray()) != "seed")
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to read bytes from a real host file.");
                                }

                                NfsCreatePathResponse createResponse =
                                    await server.Settings.FileSystem.CreatePathAsync(
                                        new NfsCreatePathRequest(exportRoot, "created.txt", NfsPathKind.File, failIfExists: true, cancellationToken)).ConfigureAwait(false);
                                if (!createResponse.CreatedNew || createResponse.PathInfo.Kind != NfsPathKind.File)
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to create a new host file beneath the export root.");
                                }

                                NfsWriteFileResponse writeResponse =
                                    await server.Settings.FileSystem.WriteFileAsync(
                                        new NfsWriteFileRequest(
                                            createdFilePath,
                                            0,
                                            Encoding.UTF8.GetBytes("hello"),
                                            NfsWriteStability.FileSync,
                                            cancellationToken)).ConfigureAwait(false);
                                if (writeResponse.CommittedStability != NfsWriteStability.FileSync || writeResponse.BytesWritten != 5)
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to write and durably acknowledge real host bytes.");
                                }

                                _ = await server.Settings.FileSystem.CommitFileAsync(
                                    new NfsCommitFileRequest(createdFilePath, 0, 5, cancellationToken)).ConfigureAwait(false);

                                NfsRenamePathResponse renameResponse =
                                    await server.Settings.FileSystem.RenamePathAsync(
                                        new NfsRenamePathRequest(
                                            exportRoot,
                                            "created.txt",
                                            exportRoot,
                                            "renamed.txt",
                                            NfsPathKind.File,
                                            replaceExistingDestination: false,
                                            cancellationToken)).ConfigureAwait(false);
                                if (renameResponse.SourcePathInfo.Exists
                                    || !renameResponse.DestinationPathInfo.Exists
                                    || renameResponse.DestinationPathInfo.Kind != NfsPathKind.File)
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to rename a real host file beneath the export root.");
                                }

                                NfsDeletePathResponse deleteResponse =
                                    await server.Settings.FileSystem.DeletePathAsync(
                                        new NfsDeletePathRequest(exportRoot, "renamed.txt", NfsPathKind.File, cancellationToken)).ConfigureAwait(false);
                                if (deleteResponse.PathInfo.Exists || File.Exists(renamedFilePath))
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to delete a real host file beneath the export root.");
                                }
                            }
                            finally
                            {
                                if (Directory.Exists(Path.GetDirectoryName(exportRoot) ?? exportRoot))
                                {
                                    Directory.Delete(Path.GetDirectoryName(exportRoot) ?? exportRoot, recursive: true);
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltInLocalFileSystemReportsNegativeDiskCases",
                        displayName: "Built-in local file system reports missing paths and existing entries truthfully",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string exportRoot = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"), "export");
                            string existingFilePath = Path.Combine(exportRoot, "existing.txt");

                            try
                            {
                                Directory.CreateDirectory(exportRoot);
                                await File.WriteAllBytesAsync(existingFilePath, Encoding.UTF8.GetBytes("alpha"), cancellationToken).ConfigureAwait(false);

                                OpenNfsServer server = new OpenNfsServerBuilder()
                                    .UseLocalFileSystem()
                                    .AddExport("/exports/disk", exportRoot)
                                    .Build();

                                NfsLookupPathResponse missingLookupResponse =
                                    await server.Settings.FileSystem.LookupPathAsync(
                                        new NfsLookupPathRequest(exportRoot, "missing.txt", cancellationToken)).ConfigureAwait(false);
                                if (missingLookupResponse.PathInfo.Exists || missingLookupResponse.PathInfo.Kind != NfsPathKind.Missing)
                                {
                                    throw new InvalidOperationException("Expected the built-in local file system to report missing child entries truthfully.");
                                }

                                NfsReadFileResponse missingReadResponse =
                                    await server.Settings.FileSystem.ReadFileAsync(
                                        new NfsReadFileRequest(Path.Combine(exportRoot, "missing.txt"), 0, 16, cancellationToken)).ConfigureAwait(false);
                                if (missingReadResponse.Found || !missingReadResponse.EndOfFile || missingReadResponse.Data.Length != 0)
                                {
                                    throw new InvalidOperationException("Expected reads against a missing host file to report not-found without phantom data.");
                                }

                                NfsCreatePathResponse existingCreateResponse =
                                    await server.Settings.FileSystem.CreatePathAsync(
                                        new NfsCreatePathRequest(exportRoot, "existing.txt", NfsPathKind.File, failIfExists: true, cancellationToken)).ConfigureAwait(false);
                                if (existingCreateResponse.CreatedNew
                                    || existingCreateResponse.PathInfo.Kind != NfsPathKind.File
                                    || !string.Equals(existingCreateResponse.PathInfo.Path, existingFilePath, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected create requests against an existing host path to report reuse instead of pretending a new entry was created.");
                                }

                                NfsDeletePathResponse missingDeleteResponse =
                                    await server.Settings.FileSystem.DeletePathAsync(
                                        new NfsDeletePathRequest(exportRoot, "still-missing.txt", NfsPathKind.File, cancellationToken)).ConfigureAwait(false);
                                if (missingDeleteResponse.PathInfo.Exists || missingDeleteResponse.PathInfo.Kind != NfsPathKind.Missing)
                                {
                                    throw new InvalidOperationException("Expected deletes against a missing host file to report the resulting path as missing.");
                                }
                            }
                            finally
                            {
                                if (Directory.Exists(Path.GetDirectoryName(exportRoot) ?? exportRoot))
                                {
                                    Directory.Delete(Path.GetDirectoryName(exportRoot) ?? exportRoot, recursive: true);
                                }
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "ApplicationSurfaceShapeIsPresent",
                        displayName: "Public server application surface is present on the compatibility path",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            MethodInfo? buildApplicationWithoutOptions = typeof(OpenNfsServerBuilder).GetMethod(
                                nameof(OpenNfsServerBuilder.BuildApplication),
                                Type.EmptyTypes);
                            MethodInfo? buildApplicationWithOptions = typeof(OpenNfsServerBuilder).GetMethod(
                                nameof(OpenNfsServerBuilder.BuildApplication),
                                new[] { typeof(OpenNfsServerApplicationOptions) });
                            MethodInfo? builderGetExportsAsync = typeof(OpenNfsServerBuilder).GetMethod(
                                nameof(OpenNfsServerBuilder.GetExportsAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? startAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.StartAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? applicationGetExportsAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.GetExportsAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? tryStartAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.TryStartAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? stopAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.StopAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? tryStopAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.TryStopAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? runAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.RunAsync),
                                new[] { typeof(CancellationToken) });
                            MethodInfo? tryRunAsync = typeof(OpenNfsServerApplication).GetMethod(
                                nameof(OpenNfsServerApplication.TryRunAsync),
                                new[] { typeof(CancellationToken) });

                            if (buildApplicationWithoutOptions is null
                                || buildApplicationWithOptions is null
                                || builderGetExportsAsync is null
                                || applicationGetExportsAsync is null
                                || startAsync is null
                                || tryStartAsync is null
                                || stopAsync is null
                                || tryStopAsync is null
                                || runAsync is null
                                || tryRunAsync is null)
                            {
                                throw new InvalidOperationException("Expected the current public server compatibility surface to expose BuildApplication(), GetExportsAsync(), StartAsync, TryStartAsync, StopAsync, TryStopAsync, RunAsync, and TryRunAsync.");
                            }

                            if (typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.MountPort)) is null
                                || typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.NfsPort)) is null
                                || typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.Nfs40Port)) is null
                                || typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.IsRunning)) is null)
                            {
                                throw new InvalidOperationException("Expected the current public server compatibility surface to expose bound-port and running-state properties.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "ApplicationSurfaceExposesValidatedExports",
                        displayName: "Public server application surface exposes validated exports",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            string exportRoot = @"C:\Exports\ApplicationExports";
                            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                                {
                                    [exportRoot] = NfsPathKind.Directory,
                                });

                            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                                .UseFileSystem(fileSystem)
                                .AddExport("/exports/application", exportRoot);

                            IReadOnlyList<OpenNfsExportDefinition> builderExports =
                                await builder.GetExportsAsync(cancellationToken).ConfigureAwait(false);

                            await using OpenNfsServerApplication application = builder.BuildApplication(
                                new OpenNfsServerApplicationOptions
                                {
                                    EnableNfsV3 = true,
                                    EnableNfs40 = false,
                                });

                            IReadOnlyList<OpenNfsExportDefinition> applicationExports =
                                await application.GetExportsAsync(cancellationToken).ConfigureAwait(false);

                            if (builderExports.Count != 1
                                || applicationExports.Count != 1
                                || !string.Equals(builderExports[0].ExportPath, "/exports/application", StringComparison.Ordinal)
                                || !string.Equals(applicationExports[0].ExportPath, "/exports/application", StringComparison.Ordinal)
                                || !string.Equals(builderExports[0].SourcePath, exportRoot, StringComparison.Ordinal)
                                || !string.Equals(applicationExports[0].SourcePath, exportRoot, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the builder and managed application compatibility surfaces to expose the same validated export snapshot.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "ApplicationSurfaceExposesTryAsyncLifecycleCompanionsAndTypedFailures",
                        displayName: "Public server application surface exposes TryAsync lifecycle companions and typed failures",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteApplicationSurfaceTryLifecycleCompanionsAndTypedFailuresAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "ReadmeServerSnippetCompilesFromCleanConsumerApp",
                        displayName: "The canonical README server snippet compiles from a clean packaged consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltApplicationServesV3AndV40Flows",
                        displayName: "Built application serves real NFSv3 mount flow and NFSv4.0 direct flow",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuiltApplicationServesV3AndV40FlowsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "BuiltApplicationPreservesDeniedMountBehavior",
                        displayName: "Built application preserves denied MOUNT behavior on the public server surface",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "PackedServerPackageExecutesFromCleanConsumerApp",
                        displayName: "Packed server package runs a real server from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "PackedServerPackagePreservesNegativeExportValidation",
                        displayName: "Packed server package preserves denied mount behavior from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackagePreservesNegativeExportValidationAsync),
                });
        }

        private static async Task ExecuteApplicationSurfaceTryLifecycleCompanionsAndTypedFailuresAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ServerTryLifecycle", Guid.NewGuid().ToString("N"));
            string exportRoot = Path.Combine(rootDirectory, "export");

            try
            {
                Directory.CreateDirectory(exportRoot);
                await File.WriteAllTextAsync(Path.Combine(exportRoot, "hello.txt"), "hello", cancellationToken).ConfigureAwait(false);

                await using OpenNfsServerApplication firstApplication = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .AddExport("/data", exportRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            ListenerAddress = "127.0.0.1",
                            MountPort = 0,
                            NfsPort = 0,
                            Nfs40Port = 0,
                            NlmPort = 0,
                            NsmPort = 0,
                        });

                OpenNfsServerResult firstStartResult = await firstApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (!firstStartResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected TryStartAsync to succeed for the first managed OpenNFS server application.");
                }

                OpenNfsServerResult duplicateStartResult = await firstApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (duplicateStartResult.IsSuccess
                    || duplicateStartResult.Exception is not OpenNfsServerStateException
                    || !duplicateStartResult.Exception.Message.Contains("already running", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed state failure when the managed OpenNFS application is already running.");
                }

                await using OpenNfsServerApplication secondApplication = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .AddExport("/data", exportRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            ListenerAddress = "127.0.0.1",
                            MountPort = firstApplication.MountPort,
                            NfsPort = firstApplication.NfsPort,
                            Nfs40Port = firstApplication.Nfs40Port,
                            NlmPort = firstApplication.NlmPort,
                            NsmPort = firstApplication.NsmPort,
                        });

                OpenNfsServerResult conflictingStartResult = await secondApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (conflictingStartResult.IsSuccess || conflictingStartResult.Exception is null)
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed failure envelope when the managed OpenNFS listener ports are already in use.");
                }

                OpenNfsServerResult firstStopResult = await firstApplication.TryStopAsync(cancellationToken).ConfigureAwait(false);
                if (!firstStopResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected TryStopAsync to succeed for a running managed OpenNFS server application.");
                }

                await firstApplication.DisposeAsync().ConfigureAwait(false);
                OpenNfsServerResult disposedStartResult = await firstApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (disposedStartResult.IsSuccess
                    || disposedStartResult.Exception is not OpenNfsServerStateException
                    || !disposedStartResult.Exception.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed state failure after the managed OpenNFS application has been disposed.");
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

        private static async Task ExecuteBuiltApplicationServesV3AndV40FlowsAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ServerApplication", Guid.NewGuid().ToString("N"));
            string exportRoot = Path.Combine(rootDirectory, "export");
            string docsDirectory = Path.Combine(exportRoot, "docs");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(docsDirectory);
                await File.WriteAllTextAsync(Path.Combine(exportRoot, "hello.txt"), "hello-from-application", cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(docsDirectory, "readme.txt"), "readme-from-application", cancellationToken).ConfigureAwait(false);

                await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/data", exportRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            ListenerAddress = "127.0.0.1",
                            MountPort = 0,
                            NfsPort = 0,
                            Nfs40Port = 0,
                            NlmPort = 0,
                            NsmPort = 0,
                        });

                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                if (!application.IsRunning
                    || application.MountPort < 1
                    || application.NfsPort < 1
                    || application.Nfs40Port < 1)
                {
                    throw new InvalidOperationException("Expected BuildApplication() to start the public NFS listeners and surface bound ports.");
                }

                await AssertServerApplicationServesV3AndV40FlowsAsync(
                    "127.0.0.1",
                    application.MountPort,
                    application.NfsPort,
                    application.Nfs40Port,
                    "/data",
                    "data",
                    "hello-from-application",
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        private static async Task ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ServerApplicationDenied", Guid.NewGuid().ToString("N"));
            string exportRoot = Path.Combine(rootDirectory, "export");

            try
            {
                Directory.CreateDirectory(exportRoot);
                await File.WriteAllTextAsync(Path.Combine(exportRoot, "hello.txt"), "denied", cancellationToken).ConfigureAwait(false);

                await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseMountAuthorization(
                        new StaticMountAuthorization(
                            new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal)
                            {
                                ["/data"] = NfsMountAccessDisposition.Deny,
                            }))
                    .AddExport("/data", exportRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            ListenerAddress = "127.0.0.1",
                            MountPort = 0,
                            NfsPort = 0,
                            Nfs40Port = 0,
                            NlmPort = 0,
                            NsmPort = 0,
                        });

                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", application.NfsPort)
                    .WithMountPort(application.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                if (exports.Any(static export => string.Equals(export.ExportPath, "/data", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Expected denied mounts to hide the export from the public MOUNT v3 export listing.");
                }

                try
                {
                    _ = await client.MountAsync("/data", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected the public server application surface to preserve denied MOUNT behavior.");
                }
                catch (InvalidOperationException exception)
                {
                    if (!exception.Message.Contains("AccessDenied", StringComparison.Ordinal)
                        || !exception.Message.Contains("/data", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected denied mount failures to include both the export path and the MOUNT status.", exception);
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

        private static async Task ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.IO;
using System.Threading.Tasks;
using OpenNFS.Server;
using OpenNFS.Server.FileHandles;

public static class Program
{
    public static async Task<int> Main()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNfsPackServerRuntime", Guid.NewGuid().ToString("N"));
        string exportRoot = Path.Combine(rootDirectory, "export");
        string docsDirectory = Path.Combine(exportRoot, "docs");
        string mappingPath = Path.Combine(rootDirectory, "handles.json");

        try
        {
            Directory.CreateDirectory(docsDirectory);
            File.WriteAllText(Path.Combine(exportRoot, "hello.txt"), "hello-from-packed-server");
            File.WriteAllText(Path.Combine(docsDirectory, "readme.txt"), "readme-from-packed-server");

            await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                .WithServerName("Packed Runtime")
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                .AddExport("/data", exportRoot)
                .BuildApplication(
                    new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = "127.0.0.1",
                        MountPort = 0,
                        NfsPort = 0,
                        Nfs40Port = 0,
                        NlmPort = 0,
                        NsmPort = 0,
                    });

            await application.StartAsync().ConfigureAwait(false);
            Console.WriteLine("READY mountPort=" + application.MountPort + " nfsPort=" + application.NfsPort + " nfs40Port=" + application.Nfs40Port + " exportPath=/data");
            await Console.In.ReadLineAsync().ConfigureAwait(false);
            return 0;
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
""";

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            await AssertServerApplicationServesV3AndV40FlowsAsync(
                "127.0.0.1",
                process.MountPort,
                process.NfsPort,
                process.Nfs40Port,
                "/data",
                "data",
                "hello-from-packed-server",
                cancellationToken).ConfigureAwait(false);

            if (!process.GetCombinedOutput().Contains("READY mountPort=", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the packed external server consumer process to announce readiness before serving traffic."
                    + Environment.NewLine
                    + process.GetCombinedOutput());
            }
        }

        private static async Task ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            string programSource = ReadmeSnippetSupport.ExtractServerExampleSnippet();

            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Server", "OpenNFS.Server.csproj"),
                "OpenNFS.Server",
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

        private static async Task ExecutePackedServerPackagePreservesNegativeExportValidationAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.IO;
using System.Threading.Tasks;
using OpenNFS.Server;
using OpenNFS.Server.Abstractions;
using OpenNFS.Server.Requests;
using OpenNFS.Server.Responses;

public static class Program
{
    public static async Task<int> Main()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNfsPackServerDenied", Guid.NewGuid().ToString("N"));
        string exportRoot = Path.Combine(rootDirectory, "export");

        try
        {
            Directory.CreateDirectory(exportRoot);
            File.WriteAllText(Path.Combine(exportRoot, "hello.txt"), "denied-from-packed-server");

            await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                .UseLocalFileSystem()
                .UseMountAuthorization(new DenyAllMountAuthorization())
                .AddExport("/data", exportRoot)
                .BuildApplication(
                    new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = "127.0.0.1",
                        MountPort = 0,
                        NfsPort = 0,
                        Nfs40Port = 0,
                        NlmPort = 0,
                        NsmPort = 0,
                    });

            await application.StartAsync().ConfigureAwait(false);
            Console.WriteLine("READY mountPort=" + application.MountPort + " nfsPort=" + application.NfsPort + " nfs40Port=" + application.Nfs40Port + " exportPath=/data");
            await Console.In.ReadLineAsync().ConfigureAwait(false);
            return 0;
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

internal sealed class DenyAllMountAuthorization : INfsMountAuthorization
{
    public Task<NfsAuthorizeMountResponse> AuthorizeAsync(NfsAuthorizeMountRequest request)
    {
        return Task.FromResult(new NfsAuthorizeMountResponse(NfsMountAccessDisposition.Deny));
    }
}
""";

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", process.NfsPort)
                .WithMountPort(process.MountPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
            if (exports.Any(static export => string.Equals(export.ExportPath, "/data", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Expected the packed external server process to hide denied exports from the MOUNT v3 export listing.");
            }

            try
            {
                _ = await client.MountAsync("/data", cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Expected the packed external server process to deny direct mounts.");
            }
            catch (InvalidOperationException exception)
            {
                if (!exception.Message.Contains("AccessDenied", StringComparison.Ordinal)
                    || !exception.Message.Contains("/data", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected packed denied-mount failures to include both the export path and MOUNT status.", exception);
                }
            }
        }

        private static async Task AssertServerApplicationServesV3AndV40FlowsAsync(
            string host,
            int mountPort,
            int nfsPort,
            int nfs40Port,
            string exportPath,
            string v40ExportLeafName,
            string expectedHelloContents,
            CancellationToken cancellationToken)
        {
            await using OpenNfsClient nfs40Client = new OpenNfsClientBuilder()
                .WithServer(host, nfs40Port)
                .Build();
            await nfs40Client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            byte[] exportRootHandle = await ResolveExportRootV40Async(
                nfs40Client,
                v40ExportLeafName,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadDirectoryResult rootDirectoryResult = await nfs40Client.Directories.ReadDirectoryV40Async(
                exportRootHandle,
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            string[] rootNames = rootDirectoryResult.Entries.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            if (!rootDirectoryResult.IsSuccess
                || !rootNames.Contains("docs", StringComparer.Ordinal)
                || !rootNames.Contains("hello.txt", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to expose 'docs' and 'hello.txt' over the NFSv4.0 root flow.");
            }

            OpenNfsV40LookupResult helloLookupResult = await nfs40Client.Directories.LookupV40Async(
                exportRootHandle,
                "hello.txt",
                cancellationToken).ConfigureAwait(false);
            if (!helloLookupResult.IsSuccess || helloLookupResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the public server application surface to resolve 'hello.txt' over the NFSv4.0 direct flow.");
            }

            OpenNfsV40ReadResult helloReadResult = await nfs40Client.Files.ReadV40Async(
                helloLookupResult.ObjectFileHandle.ToArray(),
                0UL,
                128U,
                cancellationToken).ConfigureAwait(false);
            if (!helloReadResult.IsSuccess
                || !string.Equals(Encoding.UTF8.GetString(helloReadResult.Data.Span), expectedHelloContents, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to return the seeded hello.txt content over NFSv4.0.");
            }

            await using OpenNfsClient nfsV3Client = new OpenNfsClientBuilder()
                .WithServer(host, nfsPort)
                .WithMountPort(mountPort)
                .Build();
            await nfsV3Client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            await using OpenNfsMountSession session =
                await nfsV3Client.MountAsync(exportPath, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<OpenNfsV3DirectoryEntry> sessionRootEntries =
                await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
            string[] sessionRootNames = sessionRootEntries.Select(static entry => entry.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            if (!sessionRootNames.Contains("docs", StringComparer.Ordinal)
                || !sessionRootNames.Contains("hello.txt", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to expose 'docs' and 'hello.txt' over the mounted NFSv3 flow.");
            }

            byte[] helloBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);
            if (!string.Equals(Encoding.UTF8.GetString(helloBytes), expectedHelloContents, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to return the seeded hello.txt content over the mounted NFSv3 flow.");
            }

            await session.Directories.CreateFileAsync("/notes.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync(
                "/notes.txt",
                Encoding.UTF8.GetBytes("written-through-application"),
                OpenNfsWriteStability.FileSync,
                cancellationToken).ConfigureAwait(false);
            byte[] writtenBytes = await session.Files.ReadAllBytesAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
            if (!string.Equals(Encoding.UTF8.GetString(writtenBytes), "written-through-application", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the public server application surface to round-trip NFSv3 write traffic.");
            }

            await session.Directories.DeleteFileAsync("/notes.txt", cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> ResolveExportRootV40Async(
            OpenNfsClient client,
            string exportLeafName,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the NFSv4.0 root discovery flow to return a usable filehandle.");
            }

            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            if (!rootListing.IsSuccess)
            {
                throw new InvalidOperationException("Expected the NFSv4.0 root directory listing to succeed.");
            }

            bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                static entry => string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                    || string.Equals(entry.Name, "hello.txt", StringComparison.Ordinal));
            if (rootAlreadyLooksLikeExport)
            {
                return rootResult.ObjectFileHandle.ToArray();
            }

            OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                exportLeafName,
                cancellationToken).ConfigureAwait(false);
            if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException(
                    "Expected the NFSv4.0 pseudo-root to expose export leaf '" + exportLeafName + "'.");
            }

            return exportLookup.ObjectFileHandle.ToArray();
        }
    }
}
