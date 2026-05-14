namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Managed application lifetime and direct runtime flows for the public server surface suites.
    /// </summary>
    internal static class ServerSurfaceApplicationSupport
    {
        internal static async Task ExecuteApplicationSurfaceTryLifecycleCompanionsAndTypedFailuresAsync(CancellationToken cancellationToken)
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
                            EnableNfs41 = true,
                            EnableNfs42 = true,
                            MountPort = 0,
                            NfsPort = 0,
                            Nfs40Port = 0,
                            Nfs41Port = 0,
                            Nfs42Port = 0,
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
                    || duplicateStartResult.Exception is not OpenNfsServerStateException duplicateStartException
                    || !duplicateStartException.Message.Contains("already running", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed state failure when the managed OpenNFS application is already running.");
                }

                if (duplicateStartException.Category != OpenNfsServerErrorCategory.Conflict)
                {
                    throw new InvalidOperationException(
                        "Expected the duplicate-start typed failure to carry OpenNfsServerErrorCategory.Conflict. Observed: "
                        + duplicateStartException.Category);
                }

                await using OpenNfsServerApplication secondApplication = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .AddExport("/data", exportRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            ListenerAddress = "127.0.0.1",
                            EnableNfs41 = true,
                            EnableNfs42 = true,
                            MountPort = firstApplication.MountPort,
                            NfsPort = firstApplication.NfsPort,
                            Nfs40Port = firstApplication.Nfs40Port,
                            Nfs41Port = firstApplication.Nfs41Port,
                            Nfs42Port = firstApplication.Nfs42Port,
                            NlmPort = firstApplication.NlmPort,
                            NsmPort = firstApplication.NsmPort,
                        });

                OpenNfsServerResult conflictingStartResult = await secondApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (conflictingStartResult.IsSuccess || conflictingStartResult.Exception is null)
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed failure envelope when the managed OpenNFS listener ports are already in use.");
                }

                if (conflictingStartResult.Exception is OpenNfsServerStateException conflictingStartException
                    && conflictingStartException.Category != OpenNfsServerErrorCategory.IoError
                    && conflictingStartException.Category != OpenNfsServerErrorCategory.Conflict)
                {
                    throw new InvalidOperationException(
                        "Expected the bind-conflict typed failure to carry OpenNfsServerErrorCategory.IoError (port bind failure) or Conflict. Observed: "
                        + conflictingStartException.Category);
                }

                OpenNfsServerResult firstStopResult = await firstApplication.TryStopAsync(cancellationToken).ConfigureAwait(false);
                if (!firstStopResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected TryStopAsync to succeed for a running managed OpenNFS server application.");
                }

                await firstApplication.DisposeAsync().ConfigureAwait(false);
                OpenNfsServerResult disposedStartResult = await firstApplication.TryStartAsync(cancellationToken).ConfigureAwait(false);
                if (disposedStartResult.IsSuccess
                    || disposedStartResult.Exception is not OpenNfsServerStateException disposedStartException
                    || !disposedStartException.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Expected TryStartAsync to report a typed state failure after the managed OpenNFS application has been disposed.");
                }

                if (disposedStartException.Category != OpenNfsServerErrorCategory.Conflict)
                {
                    throw new InvalidOperationException(
                        "Expected the disposed-application typed failure to carry OpenNfsServerErrorCategory.Conflict. Observed: "
                        + disposedStartException.Category);
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

        internal static async Task ExecuteBuiltApplicationServesV3AndV40FlowsAsync(CancellationToken cancellationToken)
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
                            EnableNfs41 = true,
                            EnableNfs42 = true,
                            MountPort = 0,
                            NfsPort = 0,
                            Nfs40Port = 0,
                            Nfs41Port = 0,
                            Nfs42Port = 0,
                            NlmPort = 0,
                            NsmPort = 0,
                        });

                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                if (!application.IsRunning
                    || application.MountPort < 1
                    || application.NfsPort < 1
                    || application.Nfs40Port < 1
                    || application.Nfs41Port < 1
                    || application.Nfs42Port < 1)
                {
                    throw new InvalidOperationException("Expected BuildApplication() to start the public NFS listeners and surface bound ports, including the opt-in NFSv4.1 and initial NFSv4.2 ports.");
                }

                await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV3AndV40FlowsAsync(
                    "127.0.0.1",
                    application.MountPort,
                    application.NfsPort,
                    application.Nfs40Port,
                    "/data",
                    "data",
                    "hello-from-application",
                    cancellationToken).ConfigureAwait(false);
                await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV41SessionFlowAsync(
                    "127.0.0.1",
                    application.Nfs41Port,
                    cancellationToken).ConfigureAwait(false);
                await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV42IoAdviseFlowAsync(
                    "127.0.0.1",
                    application.Nfs42Port,
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

        internal static async Task ExecuteBuiltApplicationPreservesDeniedMountBehaviorAsync(CancellationToken cancellationToken)
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
    }
}
