namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// README and packaged consumer execution flows for the public server surface suites.
    /// </summary>
    internal static class ServerSurfacePackagingSupport
    {
        internal static async Task ExecutePackedServerPackageExecutesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
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

            await application.StartAsync().ConfigureAwait(false);
            Console.WriteLine("READY mountPort=" + application.MountPort + " nfsPort=" + application.NfsPort + " nfs40Port=" + application.Nfs40Port + " nfs41Port=" + application.Nfs41Port + " nfs42Port=" + application.Nfs42Port + " exportPath=/data");
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

            await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV3AndV40FlowsAsync(
                "127.0.0.1",
                process.MountPort,
                process.NfsPort,
                process.Nfs40Port,
                "/data",
                "data",
                "hello-from-packed-server",
                cancellationToken).ConfigureAwait(false);
            await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV41SessionFlowAsync(
                "127.0.0.1",
                process.Nfs41Port,
                cancellationToken).ConfigureAwait(false);
            await ServerSurfaceRuntimeSupport.AssertServerApplicationServesV42IoAdviseFlowAsync(
                "127.0.0.1",
                process.Nfs42Port,
                cancellationToken).ConfigureAwait(false);

            if (!process.GetCombinedOutput().Contains("READY mountPort=", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the packed external server consumer process to announce readiness before serving traffic."
                    + Environment.NewLine
                    + process.GetCombinedOutput());
            }
        }

        internal static async Task ExecuteReadmeServerSnippetCompilesFromCleanConsumerAppAsync(CancellationToken cancellationToken)
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

        internal static async Task ExecutePackedServerPackagePreservesNegativeExportValidationAsync(CancellationToken cancellationToken)
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
                        Nfs41Port = 0,
                        Nfs42Port = 0,
                        NlmPort = 0,
                        NsmPort = 0,
                    });

            await application.StartAsync().ConfigureAwait(false);
            Console.WriteLine("READY mountPort=" + application.MountPort + " nfsPort=" + application.NfsPort + " nfs40Port=" + application.Nfs40Port + " nfs41Port=" + application.Nfs41Port + " nfs42Port=" + application.Nfs42Port + " exportPath=/data");
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

            System.Collections.Generic.IReadOnlyList<OpenNfsExportV3Entry> exports = await client.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
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
    }
}
