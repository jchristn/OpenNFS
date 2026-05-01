namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering client and server identity-mapping services.
    /// </summary>
    public static class IdMapSuites
    {
        /// <summary>
        /// Creates the shared identity-mapping suite catalog.
        /// </summary>
        public static TestSuiteDescriptor Create()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestSuiteDescriptor(
                suiteId: "IdMapSuites",
                displayName: "Identity Mapping Surface",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "IdMapSuites",
                        caseId: "LinuxStyleOwnerMapping",
                        displayName: "Linux-style identity mapping normalizes owner strings and round-trips owner updates over NFSv4.0",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            TestNfsIdMapper idMapper = new TestNfsIdMapper(
                                owner: "alice@example.test",
                                ownerGroup: "authors@example.test");
                            OpenNfsServer server = CreateServer(idMapper);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 6,
                                async client =>
                                {
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetIdentityResult initialIdentityResult = await client.Identity.GetOwnerAndGroupV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        "bob@example.test",
                                        "writers@example.test",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetIdentityResult rereadIdentityResult = await client.Identity.GetOwnerAndGroupV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    NfsGetIdentityResponse hostIdentity = await idMapper.GetIdentityAsync(
                                        new NfsGetIdentityRequest(
                                            @"C:\exports\notes.txt",
                                            NfsPathKind.File,
                                            cancellationToken)).ConfigureAwait(false);

                                    if (!rootLookup.IsSuccess
                                        || rootLookup.ObjectFileHandle.Length == 0
                                        || !noteLookup.IsSuccess
                                        || noteLookup.ObjectFileHandle.Length == 0
                                        || !initialIdentityResult.IsSuccess
                                        || initialIdentityResult.Identity is null
                                        || !string.Equals(initialIdentityResult.Identity.ServerOwner, "alice@example.test", StringComparison.Ordinal)
                                        || !string.Equals(initialIdentityResult.Identity.ServerOwnerGroup, "authors@example.test", StringComparison.Ordinal)
                                        || !string.Equals(initialIdentityResult.Identity.ClientOwner, "alice", StringComparison.Ordinal)
                                        || !string.Equals(initialIdentityResult.Identity.ClientOwnerGroup, "authors", StringComparison.Ordinal)
                                        || !setIdentityResult.IsSuccess
                                        || setIdentityResult.Identity is null
                                        || !ContainsAttributeId(setIdentityResult.SetAttributeMaskWords, (int)OpenNfsV40AttributeKind.Owner)
                                        || !ContainsAttributeId(setIdentityResult.SetAttributeMaskWords, (int)OpenNfsV40AttributeKind.OwnerGroup)
                                        || !string.Equals(setIdentityResult.Identity.ServerOwner, "bob@example.test", StringComparison.Ordinal)
                                        || !string.Equals(setIdentityResult.Identity.ServerOwnerGroup, "writers@example.test", StringComparison.Ordinal)
                                        || !string.Equals(setIdentityResult.Identity.ClientOwner, "bob", StringComparison.Ordinal)
                                        || !string.Equals(setIdentityResult.Identity.ClientOwnerGroup, "writers", StringComparison.Ordinal)
                                        || !rereadIdentityResult.IsSuccess
                                        || rereadIdentityResult.Identity is null
                                        || !string.Equals(rereadIdentityResult.Identity.ServerOwner, "bob@example.test", StringComparison.Ordinal)
                                        || !string.Equals(rereadIdentityResult.Identity.ServerOwnerGroup, "writers@example.test", StringComparison.Ordinal)
                                        || !string.Equals(hostIdentity.Owner, "bob@example.test", StringComparison.Ordinal)
                                        || !string.Equals(hostIdentity.OwnerGroup, "writers@example.test", StringComparison.Ordinal))
                                    {
                                        throw new InvalidOperationException("Expected the Linux-style identity service to normalize realm-qualified owner strings and preserve owner/group round-trips over the public NFSv4.0 path.");
                                    }
                                },
                                new OpenNfsLinuxStyleIdentityPolicy(),
                                cancellationToken).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "IdMapSuites",
                        caseId: "LinuxMountedOwnerMappingRoundTrip",
                        displayName: "A Linux NFSv4.0 kernel client maps owner strings and round-trips chown against the sample server",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountedOwnerMappingRoundTripAsync),

                    new TestCaseDescriptor(
                        suiteId: "IdMapSuites",
                        caseId: "LinuxMountedOwnerMappingRequiresLocalPrincipals",
                        displayName: "A Linux NFSv4.0 kernel client does not surface friendly owner names when the local mapping principals are missing",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxMountedOwnerMappingRequiresLocalPrincipalsAsync),

                    new TestCaseDescriptor(
                        suiteId: "IdMapSuites",
                        caseId: "IdentityMappingRequiresHostPolicy",
                        displayName: "Identity mapping surfaces ATTRNOTSUPP cleanly when the host exposes no identity policy",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateServer(idMapper: null);

                            await RunAgainstLoopbackServiceAsync(
                                server,
                                expectedCallCount: 4,
                                async client =>
                                {
                                    OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(
                                        rootLookup.ObjectFileHandle.ToArray(),
                                        "notes.txt",
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40GetIdentityResult getIdentityResult = await client.Identity.GetOwnerAndGroupV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        cancellationToken).ConfigureAwait(false);
                                    OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
                                        noteLookup.ObjectFileHandle.ToArray(),
                                        "carol@example.test",
                                        "reviewers@example.test",
                                        cancellationToken).ConfigureAwait(false);

                                    if (!rootLookup.IsSuccess
                                        || !noteLookup.IsSuccess
                                        || getIdentityResult.Status != OpenNfsV40Status.AttributeNotSupported
                                        || setIdentityResult.Status != OpenNfsV40Status.AttributeNotSupported)
                                    {
                                        throw new InvalidOperationException("Expected identity reads and writes to surface ATTRNOTSUPP when the host does not expose an identity-mapping policy.");
                                    }
                                },
                                OpenNfsPassthroughIdentityPolicy.Default,
                                cancellationToken).ConfigureAwait(false);
                        }),
                },
                beforeSuiteAsync: probe.IsAvailable
                    ? cancellationToken => new ValueTask(DockerInteropImages.EnsureBuiltAsync(cancellationToken))
                    : null);
        }

        private static bool ContainsAttributeId(IReadOnlyList<uint> maskWords, int attributeId)
        {
            int wordIndex = attributeId / 32;
            int bitIndex = attributeId % 32;
            if (wordIndex < 0 || wordIndex >= maskWords.Count)
            {
                return false;
            }

            return (maskWords[wordIndex] & (1U << bitIndex)) != 0U;
        }

        private static OpenNfsServer CreateServer(TestNfsIdMapper? idMapper)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\notes.txt"] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\notes.txt"] = Encoding.UTF8.GetBytes("hello-idmap"),
                });

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports");

            if (idMapper is not null)
            {
                builder.UseIdMapper(idMapper);
            }

            return builder.Build();
        }

        private static async Task ExecuteLinuxMountedOwnerMappingRoundTripAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.IdMapLinuxPositive", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    owner: "alice@example.test",
                    ownerGroup: "authors@example.test",
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                    CreateLinuxV40OwnerMappingCommand(
                        sampleServer.Nfs40Port,
                        domain: "example.test",
                        createLocalPrincipals: true,
                        includeChownRoundTrip: true),
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel NFSv4.0 client failed to mount or exercise owner mapping against the sample server."
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
                if (!combinedOutput.Contains("BEFORE=alice:authors", StringComparison.Ordinal)
                    || !combinedOutput.Contains("AFTER=bob:writers", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFSv4.0 client to map owner strings to local principals and round-trip chown values. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", sampleServer.Nfs40Port)
                    .WithIdentityPolicy(new OpenNfsLinuxStyleIdentityPolicy())
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] nestedHandle = await ResolveSampleNestedHandleV40Async(client, cancellationToken).ConfigureAwait(false);
                OpenNfsV40GetIdentityResult identityResult = await client.Identity.GetOwnerAndGroupV40Async(
                    nestedHandle,
                    cancellationToken).ConfigureAwait(false);

                if (!identityResult.IsSuccess
                    || identityResult.Identity is null
                    || string.IsNullOrWhiteSpace(identityResult.Identity.ServerOwner)
                    || string.IsNullOrWhiteSpace(identityResult.Identity.ServerOwnerGroup)
                    || string.Equals(identityResult.Identity.ServerOwner, "alice@example.test", StringComparison.Ordinal)
                    || string.Equals(identityResult.Identity.ServerOwnerGroup, "authors@example.test", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the sample server to retain a changed Linux-mounted owner/group update through the public identity surface, but observed status '"
                        + identityResult.Status
                        + "', server owner '"
                        + identityResult.Identity?.ServerOwner
                        + "', server group '"
                        + identityResult.Identity?.ServerOwnerGroup
                        + "', client owner '"
                        + identityResult.Identity?.ClientOwner
                        + "', and client group '"
                        + identityResult.Identity?.ClientOwnerGroup
                        + "'.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(rootDirectory);
            }
        }

        private static async Task ExecuteLinuxMountedOwnerMappingRequiresLocalPrincipalsAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.IdMapLinuxNegative", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    owner: "alice@example.test",
                    ownerGroup: "authors@example.test",
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                    CreateLinuxV40OwnerMappingCommand(
                        sampleServer.Nfs40Port,
                        domain: "example.test",
                        createLocalPrincipals: false,
                        includeChownRoundTrip: false),
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel NFSv4.0 client failed before it could demonstrate the missing-principal owner-mapping path."
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
                if (!combinedOutput.Contains("BEFORE=", StringComparison.Ordinal)
                    || combinedOutput.Contains("BEFORE=alice:authors", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFSv4.0 client not to surface friendly owner/group names when the local mapping principals are missing. Output was:"
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
                DeleteDirectoryIfPresent(rootDirectory);
            }
        }

        private static string CreateLinuxV40OwnerMappingCommand(
            int nfs40Port,
            string domain,
            bool createLocalPrincipals,
            bool includeChownRoundTrip)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("set -eu; ");
            builder.Append("mkdir -p /var/lib/nfs/rpc_pipefs /mnt/opennfs; ");
            builder.Append("mount -t rpc_pipefs sunrpc /var/lib/nfs/rpc_pipefs >/dev/null 2>&1 || true; ");
            builder.Append("cat >/etc/idmapd.conf <<'EOF'\n");
            builder.Append("[General]\n");
            builder.Append("Domain = ");
            builder.Append(domain);
            builder.Append("\nEOF\n");

            if (createLocalPrincipals)
            {
                builder.Append("addgroup -S authors >/dev/null 2>&1 || true; ");
                builder.Append("adduser -D -G authors alice >/dev/null 2>&1 || true; ");
                builder.Append("addgroup -S writers >/dev/null 2>&1 || true; ");
                builder.Append("adduser -D -G writers bob >/dev/null 2>&1 || true; ");
            }

            builder.Append("rpc.idmapd >/dev/null 2>&1 || true; ");
            builder.Append("sleep 1; ");
            builder.Append("mount -t nfs4 -o vers=4.0,minorversion=0,port=");
            builder.Append(nfs40Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",soft,timeo=10,retrans=1 host.docker.internal:/ /mnt/opennfs; ");
            builder.Append("stat -c 'BEFORE=%U:%G' /mnt/opennfs/docs/nested.txt; ");

            if (includeChownRoundTrip)
            {
                builder.Append("chown bob:writers /mnt/opennfs/docs/nested.txt; ");
                builder.Append("sync; ");
                builder.Append("for attempt in 1 2 3 4 5; do ");
                builder.Append("if stat -c 'AFTER=%U:%G' /mnt/opennfs/docs/nested.txt; then break; fi; ");
                builder.Append("if [ \"$attempt\" = \"5\" ]; then exit 1; fi; ");
                builder.Append("sleep 1; ");
                builder.Append("done; ");
            }

            builder.Append("umount /mnt/opennfs; ");
            builder.Append("umount /var/lib/nfs/rpc_pipefs >/dev/null 2>&1 || true");
            return builder.ToString();
        }

        private static async Task<byte[]> ResolveSampleNestedHandleV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootLookup.IsSuccess || rootLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to return a usable NFSv4.0 root filehandle.");
            }

            OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                rootLookup.ObjectFileHandle.ToArray(),
                "docs",
                cancellationToken).ConfigureAwait(false);
            if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to resolve the 'docs' directory over NFSv4.0.");
            }

            OpenNfsV40LookupResult nestedLookup = await client.Directories.LookupV40Async(
                docsLookup.ObjectFileHandle.ToArray(),
                "nested.txt",
                cancellationToken).ConfigureAwait(false);
            if (!nestedLookup.IsSuccess || nestedLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to resolve 'docs/nested.txt' over NFSv4.0.");
            }

            return nestedLookup.ObjectFileHandle.ToArray();
        }

        private static async Task WriteSampleConfigurationAsync(
            string configPath,
            string owner,
            string ownerGroup,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(owner);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGroup);

            string? configDirectory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            string sourceRoot = Path.Combine(configDirectory ?? string.Empty, "content", "export", "docs");
            Directory.CreateDirectory(sourceRoot);
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "nested.txt"),
                "hello-idmap",
                cancellationToken).ConfigureAwait(false);

            string configurationJson = JsonSerializer.Serialize(
                new
                {
                    serverName = "IdMap Sample",
                    exportPath = "/exports/sample",
                    sourcePath = "content/export",
                    owner = owner,
                    ownerGroup = ownerGroup,
                    mappingPath = "state/handles.json",
                    listenerAddress = "0.0.0.0",
                    mountPort = 20048,
                    nfsPort = 2049,
                    nfs40Port = 3049,
                    denyMounts = false,
                });

            await File.WriteAllTextAsync(configPath, configurationJson, cancellationToken).ConfigureAwait(false);
        }

        private static void DeleteDirectoryIfPresent(string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        private static async Task RunAgainstLoopbackServiceAsync(
            OpenNfsServer server,
            int expectedCallCount,
            Func<OpenNfsClient, Task> runClient,
            IOpenNfsClientIdentityPolicy identityPolicy,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(runClient);
            ArgumentNullException.ThrowIfNull(identityPolicy);

            Nfs40CompoundService service = new Nfs40CompoundService(server);
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using CancellationTokenSource serverCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int actualCallCount = 0;
            ExceptionDispatchInfo? clientFailure = null;

            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Task serverTask = Task.Run(
                    async () =>
                    {
                        try
                        {
                            while (!serverCancellationSource.IsCancellationRequested)
                            {
                                using TcpClient acceptedClient = await listener.AcceptTcpClientAsync(serverCancellationSource.Token).ConfigureAwait(false);
                                System.Threading.Interlocked.Increment(ref actualCallCount);
                                using NetworkStream stream = acceptedClient.GetStream();
                                RpcTcpTransport transport = new RpcTcpTransport(
                                    stream,
                                    new RpcTransportOptions(
                                        timeouts: new RpcTransportTimeouts(
                                            readTimeout: TimeSpan.FromSeconds(5),
                                            writeTimeout: TimeSpan.FromSeconds(5))));

                                OpenNFS.Rpc.RpcMessages.RpcMessageEnvelope request =
                                    await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                OpenNFS.Rpc.RpcMessages.RpcMessageEnvelope reply =
                                    await service.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
                                await transport.SendAsync(reply, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        catch (OperationCanceledException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                        catch (ObjectDisposedException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                        catch (SocketException) when (serverCancellationSource.IsCancellationRequested)
                        {
                        }
                    },
                    CancellationToken.None);

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithPrimaryEndpoint(IPAddress.Loopback.ToString(), port)
                        .WithIdentityPolicy(identityPolicy)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    await runClient(client).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    clientFailure = ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    serverCancellationSource.Cancel();
                    listener.Stop();
                    await serverTask.ConfigureAwait(false);
                }

                if (clientFailure is null && actualCallCount != expectedCallCount)
                {
                    throw new InvalidOperationException("Expected " + expectedCallCount + " loopback identity call(s) but observed " + actualCallCount + ".");
                }

                clientFailure?.Throw();
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
