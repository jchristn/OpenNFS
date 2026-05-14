namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.ExceptionServices;
    using System.Text;
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

    internal static class IdMapLoopbackCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "IdMapSuites",
                    caseId: "LinuxStyleOwnerMapping",
                    displayName: "Linux-style identity mapping normalizes owner strings and round-trips owner updates over NFSv4.0",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: ExecuteLinuxStyleOwnerMappingAsync),

                new TestCaseDescriptor(
                    suiteId: "IdMapSuites",
                    caseId: "IdentityMappingRequiresHostPolicy",
                    displayName: "Identity mapping surfaces ATTRNOTSUPP cleanly when the host exposes no identity policy",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: ExecuteIdentityMappingRequiresHostPolicyAsync),
            };
        }

        private static async Task ExecuteLinuxStyleOwnerMappingAsync(CancellationToken cancellationToken)
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
        }

        private static async Task ExecuteIdentityMappingRequiresHostPolicyAsync(CancellationToken cancellationToken)
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
