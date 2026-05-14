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
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the NFSv4.0 client suite catalog.
    /// </summary>
    internal static class ClientV40SuiteSupport
    {
        internal static OpenNfsCompoundOperation CreateLookupOperation(string entryName)
        {
            XdrWriter writer = new XdrWriter();
            new LOOKUP4args
            {
                objname = new component4
                {
                    Value = new utf8str_cs
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(entryName),
                        },
                    },
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_LOOKUP, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreatePutFileHandleOperation(byte[] fileHandle)
        {
            XdrWriter writer = new XdrWriter();
            new PUTFH4args
            {
                @object = new nfs_fh4
                {
                    Value = fileHandle,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTFH, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreatePutPublicFileHandleOperation()
        {
            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_PUTPUBFH, Array.Empty<byte>());
        }

        internal static OpenNfsCompoundOperation CreateVerifyOperation(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new VERIFY4args
            {
                obj_attributes = CreateTypeAttributes(fileType),
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_VERIFY, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreateNotVerifyOperation(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new NVERIFY4args
            {
                obj_attributes = CreateTypeAttributes(fileType),
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_NVERIFY, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreateOpenAttributeOperation(bool createdir)
        {
            XdrWriter writer = new XdrWriter();
            new OPENATTR4args
            {
                createdir = createdir,
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_OPENATTR, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreateDelegationPurgeOperation(ulong clientId)
        {
            XdrWriter writer = new XdrWriter();
            new DELEGPURGE4args
            {
                clientid = new clientid4
                {
                    Value = clientId,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_DELEGPURGE, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreateReleaseLockOwnerOperation(ulong clientId, byte[] ownerBytes)
        {
            XdrWriter writer = new XdrWriter();
            new RELEASE_LOCKOWNER4args
            {
                lock_owner = new lock_owner4
                {
                    clientid = new clientid4
                    {
                        Value = clientId,
                    },
                    owner = ownerBytes,
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_RELEASE_LOCKOWNER, writer.ToArray());
        }

        internal static OpenNfsCompoundOperation CreateReadDirectoryOperation(ulong cookie, byte[] cookieVerifier, uint maxCount)
        {
            XdrWriter writer = new XdrWriter();
            new READDIR4args
            {
                cookie = new nfs_cookie4
                {
                    Value = cookie,
                },
                cookieverf = new verifier4
                {
                    Value = cookieVerifier,
                },
                dircount = new count4
                {
                    Value = maxCount,
                },
                maxcount = new count4
                {
                    Value = maxCount,
                },
                attr_request = new bitmap4
                {
                    Value = new[]
                    {
                        (1U << (int)Nfs40Constants.FATTR4_TYPE)
                        | (1U << (int)Nfs40Constants.FATTR4_CHANGE)
                        | (1U << (int)Nfs40Constants.FATTR4_SIZE)
                        | (1U << (int)Nfs40Constants.FATTR4_FILEHANDLE),
                    },
                },
            }.WriteTo(writer);

            return new OpenNfsCompoundOperation((uint)nfs_opnum4.OP_READDIR, writer.ToArray());
        }

        internal static fattr4 CreateTypeAttributes(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new fattr4_type
            {
                Value = fileType,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_TYPE),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        internal static OpenNfsServer CreateServer(
            TestNfsIdMapper? idMapper = null,
            TestNfsAcls? acls = null,
            TestNfsDelegations? delegations = null)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\exports\docs\shortcut"] = NfsPathKind.SymbolicLink,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\shortcut"] = "notes.txt",
                });

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports");

            if (idMapper is not null)
            {
                builder.UseIdMapper(idMapper);
            }

            if (acls is not null)
            {
                builder.UseAcls(acls);
            }

            if (delegations is not null)
            {
                builder.UseDelegations(delegations);
            }

            return builder.Build();
        }

        internal static OpenNfsServer CreateCrossExportServer()
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\exports\docs\shortcut"] = NfsPathKind.SymbolicLink,
                    [@"C:\other"] = NfsPathKind.Directory,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\shortcut"] = "notes.txt",
                });

            return new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .AddExport("/other", @"C:\other")
                .Build();
        }

        internal static async Task<LockingServerContext> CreateLockingServerAsync(
            CancellationToken cancellationToken)
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                });

            await fileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    Encoding.UTF8.GetBytes("hello-v4"),
                    NfsWriteStability.FileSync,
                    cancellationToken)).ConfigureAwait(false);

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .Build();

            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                cancellationToken).ConfigureAwait(false);

            return new LockingServerContext(server, docsHandle, noteHandle);
        }

        internal readonly struct LockingServerContext
        {
            internal LockingServerContext(OpenNfsServer server, NfsFileHandle docsHandle, NfsFileHandle noteHandle)
            {
                Server = server;
                DocsHandle = docsHandle;
                NoteHandle = noteHandle;
            }

            internal OpenNfsServer Server { get; }

            internal NfsFileHandle DocsHandle { get; }

            internal NfsFileHandle NoteHandle { get; }
        }

        internal static async Task RunAgainstLoopbackServiceAsync(
            OpenNfsServer server,
            int expectedCallCount,
            Func<OpenNfsClient, Task> runClient,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(server);
            await RunAgainstLoopbackServiceAsync(
                new Nfs40CompoundService(server),
                expectedCallCount,
                runClient,
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task RunAgainstLoopbackServiceAsync(
            Nfs40CompoundService service,
            int expectedCallCount,
            Func<OpenNfsClient, Task> runClient,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(runClient);

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
                                Interlocked.Increment(ref actualCallCount);
                                using NetworkStream stream = acceptedClient.GetStream();
                                RpcTcpTransport transport = new RpcTcpTransport(
                                    stream,
                                    new RpcTransportOptions(
                                        timeouts: new RpcTransportTimeouts(
                                            readTimeout: TimeSpan.FromSeconds(5),
                                            writeTimeout: TimeSpan.FromSeconds(5))));

                                RpcMessageEnvelope request = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                                RpcMessageEnvelope reply = await service.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
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
                        .Build();
                    await client.OpenAsync(cancellationToken).ConfigureAwait(false);
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
                    throw new InvalidOperationException($"Expected {expectedCallCount} NFSv4.0 loopback calls but observed {actualCallCount}.");
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
