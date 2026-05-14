namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ServerSurfaceSuiteSupport;

    /// <summary>
    /// Server builder and export validation surface cases.
    /// </summary>
    internal static class ServerSurfaceBuilderValidationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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
            };
        }
    }
}
