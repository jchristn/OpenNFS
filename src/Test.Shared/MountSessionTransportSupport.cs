namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Server;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Transport cases for the pooled, multiplexed client TCP connections: connection reuse, bounded concurrency, reconnect
    /// after a server restart, fail-fast on killed connections, per-call cancellation, and response timeouts on black-holed
    /// connections.
    /// </summary>
    internal static class MountSessionTransportSupport
    {
        internal static async Task ExecuteSequentialRpcsReuseConnectionsAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", server.NfsPort);
            await using OpenNfsClient client = CreateProxiedClient(server, proxy).Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            await session.Directories.CreateDirectoryAsync("/a/b", createParents: true, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync("/a/b/file.txt", Encoding.UTF8.GetBytes("pooled"), OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
            byte[] fileHandle = await session.ResolvePathHandleOrThrowAsync("/a/b/file.txt", "test", cancellationToken).ConfigureAwait(false);

            int rpcCount = 0;
            while (rpcCount < 2000)
            {
                OpenNfsV3GetAttributesResult attributes = await client.Files.GetAttributesV3Async(fileHandle, cancellationToken).ConfigureAwait(false);
                Require(attributes.IsSuccess && attributes.Attributes!.SizeBytes == 6, "Expected sequential GETATTR calls to succeed.");
                rpcCount++;

                if (rpcCount % 100 == 0)
                {
                    Require(await session.Metadata.ExistsAsync("/a/b/file.txt", cancellationToken).ConfigureAwait(false), "Expected sequential lookups to succeed.");
                    rpcCount += 3;
                }
            }

            OpenNfsRpcConnectionPool pool = GetPool(client);
            Require(proxy.AcceptedConnections <= pool.MaxConnectionsPerEndpoint, "Expected " + rpcCount + " sequential RPCs to use at most " + pool.MaxConnectionsPerEndpoint + " TCP connections, but the server accepted " + proxy.AcceptedConnections + ".");
            Require(proxy.AcceptedConnections == 1, "Expected strictly sequential RPCs to reuse a single connection, but the server accepted " + proxy.AcceptedConnections + ".");
        }

        internal static async Task ExecuteConcurrentRpcsShareBoundedConnectionsAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", server.NfsPort);
            await using OpenNfsClient client = CreateProxiedClient(server, proxy).Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            await session.Directories.CreateDirectoryAsync("/c", createParents: true, cancellationToken).ConfigureAwait(false);
            Task[] workers = Enumerable.Range(0, 32).Select(index => Task.Run(async () =>
            {
                string path = "/c/f" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin";
                byte[] payload = CreatePayload(5000 + index, seed: index);
                await session.Files.WriteAllBytesAsync(path, payload, OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
                for (int iteration = 0; iteration < 20; iteration++)
                {
                    byte[] readBack = await session.Files.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    Require(readBack.AsSpan().SequenceEqual(payload), "Expected concurrent reads over shared connections to return the right payload.");
                }
            }, cancellationToken)).ToArray();
            await Task.WhenAll(workers).ConfigureAwait(false);

            int limit = GetPool(client).MaxConnectionsPerEndpoint;
            Require(proxy.AcceptedConnections <= limit, "Expected 32-way concurrency to use at most " + limit + " connections, but the server accepted " + proxy.AcceptedConnections + ".");
        }

        internal static async Task ExecuteServerRestartReconnectsAsync(CancellationToken cancellationToken)
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.Restart", Guid.NewGuid().ToString("N"));
            string exportDirectory = Path.Combine(stateDirectory, "export");
            Directory.CreateDirectory(exportDirectory);

            try
            {
                OpenNfsServerApplication first = BuildApplication(stateDirectory, exportDirectory);
                await first.StartAsync(cancellationToken).ConfigureAwait(false);
                int mountPort = first.MountPort;

                await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", first.NfsPort);
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", proxy.Port)
                    .WithMountEndpoint("127.0.0.1", mountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

                await session.Files.WriteAllBytesAsync("/before.txt", Encoding.UTF8.GetBytes("before"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                int connectionsBeforeRestart = proxy.AcceptedConnections;

                await first.DisposeAsync().ConfigureAwait(false);
                OpenNfsServerApplication second = BuildApplication(stateDirectory, exportDirectory);
                await using (second.ConfigureAwait(false))
                {
                    await second.StartAsync(cancellationToken).ConfigureAwait(false);
                    proxy.Retarget("127.0.0.1", second.NfsPort);
                    await WaitUntilAsync(() => proxy.OpenConnections == 0, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                    Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync("/before.txt", cancellationToken).ConfigureAwait(false)) == "before", "Expected the first RPC after the server restart to reconnect transparently.");
                    await session.Files.WriteAllBytesAsync("/after.txt", Encoding.UTF8.GetBytes("after"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                    Require(File.ReadAllText(Path.Combine(exportDirectory, "after.txt")) == "after", "Expected non-idempotent writes after the restart to succeed on the new connection.");
                    Require(proxy.AcceptedConnections > connectionsBeforeRestart, "Expected the client to open a new connection after the restart.");
                }
            }
            finally
            {
                EphemeralOpenNfsServer.DeleteDirectory(stateDirectory);
            }
        }

        internal static async Task ExecuteKilledConnectionFailsInFlightFastAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", server.NfsPort);
            await using OpenNfsClient client = CreateProxiedClient(server, proxy)
                .WithResponseTimeout(TimeSpan.FromSeconds(60))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync("/k.bin", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] handle = await session.ResolvePathHandleOrThrowAsync("/k.bin", "test", cancellationToken).ConfigureAwait(false);

            proxy.BlackHoleReplies = true;
            Task<OpenNfsV3WriteResult>[] inFlight = Enumerable.Range(0, 6)
                .Select(index => client.Files.WriteV3Async(handle, (ulong)index, OpenNfsWriteStability.FileSync, new byte[] { 2 }, cancellationToken))
                .ToArray();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            Require(inFlight.All(static task => !task.IsCompleted), "Expected the black-holed WRITE calls to still be waiting.");

            Stopwatch stopwatch = Stopwatch.StartNew();
            proxy.KillAllConnections();
            foreach (Task<OpenNfsV3WriteResult> task in inFlight)
            {
                await ExpectAsync<OpenNfsClientIoException>(() => task).ConfigureAwait(false);
            }

            Require(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "Expected in-flight calls on a killed connection to fail fast, but they took " + stopwatch.Elapsed + ".");

            proxy.BlackHoleReplies = false;
            OpenNfsV3GetAttributesResult afterKill = await client.Files.GetAttributesV3Async(handle, cancellationToken).ConfigureAwait(false);
            Require(afterKill.IsSuccess, "Expected the next RPC after a killed connection to reconnect and succeed.");
        }

        internal static async Task ExecuteCancellingOneRpcKeepsOthersAsync(CancellationToken cancellationToken)
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.CancelOne", Guid.NewGuid().ToString("N"));
            string exportDirectory = Path.Combine(stateDirectory, "export");
            Directory.CreateDirectory(exportDirectory);
            File.WriteAllText(Path.Combine(exportDirectory, "slow.bin"), "slow");
            File.WriteAllText(Path.Combine(exportDirectory, "fast1.bin"), "fast1");
            File.WriteAllText(Path.Combine(exportDirectory, "fast2.bin"), "fast2");

            try
            {
                SlowPathFileSystem fileSystem = new SlowPathFileSystem("slow.bin", TimeSpan.FromSeconds(2));
                await using OpenNfsServerApplication app = new OpenNfsServerBuilder()
                    .UseFileSystem(fileSystem)
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(stateDirectory, "handles.json")))
                    .AddExport(EphemeralOpenNfsServer.ExportPath, exportDirectory)
                    .BuildApplication(new OpenNfsServerApplicationOptions { ListenerAddress = "127.0.0.1", MountPort = 0, NfsPort = 0, EnableNfs40 = false });
                await app.StartAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", app.NfsPort)
                    .WithMountEndpoint("127.0.0.1", app.MountPort)
                    .WithMaxConnectionsPerEndpoint(1)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

                fileSystem.Enabled = false;
                byte[] slowHandle = await session.ResolvePathHandleOrThrowAsync("/slow.bin", "test", cancellationToken).ConfigureAwait(false);
                byte[] fast1Handle = await session.ResolvePathHandleOrThrowAsync("/fast1.bin", "test", cancellationToken).ConfigureAwait(false);
                byte[] fast2Handle = await session.ResolvePathHandleOrThrowAsync("/fast2.bin", "test", cancellationToken).ConfigureAwait(false);
                fileSystem.Enabled = true;

                using CancellationTokenSource cancelSlow = new CancellationTokenSource();
                Task<OpenNfsV3GetAttributesResult> slow = client.Files.GetAttributesV3Async(slowHandle, cancelSlow.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                Task<OpenNfsV3GetAttributesResult> fast1 = client.Files.GetAttributesV3Async(fast1Handle, cancellationToken);
                Task<OpenNfsV3GetAttributesResult> fast2 = client.Files.GetAttributesV3Async(fast2Handle, cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
                cancelSlow.Cancel();

                await ExpectAsync<OperationCanceledException>(() => slow).ConfigureAwait(false);
                OpenNfsV3GetAttributesResult fast1Result = await fast1.ConfigureAwait(false);
                OpenNfsV3GetAttributesResult fast2Result = await fast2.ConfigureAwait(false);
                Require(fast1Result.IsSuccess && fast1Result.Attributes!.SizeBytes == 5, "Expected an RPC sharing the connection with a cancelled RPC to complete.");
                Require(fast2Result.IsSuccess && fast2Result.Attributes!.SizeBytes == 5, "Expected a second RPC sharing the connection with a cancelled RPC to complete.");

                OpenNfsV3GetAttributesResult followUp = await client.Files.GetAttributesV3Async(slowHandle, cancellationToken).ConfigureAwait(false);
                Require(followUp.IsSuccess && followUp.Attributes!.SizeBytes == 4, "Expected a later RPC to succeed after the cancelled call's late reply was discarded.");
            }
            finally
            {
                EphemeralOpenNfsServer.DeleteDirectory(stateDirectory);
            }
        }

        internal static async Task ExecuteResponseTimeoutOnBlackHoledConnectionAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", server.NfsPort);
            await using OpenNfsClient client = CreateProxiedClient(server, proxy)
                .WithResponseTimeout(TimeSpan.FromSeconds(1))
                .WithRetryPolicy(2, TimeSpan.FromMilliseconds(50))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync("/t.bin", new byte[] { 1, 2 }, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            byte[] handle = await session.ResolvePathHandleOrThrowAsync("/t.bin", "test", cancellationToken).ConfigureAwait(false);

            proxy.BlackHoleReplies = true;
            Stopwatch stopwatch = Stopwatch.StartNew();
            OpenNfsClientIoException timeout = await ExpectAsync<OpenNfsClientIoException>(
                () => client.Files.GetAttributesV3Async(handle, cancellationToken)).ConfigureAwait(false);
            Require(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Expected the response timeout to bound a black-holed call, but it took " + stopwatch.Elapsed + ".");
            Require(timeout.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase), "Expected a timeout failure, but got: " + timeout.Message);

            proxy.BlackHoleReplies = false;
            OpenNfsV3GetAttributesResult recovered = await client.Files.GetAttributesV3Async(handle, cancellationToken).ConfigureAwait(false);
            Require(recovered.IsSuccess && recovered.Attributes!.SizeBytes == 2, "Expected the client to replace the black-holed connection and succeed.");
            Require(proxy.AcceptedConnections >= 2, "Expected the black-holed connection to be retired and replaced.");
        }

        internal static async Task ExecuteIdleConnectionsCloseAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            await using ConnectionCountingTcpProxy proxy = ConnectionCountingTcpProxy.Start("127.0.0.1", server.NfsPort);
            await using OpenNfsClient client = CreateProxiedClient(server, proxy)
                .WithIdleConnectionTimeout(TimeSpan.FromSeconds(1))
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            Require(await session.Metadata.ExistsAsync("/", cancellationToken).ConfigureAwait(false) && await session.Metadata.ExistsAsync("/x", cancellationToken).ConfigureAwait(false) == false, "Expected lookups to succeed.");
            Require(proxy.OpenConnections == 1, "Expected one open pooled connection after the lookups.");
            await WaitUntilAsync(() => proxy.OpenConnections == 0, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Require(proxy.OpenConnections == 0, "Expected the idle pooled connection to be closed after the idle timeout.");

            Require(!await session.Metadata.ExistsAsync("/y", cancellationToken).ConfigureAwait(false), "Expected a new connection to be opened after an idle close.");
            Require(proxy.AcceptedConnections == 2, "Expected exactly one reconnect after the idle close.");

            await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await WaitUntilAsync(() => proxy.OpenConnections == 0, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Require(proxy.OpenConnections == 0, "Expected DisconnectAsync to close pooled connections.");
        }

        private static OpenNfsClientBuilder CreateProxiedClient(EphemeralOpenNfsServer server, ConnectionCountingTcpProxy proxy)
        {
            return new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", proxy.Port)
                .WithMountEndpoint("127.0.0.1", server.MountPort);
        }

        private static OpenNfsRpcConnectionPool GetPool(OpenNfsClient client)
        {
            return ((OpenNfsNetworkRpcExecutor)client.RpcExecutor).TcpPool;
        }

        private static OpenNfsServerApplication BuildApplication(string stateDirectory, string exportDirectory)
        {
            return new OpenNfsServerBuilder()
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(stateDirectory, "handles.json")))
                .AddExport(EphemeralOpenNfsServer.ExportPath, exportDirectory)
                .BuildApplication(new OpenNfsServerApplicationOptions { ListenerAddress = "127.0.0.1", MountPort = 0, NfsPort = 0, EnableNfs40 = false });
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!condition() && stopwatch.Elapsed < timeout)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Local file system that delays path-info resolution for one file name, used to hold an RPC on the server.
        /// </summary>
        private sealed class SlowPathFileSystem : INfsFileSystem
        {
            private readonly TimeSpan _delay;
            private readonly LocalNfsFileSystem _inner = LocalNfsFileSystem.Default;
            private readonly string _slowFileName;

            internal SlowPathFileSystem(string slowFileName, TimeSpan delay)
            {
                _slowFileName = slowFileName;
                _delay = delay;
            }

            internal bool Enabled { get; set; }

            public async Task<NfsGetPathInfoResponse> GetPathInfoAsync(NfsGetPathInfoRequest request)
            {
                if (Enabled && request.SourcePath.EndsWith(_slowFileName, StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(_delay).ConfigureAwait(false);
                }

                return await _inner.GetPathInfoAsync(request).ConfigureAwait(false);
            }

            public Task<NfsLookupPathResponse> LookupPathAsync(NfsLookupPathRequest request) => _inner.LookupPathAsync(request);

            public Task<NfsReadDirectoryResponse> ReadDirectoryAsync(NfsReadDirectoryRequest request) => _inner.ReadDirectoryAsync(request);

            public Task<NfsReadFileResponse> ReadFileAsync(NfsReadFileRequest request) => _inner.ReadFileAsync(request);

            public Task<NfsReadSymbolicLinkResponse> ReadSymbolicLinkAsync(NfsReadSymbolicLinkRequest request) => _inner.ReadSymbolicLinkAsync(request);

            public Task<NfsWriteFileResponse> WriteFileAsync(NfsWriteFileRequest request) => _inner.WriteFileAsync(request);

            public Task<NfsCommitFileResponse> CommitFileAsync(NfsCommitFileRequest request) => _inner.CommitFileAsync(request);

            public Task<NfsCreatePathResponse> CreatePathAsync(NfsCreatePathRequest request) => _inner.CreatePathAsync(request);

            public Task<NfsCreateSymbolicLinkResponse> CreateSymbolicLinkAsync(NfsCreateSymbolicLinkRequest request) => _inner.CreateSymbolicLinkAsync(request);

            public Task<NfsCreateHardLinkResponse> CreateHardLinkAsync(NfsCreateHardLinkRequest request) => _inner.CreateHardLinkAsync(request);

            public Task<NfsDeletePathResponse> DeletePathAsync(NfsDeletePathRequest request) => _inner.DeletePathAsync(request);

            public Task<NfsRenamePathResponse> RenamePathAsync(NfsRenamePathRequest request) => _inner.RenamePathAsync(request);
        }
    }
}
