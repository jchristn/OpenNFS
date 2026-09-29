namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Mounted-session and portmapper cases against an in-process OpenNFS server started from the public OpenNFS.Server surface.
    /// </summary>
    internal static class MountSessionIntegrationSupport
    {
        internal static async Task ExecuteCoreScenariosAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            await using (session.ConfigureAwait(false))
            {
                MountSessionScenarioTarget target = new MountSessionScenarioTarget(
                    "in-process OpenNFS server",
                    client,
                    session,
                    modeRoundTripsExactly: !OperatingSystem.IsWindows(),
                    rejectsWindowsUnsafeNames: OperatingSystem.IsWindows());
                await RunCoreScenariosAsync(target, cancellationToken).ConfigureAwait(false);
            }

            string[] namesDirectories = Directory.GetDirectories(server.SourceRoot, " x", SearchOption.AllDirectories);
            Require(namesDirectories.Length == 1, "Expected exactly one host directory named ' x' (leading space preserved).");
            string[] hostNames = Directory.GetFileSystemEntries(namesDirectories[0]).Select(static path => Path.GetFileName(path)).ToArray();
            foreach (string name in PortableSpecialNames.Where(static name => name != "internal   multiple   spaces.txt"))
            {
                Require(hostNames.Contains(name, StringComparer.Ordinal), "Expected the host directory to contain the verbatim name '" + name + "'.");
            }

            Require(
                !hostNames.Contains("leading-spaces.txt", StringComparer.Ordinal)
                    && !hostNames.Contains("trailing-space.txt", StringComparer.Ordinal)
                    && !hostNames.Contains("trailing-dot", StringComparer.Ordinal),
                "Expected no trimmed aliases on the host.");

            string[] leftovers = Directory.GetFiles(server.SourceRoot, "*.bin", SearchOption.AllDirectories)
                .Where(static path => path.Contains("truncate.bin", StringComparison.Ordinal))
                .ToArray();
            Require(leftovers.Length == 1 && new FileInfo(leftovers[0]).Length == 0, "Expected the host file behind the truncate scenario to be empty on disk.");
        }

        internal static async Task ExecuteLargeDirectoryAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            await using (session.ConfigureAwait(false))
            {
                await RunLargeDirectoryScenarioAsync(
                    new MountSessionScenarioTarget("in-process OpenNFS server", client, session, modeRoundTripsExactly: false),
                    1500,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        internal static async Task ExecuteConcurrencyAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            await using (session.ConfigureAwait(false))
            {
                await RunConcurrencyScenarioAsync(
                    new MountSessionScenarioTarget("in-process OpenNFS server", client, session, modeRoundTripsExactly: false),
                    32,
                    cancellationToken).ConfigureAwait(false);

                Task<bool>[] lookups = Enumerable.Range(0, 64)
                    .Select(index => session.Metadata.ExistsAsync(index % 2 == 0 ? "/" : "/does-not-exist-" + index, cancellationToken))
                    .ToArray();
                bool[] results = await Task.WhenAll(lookups).ConfigureAwait(false);
                Require(results.Where((value, index) => index % 2 == 0).All(static value => value), "Expected concurrent root lookups to succeed.");
                Require(results.Where((value, index) => index % 2 == 1).All(static value => !value), "Expected concurrent missing lookups to report false.");
            }
        }

        internal static async Task ExecuteMountAsyncSessionDisposeUnmountsAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsClient client = server.CreateClientBuilder().WithAuthSysCredentials("umount-test-host", 0, 0).Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<OpenNfsMountedExportV3Entry> mounted = await client.Exports.ListMountsV3Async(cancellationToken).ConfigureAwait(false);
            Require(mounted.Any(static entry => entry.ExportPath == EphemeralOpenNfsServer.ExportPath), "Expected MountAsync to register the mount with the server.");

            await session.DisposeAsync().ConfigureAwait(false);
            IReadOnlyList<OpenNfsMountedExportV3Entry> afterDispose = await client.Exports.ListMountsV3Async(cancellationToken).ConfigureAwait(false);
            Require(!afterDispose.Any(static entry => entry.ExportPath == EphemeralOpenNfsServer.ExportPath), "Expected disposing a MountAsync session to send UMNT.");

            await session.DisposeAsync().ConfigureAwait(false);
            session.Dispose();

            OpenNfsMountSession secondSession = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            await server.Application.StopAsync(cancellationToken).ConfigureAwait(false);
            DateTime started = DateTime.UtcNow;
            secondSession.Dispose();
            Require(DateTime.UtcNow - started < TimeSpan.FromSeconds(30), "Expected Dispose to finish promptly even when the server is gone.");
        }

        internal static async Task ExecuteEphemeralServerSnippetAsync(CancellationToken cancellationToken)
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.Snippet", Guid.NewGuid().ToString("N"));
            string exportDirectory = Path.Combine(stateDirectory, "export");
            Directory.CreateDirectory(exportDirectory);

            try
            {
                OpenNfsServerApplication app = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(stateDirectory, "handles.json")))
                    .AddExport("/export", exportDirectory)
                    .BuildApplication(new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = "127.0.0.1",
                        MountPort = 0,
                        NfsPort = 0,
                        EnableNfs40 = false,
                    });
                await using (app.ConfigureAwait(false))
                {
                    await app.StartAsync(cancellationToken).ConfigureAwait(false);
                    int mountPort = app.MountPort;
                    int nfsPort = app.NfsPort;
                    Require(app.IsRunning && mountPort > 0 && nfsPort > 0 && mountPort != nfsPort, "Expected the ephemeral server to bind distinct loopback MOUNT and NFS ports.");
                    Require(app.Nfs40Port == 0, "Expected NFSv4.0 to stay disabled.");

                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithPrimaryEndpoint("127.0.0.1", nfsPort)
                        .WithMountEndpoint("127.0.0.1", mountPort)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    await using (OpenNfsMountSession session = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false))
                    {
                        await session.Files.WriteAllBytesAsync("/hello.txt", Encoding.UTF8.GetBytes("hello"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                    }

                    Require(File.ReadAllText(Path.Combine(exportDirectory, "hello.txt")) == "hello", "Expected the snippet write to land in the served directory.");

                    await app.StopAsync(cancellationToken).ConfigureAwait(false);
                    Require(!app.IsRunning && app.MountPort == 0 && app.NfsPort == 0, "Expected StopAsync to release the listeners and reset the bound ports.");
                    Require(!IsListening(mountPort) && !IsListening(nfsPort), "Expected the stopped server to stop accepting connections.");
                }
            }
            finally
            {
                EphemeralOpenNfsServer.DeleteDirectory(stateDirectory);
            }
        }

        internal static async Task ExecuteFsInfoAdvertisesCanSetTimeAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
            await using (client.ConfigureAwait(false))
            await using (session.ConfigureAwait(false))
            {
                OpenNfsV3FileSystemInfoResult fileSystemInfo = await client.Files.GetFileSystemInfoV3Async(session.RootFileHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                Require(
                    fileSystemInfo.IsSuccess && (fileSystemInfo.Properties & OpenNfsV3FileSystemProperties.CanSetTime) == OpenNfsV3FileSystemProperties.CanSetTime,
                    "Expected FSINFO to advertise FSF3_CANSETTIME when the host supports attribute mutation.");
            }
        }

        internal static async Task ExecutePortmapperDiscoveryFindsPortsAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using InProcessPortmapper portmapper = InProcessPortmapper.Start(new Dictionary<(uint Program, uint Version, uint Protocol), uint>
            {
                [(100005, 3, 6)] = (uint)server.MountPort,
                [(100003, 3, 6)] = (uint)server.NfsPort,
            });

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1")
                .WithPortmapperDiscovery()
                .WithPortmapperPort(portmapper.Port)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            Require(client.DiscoveredMountEndpoint?.Port == server.MountPort, "Expected ConnectAsync to discover the MOUNT v3 port through the portmapper.");
            Require(client.DiscoveredNfsEndpoint?.Port == server.NfsPort, "Expected ConnectAsync to discover the NFSv3 port because the NFS port was not set explicitly.");

            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync("/discovered.txt", Encoding.UTF8.GetBytes("found"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync("/discovered.txt", cancellationToken).ConfigureAwait(false)) == "found", "Expected NFSv3 traffic to use the discovered port.");
            Require(portmapper.Queries.Count == 2, "Expected discovery to query the portmapper once per program, but observed " + portmapper.Queries.Count + " queries.");
        }

        internal static async Task ExecutePortmapperExplicitEndpointsWinAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            int bogusPort = GetUnusedPort();
            await using InProcessPortmapper portmapper = InProcessPortmapper.Start(new Dictionary<(uint Program, uint Version, uint Protocol), uint>
            {
                [(100005, 3, 6)] = (uint)bogusPort,
                [(100003, 3, 6)] = (uint)bogusPort,
            });

            await using OpenNfsClient bothExplicit = server.CreateClientBuilder()
                .WithPortmapperDiscovery()
                .WithPortmapperPort(portmapper.Port)
                .Build();
            await bothExplicit.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using (OpenNfsMountSession session = await bothExplicit.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false))
            {
                Require(await session.Metadata.ExistsAsync("/", cancellationToken).ConfigureAwait(false), "Expected explicit endpoints to be used.");
            }

            Require(bothExplicit.DiscoveredMountEndpoint is null && bothExplicit.DiscoveredNfsEndpoint is null, "Expected no discovered endpoints when both endpoints are explicit.");
            Require(portmapper.Queries.Count == 0, "Expected explicit mount and NFS endpoints to skip portmapper queries entirely.");

            await using OpenNfsClient mountExplicit = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1")
                .WithMountPort(server.MountPort)
                .WithPortmapperDiscovery()
                .WithPortmapperPort(portmapper.Port)
                .Build();
            await mountExplicit.ConnectAsync(cancellationToken).ConfigureAwait(false);
            Require(mountExplicit.DiscoveredMountEndpoint is null, "Expected an explicit mount port to take precedence over discovery.");
            Require(mountExplicit.DiscoveredNfsEndpoint?.Port == bogusPort, "Expected the NFS port to be discovered when it was not set explicitly.");
            OpenNfsMountV3Result mountResult = await mountExplicit.Exports.MountV3Async(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            Require(mountResult.IsSuccess, "Expected MOUNT to use the explicit mount port rather than the discovered one.");
        }

        internal static async Task ExecutePortmapperUnreachableFallsBackAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            int unreachablePortmapper = GetUnusedPort();

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", server.NfsPort)
                .WithPortmapperDiscovery()
                .WithPortmapperPort(unreachablePortmapper)
                .WithRetryPolicy(1, TimeSpan.FromMilliseconds(10))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            Require(client.DiscoveredMountEndpoint is null && client.DiscoveredNfsEndpoint is null, "Expected an unreachable portmapper to leave discovery empty.");

            OpenNfsClientException failure = await ExpectAsync<OpenNfsClientException>(
                () => client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken)).ConfigureAwait(false);
            Require(
                failure.Message.Contains("Portmapper discovery", StringComparison.Ordinal)
                    && failure.Message.Contains("WithMountEndpoint", StringComparison.Ordinal)
                    && failure.Message.Contains(":" + server.NfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                "Expected a clear fallback failure naming the portmapper and the attempted endpoint, but got: " + failure.Message);

            OpenNfsV3FileSystemInfoResult fileSystemInfo = await ExpectNoThrowFsInfoAsync(client, server, cancellationToken).ConfigureAwait(false);
            Require(fileSystemInfo.IsSuccess, "Expected NFSv3 traffic to keep using the explicit NFS endpoint after a failed discovery.");
        }

        internal static async Task ExecutePortmapperZeroPortFallsBackAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using InProcessPortmapper portmapper = InProcessPortmapper.Start(new Dictionary<(uint Program, uint Version, uint Protocol), uint>());

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", server.NfsPort)
                .WithPortmapperDiscovery()
                .WithPortmapperPort(portmapper.Port)
                .WithRetryPolicy(1, TimeSpan.FromMilliseconds(10))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            Require(client.DiscoveredMountEndpoint is null, "Expected port 0 to be treated as not registered.");
            Require(portmapper.Queries.Count == 1, "Expected only the MOUNT program to be queried when the NFS port is explicit.");

            OpenNfsClientException failure = await ExpectAsync<OpenNfsClientException>(
                () => client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken)).ConfigureAwait(false);
            Require(failure.Message.Contains("GETPORT returned 0", StringComparison.Ordinal), "Expected the fallback failure to explain that MOUNT is not registered, but got: " + failure.Message);
        }

        private static async Task<OpenNfsV3FileSystemInfoResult> ExpectNoThrowFsInfoAsync(OpenNfsClient client, EphemeralOpenNfsServer server, CancellationToken cancellationToken)
        {
            await using OpenNfsClient mountClient = server.CreateClientBuilder().Build();
            await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            OpenNfsMountV3Result mount = await mountClient.Exports.MountV3Async(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            return await client.Files.GetFileSystemInfoV3Async(mount.RootFileHandle.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        private static int GetUnusedPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static bool IsListening(int port)
        {
            try
            {
                using TcpClient probe = new TcpClient();
                probe.Connect(IPAddress.Loopback, port);
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}
