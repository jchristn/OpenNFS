namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V42.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Runtime lifetime and mounted-session bootstrap flows for the public client surface suites.
    /// </summary>
    internal static class ClientSurfaceRuntimeSupport
    {
        internal static Task ExecuteRpcSecGssAuthOnlyMountAgainstKerberosProbeAsync(CancellationToken cancellationToken)
        {
            return SecuritySuiteSupport.RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: public client rpcsecgss exports OK",
                    "PROBE: public client rpcsecgss mount OK",
                    "PROBE: public client rpcsecgss read OK",
                    "PROBE: public client rpcsecgss auth-only round-trip OK",
                },
                cancellationToken);
        }

        internal static async Task ExecuteMountAsyncUsesDedicatedMountEndpointAsync(CancellationToken cancellationToken)
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

        internal static async Task ExecuteRawV42CompoundAgainstPublicServerApplicationAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientRawV42", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourceRoot);
                await File.WriteAllBytesAsync(
                    Path.Combine(sourceRoot, "hello.txt"),
                    Encoding.UTF8.GetBytes("client-v42"),
                    cancellationToken).ConfigureAwait(false);

                await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .AddExport("/export", sourceRoot)
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            EnableNfs42 = true,
                            Nfs42Port = 0,
                        });

                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                if (application.Nfs42Port < 1)
                {
                    throw new InvalidOperationException("Expected OpenNfsServerApplication to bind a real NFSv4.2 listener port.");
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", application.Nfs42Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                ulong clientId = await ExecuteExchangeIdV42Async(client, cancellationToken).ConfigureAwait(false);
                byte[] sessionId = await ExecuteCreateSessionV42Async(client, clientId, cancellationToken).ConfigureAwait(false);

                OpenNfsCompoundReply firstReply = await client.ExecuteCompoundAsync(
                    new OpenNfsCompoundRequest(
                        OpenNfsProtocolVersion.Nfs42,
                        "client-v42-first",
                        new OpenNfsCompoundOperation[]
                        {
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_SEQUENCE,
                                EncodeV42Payload(BuildSequenceArguments(sessionId, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 0).WriteTo)),
                            new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_LOOKUP,
                                EncodeV42Payload(BuildLookupArguments("hello.txt").WriteTo)),
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_IO_ADVISE,
                                EncodeV42Payload(BuildIoAdviseArguments().WriteTo)),
                        }),
                    OpenNfsOperationIdempotency.NonIdempotent,
                    cancellationToken).ConfigureAwait(false);

                COMPOUND4res firstCompound = ReadV42CompoundReply(firstReply);
                if (firstCompound.status != nfsstat4.NFS4_OK
                    || firstCompound.resarray is null
                    || firstCompound.resarray.Length != 4
                    || firstCompound.resarray[0].opsequence?.sr_status != nfsstat4.NFS4_OK
                    || firstCompound.resarray[1].opputrootfh?.status != nfsstat4.NFS4_OK
                    || firstCompound.resarray[2].oplookup?.status != nfsstat4.NFS4_OK
                    || firstCompound.resarray[3].opio_advise?.ior_status != nfsstat4.NFS4_OK)
                {
                    throw new InvalidOperationException("Expected the public raw client surface to execute a successful NFSv4.2 SEQUENCE + PUTROOTFH + LOOKUP + IO_ADVISE flow against OpenNfsServerApplication.");
                }

                OpenNfsCompoundReply secondReply = await client.ExecuteCompoundAsync(
                    new OpenNfsCompoundRequest(
                        OpenNfsProtocolVersion.Nfs42,
                        "client-v42-second",
                        new OpenNfsCompoundOperation[]
                        {
                            new OpenNfsCompoundOperation(
                                (uint)nfs_opnum4.OP_SEQUENCE,
                                EncodeV42Payload(BuildSequenceArguments(sessionId, slotId: 0, sequenceId: 2, cacheThis: true, highestSlotId: 0).WriteTo)),
                        }),
                    OpenNfsOperationIdempotency.NonIdempotent,
                    cancellationToken).ConfigureAwait(false);

                COMPOUND4res secondCompound = ReadV42CompoundReply(secondReply);
                if (secondCompound.status != nfsstat4.NFS4_OK
                    || secondCompound.resarray is null
                    || secondCompound.resarray.Length != 1
                    || secondCompound.resarray[0].opsequence?.sr_status != nfsstat4.NFS4_OK
                    || secondCompound.resarray[0].opsequence?.sr_resok4?.sr_sequenceid?.Value != 2U)
                {
                    throw new InvalidOperationException("Expected the public raw client surface to preserve NFSv4.2 slot-sequence advancement across consecutive SEQUENCE requests.");
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

        internal static async Task ExecuteGroupedV42FileApisAgainstPublicServerApplicationAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.ClientGroupedV42", Guid.NewGuid().ToString("N"));
            string mappingPath = Path.Combine(rootDirectory, "handles.json");
            string sourcePath = @"C:\exports\source.bin";
            string destinationPath = @"C:\exports\destination.bin";

            try
            {
                Directory.CreateDirectory(rootDirectory);

                CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                    new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                    {
                        [@"C:\exports"] = NfsPathKind.Directory,
                        [sourcePath] = NfsPathKind.File,
                        [destinationPath] = NfsPathKind.File,
                    });
                NfsV42SuiteSupport.FakeCopyCloneHost copyClone = new NfsV42SuiteSupport.FakeCopyCloneHost();
                await fileSystem.WriteFileAsync(
                    new NfsWriteFileRequest(
                        sourcePath,
                        0UL,
                        Encoding.UTF8.GetBytes("grouped-v42-source"),
                        NfsWriteStability.FileSync,
                        cancellationToken)).ConfigureAwait(false);
                await fileSystem.WriteFileAsync(
                    new NfsWriteFileRequest(
                        destinationPath,
                        0UL,
                        Encoding.UTF8.GetBytes("destination-seed"),
                        NfsWriteStability.FileSync,
                        cancellationToken)).ConfigureAwait(false);

                await using OpenNfsServerApplication application = new OpenNfsServerBuilder()
                    .UseFileSystem(fileSystem)
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                    .UseSparseFiles(fileSystem)
                    .UseCopyClone(copyClone)
                    .AddExport("/", @"C:\exports")
                    .BuildApplication(
                        new OpenNfsServerApplicationOptions
                        {
                            EnableNfs42 = true,
                            Nfs42Port = 0,
                        });

                await application.StartAsync(cancellationToken).ConfigureAwait(false);

                if (application.Nfs42Port < 1)
                {
                    throw new InvalidOperationException("Expected OpenNfsServerApplication to bind a real NFSv4.2 listener port for grouped helper coverage.");
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", application.Nfs42Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                ulong clientId = await ExecuteExchangeIdV42Async(client, cancellationToken).ConfigureAwait(false);
                byte[] sessionId = await ExecuteCreateSessionV42Async(client, clientId, cancellationToken).ConfigureAwait(false);
                byte[] sourceFileHandle = await LookupFileHandleV42Async(client, sessionId, "source.bin", cancellationToken).ConfigureAwait(false);
                byte[] destinationFileHandle = await LookupFileHandleV42Async(client, sessionId, "destination.bin", cancellationToken).ConfigureAwait(false);

                OpenNfsV42IoAdviseResult ioAdvise = await client.Files.IoAdviseV42Async(
                    sourceFileHandle,
                    0UL,
                    4096UL,
                    new[] { OpenNfsV42IoAdviceHint.Sequential },
                    cancellationToken).ConfigureAwait(false);
                if (!ioAdvise.IsSuccess || ioAdvise.AcknowledgedHints.Count != 0)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 IO_ADVISE helpers to establish and reuse a client-scoped session while surfacing the acknowledged-hints bitmap.");
                }

                OpenNfsV42ReadPlusResult readPlus = await client.Files.ReadPlusV42Async(
                    sourceFileHandle,
                    0UL,
                    16U,
                    cancellationToken).ConfigureAwait(false);
                if (!readPlus.IsSuccess
                    || readPlus.EndOfFile
                    || readPlus.Segments.Count != 1
                    || readPlus.Segments[0].Kind != OpenNfsV42ReadPlusSegmentKind.Data
                    || readPlus.Segments[0].Offset != 0UL
                    || readPlus.Segments[0].Length != 16UL)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 READ_PLUS helpers to surface the sparse-read content segments returned by the current public server surface.");
                }

                OpenNfsV42SeekResult seek = await client.Files.SeekV42Async(
                    sourceFileHandle,
                    0UL,
                    OpenNfsV42SeekTarget.Data,
                    cancellationToken).ConfigureAwait(false);
                if (!seek.IsSuccess || seek.Offset != 0UL || seek.EndOfFile)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 SEEK helpers to surface the typed seek result returned by the current public server surface.");
                }

                OpenNfsV42AllocateResult allocate = await client.Files.AllocateV42Async(
                    sourceFileHandle,
                    0UL,
                    64UL,
                    cancellationToken).ConfigureAwait(false);
                if (!allocate.IsSuccess)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 ALLOCATE helpers to succeed against the sparse-capable public server surface.");
                }

                OpenNfsV42DeallocateResult deallocate = await client.Files.DeallocateV42Async(
                    sourceFileHandle,
                    0UL,
                    64UL,
                    cancellationToken).ConfigureAwait(false);
                if (!deallocate.IsSuccess)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 DEALLOCATE helpers to succeed against the sparse-capable public server surface.");
                }

                OpenNfsV42CopyResult copy = await client.Files.CopyV42Async(
                    sourceFileHandle,
                    destinationFileHandle,
                    1UL,
                    3UL,
                    5UL,
                    consecutive: true,
                    synchronous: true,
                    cancellationToken).ConfigureAwait(false);
                if (!copy.IsSuccess
                    || copy.BytesCopied != 5UL
                    || copy.CommittedStability != OpenNfsWriteStability.FileSync
                    || copy.Verifier.Length != 8
                    || copy.RequiresConsecutive
                    || copy.RequiresSynchronous)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 COPY helpers to surface the typed byte-count, commit-stability, verifier, and response-requirement fields returned by the public server surface.");
                }

                if (copyClone.CopyRequests.Count != 1
                    || !string.Equals(copyClone.CopyRequests[0].SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(copyClone.CopyRequests[0].DestinationPath, destinationPath, StringComparison.OrdinalIgnoreCase)
                    || copyClone.CopyRequests[0].SourceOffset != 1UL
                    || copyClone.CopyRequests[0].DestinationOffset != 3UL
                    || copyClone.CopyRequests[0].Count != 5UL
                    || !copyClone.CopyRequests[0].Synchronous)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 COPY helpers to preserve the requested source/destination handles, byte ranges, and synchronous-copy flag when routed through the public server surface.");
                }

                OpenNfsV42CloneResult clone = await client.Files.CloneV42Async(
                    sourceFileHandle,
                    destinationFileHandle,
                    2UL,
                    4UL,
                    6UL,
                    cancellationToken).ConfigureAwait(false);
                if (!clone.IsSuccess)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 CLONE helpers to succeed against the copy/clone-capable public server surface.");
                }

                if (copyClone.CloneRequests.Count != 1
                    || !string.Equals(copyClone.CloneRequests[0].SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(copyClone.CloneRequests[0].DestinationPath, destinationPath, StringComparison.OrdinalIgnoreCase)
                    || copyClone.CloneRequests[0].SourceOffset != 2UL
                    || copyClone.CloneRequests[0].DestinationOffset != 4UL
                    || copyClone.CloneRequests[0].Count != 6UL)
                {
                    throw new InvalidOperationException("Expected grouped NFSv4.2 CLONE helpers to preserve the requested source/destination handles and byte ranges when routed through the public server surface.");
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

        internal static async Task ExecuteGroupedV42FileApisReuseSessionAcrossCallsAsync(CancellationToken cancellationToken)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task listenerTask = RunGroupedV42SessionReuseListenerAsync(listener, cancellationToken);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", listenerPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] fileHandle = new byte[] { 1, 2, 3, 4 };

                OpenNfsV42IoAdviseResult ioAdvise = await client.Files.IoAdviseV42Async(
                    fileHandle,
                    0UL,
                    4096UL,
                    new[] { OpenNfsV42IoAdviceHint.Sequential },
                    cancellationToken).ConfigureAwait(false);
                if (!ioAdvise.IsSuccess || ioAdvise.AcknowledgedHints.Count != 0)
                {
                    throw new InvalidOperationException("Expected the reusable grouped NFSv4.2 session path to execute IO_ADVISE successfully on its first sequenced call.");
                }

                OpenNfsV42SeekResult seek = await client.Files.SeekV42Async(
                    fileHandle,
                    64UL,
                    OpenNfsV42SeekTarget.Hole,
                    cancellationToken).ConfigureAwait(false);
                if (!seek.IsSuccess || seek.Offset != 128UL || seek.EndOfFile)
                {
                    throw new InvalidOperationException("Expected the reusable grouped NFSv4.2 session path to preserve the established session and advance SEQUENCE on the second grouped call.");
                }
            }
            finally
            {
                listener.Stop();
                await listenerTask.ConfigureAwait(false);
            }
        }

        internal static async Task ExecuteGroupedV42FileApisReconnectAfterTransportBreakAsync(CancellationToken cancellationToken)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int listenerPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task listenerTask = RunGroupedV42SessionReconnectListenerAsync(listener, cancellationToken);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", listenerPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] fileHandle = new byte[] { 1, 2, 3, 4 };

                OpenNfsV42IoAdviseResult ioAdvise = await client.Files.IoAdviseV42Async(
                    fileHandle,
                    0UL,
                    4096UL,
                    new[] { OpenNfsV42IoAdviceHint.Sequential },
                    cancellationToken).ConfigureAwait(false);
                if (!ioAdvise.IsSuccess || ioAdvise.AcknowledgedHints.Count != 0)
                {
                    throw new InvalidOperationException("Expected the grouped NFSv4.2 reconnect path to succeed on the first sequenced call before the transport break.");
                }

                OpenNfsV42SeekResult seek = await client.Files.SeekV42Async(
                    fileHandle,
                    64UL,
                    OpenNfsV42SeekTarget.Hole,
                    cancellationToken).ConfigureAwait(false);
                if (!seek.IsSuccess || seek.Offset != 128UL || seek.EndOfFile)
                {
                    throw new InvalidOperationException("Expected the grouped NFSv4.2 reconnect path to bind the replacement connection and preserve SEQUENCE on the replayed second call.");
                }
            }
            finally
            {
                listener.Stop();
                await listenerTask.ConfigureAwait(false);
            }
        }

        internal static async Task ExecuteMountAsyncThrowsForDeniedMountAsync(CancellationToken cancellationToken)
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

        internal static async Task ExecuteTypedCredentialMountAsyncRejectsFlavorMismatchAsync(CancellationToken cancellationToken)
        {
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", 1)
                .Build();

            bool thrown = false;
            try
            {
                _ = await client.MountAsync(
                    "/export",
                    OpenNfsClientCredential.Anonymous,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OpenNfsClientStateException exception)
            {
                if (exception.Category != OpenNfsErrorCategory.Unsupported)
                {
                    throw new InvalidOperationException(
                        "Flavor-mismatch on MountAsync(typed-credential) must surface OpenNfsErrorCategory.Unsupported. Observed: "
                        + exception.Category);
                }

                if (!exception.Message.Contains("flavor", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The flavor-mismatch failure message must mention 'flavor' for actionable diagnostics.");
                }

                thrown = true;
            }

            if (!thrown)
            {
                throw new InvalidOperationException("MountAsync(typed-credential) must reject a flavor mismatch with OpenNfsClientStateException.");
            }

            bool nullThrown = false;
            try
            {
                _ = await client.MountAsync("/export", credential: null!, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentNullException)
            {
                nullThrown = true;
            }

            if (!nullThrown)
            {
                throw new InvalidOperationException("MountAsync(string, null, CancellationToken) must throw ArgumentNullException.");
            }
        }

        internal static async Task ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync(CancellationToken cancellationToken)
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

        private static async Task<ulong> ExecuteExchangeIdV42Async(OpenNfsClient client, CancellationToken cancellationToken)
        {
            OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                new OpenNfsCompoundRequest(
                    OpenNfsProtocolVersion.Nfs42,
                    "client-v42-exchange-id",
                    new OpenNfsCompoundOperation[]
                    {
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_EXCHANGE_ID,
                            EncodeV42Payload(BuildExchangeIdArguments(verifierByte: 0x42, ownerSeed: 0x24).WriteTo)),
                    }),
                OpenNfsOperationIdempotency.NonIdempotent,
                cancellationToken).ConfigureAwait(false);

            COMPOUND4res compound = ReadV42CompoundReply(reply);
            if (compound.status != nfsstat4.NFS4_OK
                || compound.resarray is null
                || compound.resarray.Length != 1
                || compound.resarray[0].opexchange_id?.eir_status != nfsstat4.NFS4_OK
                || compound.resarray[0].opexchange_id?.eir_resok4?.eir_clientid is null)
            {
                throw new InvalidOperationException("Expected the public raw client surface to complete NFSv4.2 EXCHANGE_ID successfully.");
            }

            EXCHANGE_ID4resok exchangeResult = compound.resarray[0].opexchange_id!.eir_resok4!;
            return exchangeResult.eir_clientid!.Value;
        }

        private static async Task<byte[]> ExecuteCreateSessionV42Async(
            OpenNfsClient client,
            ulong clientId,
            CancellationToken cancellationToken)
        {
            OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                new OpenNfsCompoundRequest(
                    OpenNfsProtocolVersion.Nfs42,
                    "client-v42-create-session",
                    new OpenNfsCompoundOperation[]
                    {
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_CREATE_SESSION,
                            EncodeV42Payload(BuildCreateSessionArguments(clientId, sequenceId: 1, requestedSlots: 4).WriteTo)),
                    }),
                OpenNfsOperationIdempotency.NonIdempotent,
                cancellationToken).ConfigureAwait(false);

            COMPOUND4res compound = ReadV42CompoundReply(reply);
            if (compound.status != nfsstat4.NFS4_OK
                || compound.resarray is null
                || compound.resarray.Length != 1
                || compound.resarray[0].opcreate_session?.csr_status != nfsstat4.NFS4_OK
                || compound.resarray[0].opcreate_session?.csr_resok4?.csr_sessionid is null)
            {
                throw new InvalidOperationException("Expected the public raw client surface to complete NFSv4.2 CREATE_SESSION successfully.");
            }

            CREATE_SESSION4resok createSessionResult = compound.resarray[0].opcreate_session!.csr_resok4!;
            byte[] sessionId = createSessionResult.csr_sessionid!.Value!;
            byte[] copy = new byte[sessionId.Length];
            Buffer.BlockCopy(sessionId, 0, copy, 0, sessionId.Length);
            return copy;
        }

        private static COMPOUND4res ReadV42CompoundReply(OpenNfsCompoundReply reply)
        {
            byte[] payload = reply.ReadAcceptedSuccessProcedurePayload();
            XdrReader reader = new XdrReader(payload);
            COMPOUND4res compound = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return compound;
        }

        private static byte[] EncodeV42Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }

        private static EXCHANGE_ID4args BuildExchangeIdArguments(byte verifierByte, byte ownerSeed)
        {
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4
                    {
                        Value = new byte[] { verifierByte, verifierByte, verifierByte, verifierByte, verifierByte, verifierByte, verifierByte, verifierByte },
                    },
                    co_ownerid = new byte[] { ownerSeed, (byte)(ownerSeed + 1), (byte)(ownerSeed + 2), (byte)(ownerSeed + 3) },
                },
                eia_flags = 0U,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        private static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            channel_attrs4 channelAttributes = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0 },
                ca_maxrequestsize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new count4 { Value = 64 * 1024 },
                ca_maxoperations = new count4 { Value = 16 },
                ca_maxrequests = new count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4 { Value = clientId },
                csa_sequence = new sequenceid4 { Value = sequenceId },
                csa_flags = 0U,
                csa_fore_chan_attrs = channelAttributes,
                csa_back_chan_attrs = channelAttributes,
                csa_cb_program = 0U,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        private static SEQUENCE4args BuildSequenceArguments(
            byte[] sessionIdBytes,
            uint slotId,
            uint sequenceId,
            bool cacheThis,
            uint highestSlotId)
        {
            return new SEQUENCE4args
            {
                sa_sessionid = new sessionid4 { Value = sessionIdBytes },
                sa_sequenceid = new sequenceid4 { Value = sequenceId },
                sa_slotid = new slotid4 { Value = slotId },
                sa_highest_slotid = new slotid4 { Value = highestSlotId },
                sa_cachethis = cacheThis,
            };
        }

        private static async Task RunGroupedV42SessionReuseListenerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            const ulong clientId = 0x1234UL;
            byte[] sessionIdBytes = new byte[]
            {
                0x10, 0x11, 0x12, 0x13,
                0x20, 0x21, 0x22, 0x23,
                0x30, 0x31, 0x32, 0x33,
                0x40, 0x41, 0x42, 0x43,
            };

            try
            {
                using TcpClient tcpClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                using NetworkStream stream = tcpClient.GetStream();
                RpcTcpTransport transport = new RpcTcpTransport(stream);

                ReceivedV42CompoundRequest exchangeId = await ReceiveV42CompoundRequestAsync(transport, cancellationToken).ConfigureAwait(false);
                ValidateExchangeIdRequest(exchangeId);
                await SendV42CompoundReplyAsync(
                    transport,
                    exchangeId.Xid,
                    BuildExchangeIdReply(exchangeId.Arguments.tag, clientId),
                    cancellationToken).ConfigureAwait(false);

                ReceivedV42CompoundRequest createSession = await ReceiveV42CompoundRequestAsync(transport, cancellationToken).ConfigureAwait(false);
                ValidateCreateSessionRequest(createSession, clientId);
                await SendV42CompoundReplyAsync(
                    transport,
                    createSession.Xid,
                    BuildCreateSessionReply(createSession.Arguments.tag, sessionIdBytes),
                    cancellationToken).ConfigureAwait(false);

                ReceivedV42CompoundRequest ioAdvise = await ReceiveV42CompoundRequestAsync(transport, cancellationToken).ConfigureAwait(false);
                ValidateIoAdviseRequest(ioAdvise, sessionIdBytes);
                await SendV42CompoundReplyAsync(
                    transport,
                    ioAdvise.Xid,
                    BuildIoAdviseReply(ioAdvise.Arguments.tag, sessionIdBytes, sequenceId: 1U),
                    cancellationToken).ConfigureAwait(false);

                ReceivedV42CompoundRequest seek = await ReceiveV42CompoundRequestAsync(transport, cancellationToken).ConfigureAwait(false);
                ValidateSeekRequest(seek, sessionIdBytes);
                await SendV42CompoundReplyAsync(
                    transport,
                    seek.Xid,
                    BuildSeekReply(seek.Arguments.tag, sessionIdBytes, sequenceId: 2U, offset: 128UL),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static async Task RunGroupedV42SessionReconnectListenerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            const ulong clientId = 0x1234UL;
            byte[] sessionIdBytes = new byte[]
            {
                0x50, 0x51, 0x52, 0x53,
                0x60, 0x61, 0x62, 0x63,
                0x70, 0x71, 0x72, 0x73,
                0x80, 0x81, 0x82, 0x83,
            };

            try
            {
                using (TcpClient firstClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false))
                using (NetworkStream firstStream = firstClient.GetStream())
                {
                    RpcTcpTransport firstTransport = new RpcTcpTransport(firstStream);

                    ReceivedV42CompoundRequest exchangeId = await ReceiveV42CompoundRequestAsync(firstTransport, cancellationToken).ConfigureAwait(false);
                    ValidateExchangeIdRequest(exchangeId);
                    await SendV42CompoundReplyAsync(
                        firstTransport,
                        exchangeId.Xid,
                        BuildExchangeIdReply(exchangeId.Arguments.tag, clientId),
                        cancellationToken).ConfigureAwait(false);

                    ReceivedV42CompoundRequest createSession = await ReceiveV42CompoundRequestAsync(firstTransport, cancellationToken).ConfigureAwait(false);
                    ValidateCreateSessionRequest(createSession, clientId);
                    await SendV42CompoundReplyAsync(
                        firstTransport,
                        createSession.Xid,
                        BuildCreateSessionReply(createSession.Arguments.tag, sessionIdBytes),
                        cancellationToken).ConfigureAwait(false);

                    ReceivedV42CompoundRequest ioAdvise = await ReceiveV42CompoundRequestAsync(firstTransport, cancellationToken).ConfigureAwait(false);
                    ValidateIoAdviseRequest(ioAdvise, sessionIdBytes);
                    await SendV42CompoundReplyAsync(
                        firstTransport,
                        ioAdvise.Xid,
                        BuildIoAdviseReply(ioAdvise.Arguments.tag, sessionIdBytes, sequenceId: 1U),
                        cancellationToken).ConfigureAwait(false);
                }

                using TcpClient secondClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                using NetworkStream secondStream = secondClient.GetStream();
                RpcTcpTransport secondTransport = new RpcTcpTransport(secondStream);

                ReceivedV42CompoundRequest bindConnection = await ReceiveV42CompoundRequestAsync(secondTransport, cancellationToken).ConfigureAwait(false);
                ValidateBindConnectionRequest(bindConnection, sessionIdBytes);
                await SendV42CompoundReplyAsync(
                    secondTransport,
                    bindConnection.Xid,
                    BuildBindConnectionReply(bindConnection.Arguments.tag, sessionIdBytes),
                    cancellationToken).ConfigureAwait(false);

                ReceivedV42CompoundRequest seek = await ReceiveV42CompoundRequestAsync(secondTransport, cancellationToken).ConfigureAwait(false);
                ValidateSeekRequest(seek, sessionIdBytes);
                await SendV42CompoundReplyAsync(
                    secondTransport,
                    seek.Xid,
                    BuildSeekReply(seek.Arguments.tag, sessionIdBytes, sequenceId: 2U, offset: 128UL),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static COMPOUND4res BuildBindConnectionReply(utf8str_cs? tag, byte[] sessionIdBytes)
        {
            return new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = tag,
                resarray = new[]
                {
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                        opbind_conn_to_session = new BIND_CONN_TO_SESSION4res
                        {
                            bctsr_status = nfsstat4.NFS4_OK,
                            bctsr_resok4 = new BIND_CONN_TO_SESSION4resok
                            {
                                bctsr_sessid = new sessionid4
                                {
                                    Value = sessionIdBytes,
                                },
                                bctsr_dir = channel_dir_from_server4.CDFS4_FORE,
                                bctsr_use_conn_in_rdma_mode = false,
                            },
                        },
                    },
                },
            };
        }

        private static COMPOUND4res BuildCreateSessionReply(utf8str_cs? tag, byte[] sessionIdBytes)
        {
            channel_attrs4 channelAttributes = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0U },
                ca_maxrequestsize = new count4 { Value = 1024U * 1024U },
                ca_maxresponsesize = new count4 { Value = 1024U * 1024U },
                ca_maxresponsesize_cached = new count4 { Value = 64U * 1024U },
                ca_maxoperations = new count4 { Value = 16U },
                ca_maxrequests = new count4 { Value = 4U },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = tag,
                resarray = new[]
                {
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_CREATE_SESSION,
                        opcreate_session = new CREATE_SESSION4res
                        {
                            csr_status = nfsstat4.NFS4_OK,
                            csr_resok4 = new CREATE_SESSION4resok
                            {
                                csr_sessionid = new sessionid4 { Value = sessionIdBytes },
                                csr_sequence = new sequenceid4 { Value = 1U },
                                csr_flags = 0U,
                                csr_fore_chan_attrs = channelAttributes,
                                csr_back_chan_attrs = channelAttributes,
                            },
                        },
                    },
                },
            };
        }

        private static COMPOUND4res BuildExchangeIdReply(utf8str_cs? tag, ulong clientId)
        {
            return new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = tag,
                resarray = new[]
                {
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_EXCHANGE_ID,
                        opexchange_id = new EXCHANGE_ID4res
                        {
                            eir_status = nfsstat4.NFS4_OK,
                            eir_resok4 = new EXCHANGE_ID4resok
                            {
                                eir_clientid = new clientid4 { Value = clientId },
                                eir_sequenceid = new sequenceid4 { Value = 1U },
                                eir_flags = 0U,
                                eir_state_protect = new state_protect4_r { spr_how = state_protect_how4.SP4_NONE },
                                eir_server_owner = new server_owner4
                                {
                                    so_minor_id = 7UL,
                                    so_major_id = new byte[] { 0xAA, 0xBB, 0xCC },
                                },
                                eir_server_scope = new byte[] { 0x01, 0x02 },
                                eir_server_impl_id = Array.Empty<nfs_impl_id4>(),
                            },
                        },
                    },
                },
            };
        }

        private static COMPOUND4res BuildIoAdviseReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId)
        {
            return new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = tag,
                resarray = new[]
                {
                    BuildSequenceResult(sessionIdBytes, sequenceId),
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_PUTFH,
                        opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                    },
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_IO_ADVISE,
                        opio_advise = new IO_ADVISE4res
                        {
                            ior_status = nfsstat4.NFS4_OK,
                            resok4 = new IO_ADVISE4resok
                            {
                                ior_hints = new bitmap4 { Value = Array.Empty<uint>() },
                            },
                        },
                    },
                },
            };
        }

        private static COMPOUND4res BuildSeekReply(utf8str_cs? tag, byte[] sessionIdBytes, uint sequenceId, ulong offset)
        {
            return new COMPOUND4res
            {
                status = nfsstat4.NFS4_OK,
                tag = tag,
                resarray = new[]
                {
                    BuildSequenceResult(sessionIdBytes, sequenceId),
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_PUTFH,
                        opputfh = new PUTFH4res { status = nfsstat4.NFS4_OK },
                    },
                    new nfs_resop4
                    {
                        resop = nfs_opnum4.OP_SEEK,
                        opseek = new SEEK4res
                        {
                            sa_status = nfsstat4.NFS4_OK,
                            resok4 = new seek_res4
                            {
                                sr_offset = new offset4 { Value = offset },
                                sr_eof = false,
                            },
                        },
                    },
                },
            };
        }

        private static nfs_resop4 BuildSequenceResult(byte[] sessionIdBytes, uint sequenceId)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_SEQUENCE,
                opsequence = new SEQUENCE4res
                {
                    sr_status = nfsstat4.NFS4_OK,
                    sr_resok4 = new SEQUENCE4resok
                    {
                        sr_sessionid = new sessionid4 { Value = sessionIdBytes },
                        sr_sequenceid = new sequenceid4 { Value = sequenceId },
                        sr_slotid = new slotid4 { Value = 0U },
                        sr_highest_slotid = new slotid4 { Value = 3U },
                        sr_target_highest_slotid = new slotid4 { Value = 3U },
                        sr_status_flags = 0U,
                    },
                },
            };
        }

        private static async Task<ReceivedV42CompoundRequest> ReceiveV42CompoundRequestAsync(
            RpcTcpTransport transport,
            CancellationToken cancellationToken)
        {
            RpcMessageEnvelope envelope = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            XdrReader reader = new XdrReader(envelope.ProcedurePayload);
            COMPOUND4args arguments = COMPOUND4args.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return new ReceivedV42CompoundRequest(envelope.Header.xid, arguments);
        }

        private static async Task SendV42CompoundReplyAsync(
            RpcTcpTransport transport,
            uint xid,
            COMPOUND4res compoundReply,
            CancellationToken cancellationToken)
        {
            XdrWriter responseWriter = new XdrWriter();
            compoundReply.WriteTo(responseWriter);
            RpcMessageEnvelope replyEnvelope = RpcMessageFactory.CreateAcceptedReply(
                xid,
                accept_stat.SUCCESS,
                RpcAuthenticationCodec.CreateNone(),
                responseWriter.ToArray());
            await transport.SendAsync(replyEnvelope, cancellationToken).ConfigureAwait(false);
        }

        private static void ValidateCreateSessionRequest(ReceivedV42CompoundRequest request, ulong expectedClientId)
        {
            if (request.Arguments.minorversion != 2U
                || request.Arguments.argarray is null
                || request.Arguments.argarray.Length != 1
                || request.Arguments.argarray[0].argop != nfs_opnum4.OP_CREATE_SESSION
                || request.Arguments.argarray[0].opcreate_session?.csa_clientid?.Value != expectedClientId
                || request.Arguments.argarray[0].opcreate_session?.csa_sequence?.Value != 1U)
            {
                throw new InvalidOperationException("Expected the reusable grouped NFSv4.2 session path to issue a single CREATE_SESSION request immediately after EXCHANGE_ID.");
            }
        }

        private static void ValidateBindConnectionRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
        {
            if (request.Arguments.minorversion != 2U
                || request.Arguments.argarray is null
                || request.Arguments.argarray.Length != 1
                || request.Arguments.argarray[0].argop != nfs_opnum4.OP_BIND_CONN_TO_SESSION
                || request.Arguments.argarray[0].opbind_conn_to_session?.bctsa_sessid?.Value is null
                || !request.Arguments.argarray[0].opbind_conn_to_session!.bctsa_sessid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
            {
                throw new InvalidOperationException("Expected the grouped NFSv4.2 reconnect path to send a single BIND_CONN_TO_SESSION request for the established session after the transport break.");
            }
        }

        private static void ValidateExchangeIdRequest(ReceivedV42CompoundRequest request)
        {
            if (request.Arguments.minorversion != 2U
                || request.Arguments.argarray is null
                || request.Arguments.argarray.Length != 1
                || request.Arguments.argarray[0].argop != nfs_opnum4.OP_EXCHANGE_ID)
            {
                throw new InvalidOperationException("Expected the reusable grouped NFSv4.2 session path to begin with a single EXCHANGE_ID request.");
            }
        }

        private static void ValidateIoAdviseRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
        {
            if (request.Arguments.minorversion != 2U
                || request.Arguments.argarray is null
                || request.Arguments.argarray.Length != 3
                || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
                || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
                || request.Arguments.argarray[2].argop != nfs_opnum4.OP_IO_ADVISE
                || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 1U
                || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
                || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
            {
                throw new InvalidOperationException("Expected the first grouped NFSv4.2 helper call to send SEQUENCE(1) + PUTFH + IO_ADVISE on the established session.");
            }
        }

        private static void ValidateSeekRequest(ReceivedV42CompoundRequest request, byte[] expectedSessionId)
        {
            if (request.Arguments.minorversion != 2U
                || request.Arguments.argarray is null
                || request.Arguments.argarray.Length != 3
                || request.Arguments.argarray[0].argop != nfs_opnum4.OP_SEQUENCE
                || request.Arguments.argarray[1].argop != nfs_opnum4.OP_PUTFH
                || request.Arguments.argarray[2].argop != nfs_opnum4.OP_SEEK
                || request.Arguments.argarray[0].opsequence?.sa_sequenceid?.Value != 2U
                || request.Arguments.argarray[0].opsequence?.sa_sessionid?.Value is null
                || !request.Arguments.argarray[0].opsequence!.sa_sessionid!.Value!.AsSpan().SequenceEqual(expectedSessionId))
            {
                throw new InvalidOperationException("Expected the second grouped NFSv4.2 helper call to reuse the same session and advance SEQUENCE to 2.");
            }
        }

        private sealed class ReceivedV42CompoundRequest
        {
            internal ReceivedV42CompoundRequest(uint xid, COMPOUND4args arguments)
            {
                Xid = xid;
                Arguments = arguments;
            }

            internal uint Xid { get; }

            internal COMPOUND4args Arguments { get; }
        }

        private static LOOKUP4args BuildLookupArguments(string entryName)
        {
            return new LOOKUP4args
            {
                objname = new component4
                {
                    Value = new utf8str_cs
                    {
                        Value = new utf8string { Value = Encoding.UTF8.GetBytes(entryName) },
                    },
                },
            };
        }

        private static async Task<byte[]> LookupFileHandleV42Async(
            OpenNfsClient client,
            byte[] sessionId,
            string entryName,
            CancellationToken cancellationToken)
        {
            OpenNfsCompoundReply reply = await client.ExecuteCompoundAsync(
                new OpenNfsCompoundRequest(
                    OpenNfsProtocolVersion.Nfs42,
                    "client-v42-getfh",
                    new OpenNfsCompoundOperation[]
                    {
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_SEQUENCE,
                            EncodeV42Payload(BuildSequenceArguments(sessionId, slotId: 0, sequenceId: 1, cacheThis: true, highestSlotId: 0).WriteTo)),
                        new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTROOTFH, Array.Empty<byte>()),
                        new OpenNfsCompoundOperation(
                            (uint)nfs_opnum4.OP_LOOKUP,
                            EncodeV42Payload(BuildLookupArguments(entryName).WriteTo)),
                        new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_GETFH, Array.Empty<byte>()),
                    }),
                OpenNfsOperationIdempotency.NonIdempotent,
                cancellationToken).ConfigureAwait(false);

            COMPOUND4res compound = ReadV42CompoundReply(reply);
            if (compound.status != nfsstat4.NFS4_OK
                || compound.resarray is null
                || compound.resarray.Length != 4
                || compound.resarray[0].opsequence?.sr_status != nfsstat4.NFS4_OK
                || compound.resarray[1].opputrootfh?.status != nfsstat4.NFS4_OK
                || compound.resarray[2].oplookup?.status != nfsstat4.NFS4_OK
                || compound.resarray[3].opgetfh?.status != nfsstat4.NFS4_OK
                || compound.resarray[3].opgetfh?.resok4?.@object is null)
            {
                throw new InvalidOperationException("Expected the raw NFSv4.2 lookup helper to resolve a stable filehandle for grouped client coverage.");
            }

            byte[] fileHandle = compound.resarray[3].opgetfh!.resok4!.@object!.Value!;
            byte[] copy = new byte[fileHandle.Length];
            Buffer.BlockCopy(fileHandle, 0, copy, 0, fileHandle.Length);
            return copy;
        }

        private static IO_ADVISE4args BuildIoAdviseArguments()
        {
            return new IO_ADVISE4args
            {
                iaa_stateid = new stateid4 { seqid = 1, other = new byte[12] },
                iaa_offset = new offset4 { Value = 0 },
                iaa_count = new length4 { Value = 4096 },
                iaa_hints = new bitmap4 { Value = Array.Empty<uint>() },
            };
        }
    }
}
