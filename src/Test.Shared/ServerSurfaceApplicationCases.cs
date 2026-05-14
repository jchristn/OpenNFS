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
    using static Test.Shared.ServerSurfaceSuiteSupport;

    /// <summary>
    /// Server application surface, lifecycle, and exception suites.
    /// </summary>
    internal static class ServerSurfaceApplicationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
                                || typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.Nfs41Port)) is null
                                || typeof(OpenNfsServerApplication).GetProperty(nameof(OpenNfsServerApplication.Nfs42Port)) is null
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
                                    EnableNfsV3 = false,
                                    EnableNfs40 = false,
                                    EnableNfs41 = true,
                                    EnableNfs42 = true,
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
                        caseId: "ServerExceptionBaseExposesNormalizedCategory",
                        displayName: "OpenNfsServerException carries a normalized OpenNfsServerErrorCategory mirroring the client surface",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsServerStateException defaultException = new OpenNfsServerStateException("default");
                            if (defaultException.Category != OpenNfsServerErrorCategory.Conflict)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsServerStateException must default to OpenNfsServerErrorCategory.Conflict. Observed: "
                                    + defaultException.Category);
                            }

                            OpenNfsServerStateException ioException = new OpenNfsServerStateException(
                                "bind failure",
                                OpenNfsServerErrorCategory.IoError);
                            if (ioException.Category != OpenNfsServerErrorCategory.IoError)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsServerStateException must honor an explicit IoError category. Observed: "
                                    + ioException.Category);
                            }

                            OpenNfsServerStateException unsupportedException = new OpenNfsServerStateException(
                                "missing runtime",
                                OpenNfsServerErrorCategory.Unsupported,
                                new InvalidOperationException("inner"));
                            if (unsupportedException.Category != OpenNfsServerErrorCategory.Unsupported
                                || unsupportedException.InnerException is null)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsServerStateException must honor an explicit Unsupported category and preserve the inner exception. Observed: "
                                    + unsupportedException.Category
                                    + ", inner=" + (unsupportedException.InnerException?.Message ?? "<null>"));
                            }

                            // The base exception type itself is abstract; the property must be defined on the base
                            // so future typed server exceptions inherit it. Pin via reflection.
                            System.Reflection.PropertyInfo? categoryProperty = typeof(OpenNfsServerException)
                                .GetProperty(
                                    nameof(OpenNfsServerStateException.Category),
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            if (categoryProperty is null
                                || categoryProperty.PropertyType != typeof(OpenNfsServerErrorCategory))
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsServerException must expose Category as a public OpenNfsServerErrorCategory property on the base type so all server-typed exceptions inherit it.");
                            }

                            return Task.CompletedTask;
                        }),

            };
        }
    }
}
