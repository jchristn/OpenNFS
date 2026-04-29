namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server;
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
                        caseId: "PackedServerPackageExecutesFromCleanConsumerApp",
                        displayName: "Packed server package restores and executes from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ServerSurfaceSuites",
                        caseId: "PackedServerPackagePreservesNegativeExportValidation",
                        displayName: "Packed server package preserves clear negative export validation from a clean consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecutePackedServerPackagePreservesNegativeExportValidationAsync),
                });
        }

        private static async Task ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Server;

public static class Program
{
    public static async Task<int> Main()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNfsPackServer", Guid.NewGuid().ToString("N"));
        string exportRoot = Path.Combine(rootDirectory, "export");

        Directory.CreateDirectory(exportRoot);

        try
        {
            File.WriteAllText(Path.Combine(exportRoot, "hello.txt"), "hello");

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseLocalFileSystem()
                .AddExport("/data", exportRoot)
                .Build();

            IReadOnlyList<OpenNfsExportDefinition> exports = await server.GetExportsAsync(CancellationToken.None);
            if (exports.Count != 1
                || !string.Equals(exports[0].ExportPath, "/data", StringComparison.Ordinal)
                || !string.Equals(exports[0].SourcePath, exportRoot, StringComparison.Ordinal)
                || exports[0].ReadOnly)
            {
                throw new InvalidOperationException("The packaged server surface did not preserve the configured local export.");
            }

            Console.WriteLine("SERVER PACKAGE EXECUTION OK");
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

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Server", "OpenNFS.Server.csproj"),
                "OpenNFS.Server",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("SERVER PACKAGE EXECUTION OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external server consumer app to report successful packaged execution."
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

        private static async Task ExecutePackedServerPackagePreservesNegativeExportValidationAsync(CancellationToken cancellationToken)
        {
            const string programSource = """
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenNFS.Server;

public static class Program
{
    public static async Task<int> Main()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), "OpenNfsPackServerNegative", Guid.NewGuid().ToString("N"), "missing");

        OpenNfsServer server = new OpenNfsServerBuilder()
            .UseLocalFileSystem()
            .AddExport("/missing", missingRoot)
            .Build();

        try
        {
            _ = await server.GetExportsAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected missing export roots to be rejected by the packaged server surface.");
        }
        catch (InvalidOperationException exception)
        {
            if (!exception.Message.Contains("/missing", StringComparison.Ordinal)
                || !exception.Message.Contains("missing source path", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected packaged export validation failures to include both the export path and missing-path context, but received: "
                    + exception.Message,
                    exception);
            }
        }

        Console.WriteLine("SERVER PACKAGE NEGATIVE OK");
        return 0;
    }
}
""";

            DotnetCommandResult result = await ExternalPackageConsumerSupport.RunSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Server", "OpenNFS.Server.csproj"),
                "OpenNFS.Server",
                programSource,
                cancellationToken).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("SERVER PACKAGE NEGATIVE OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the clean external server consumer app to report successful negative-path validation."
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
