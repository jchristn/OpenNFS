namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the sample-server and filehandle suite catalog.
    /// </summary>
    internal static class SampleServerSuiteSupport
    {
        internal static Task ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerArtifactSupport.ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync(cancellationToken);
        }

        internal static Task ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerArtifactSupport.ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync(cancellationToken);
        }

        internal static Task ExecuteLinuxMountReadWriteAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerArtifactSupport.ExecuteLinuxMountReadWriteAsync(cancellationToken);
        }

        internal static Task ExecuteLinuxMountDeniedAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerArtifactSupport.ExecuteLinuxMountDeniedAsync(cancellationToken);
        }

        internal static Task ExecutePersistentFileHandleRestartAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerPersistenceSupport.ExecutePersistentFileHandleRestartAsync(cancellationToken);
        }

        internal static Task ExecutePersistentFileHandleRestartNegativeAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerPersistenceSupport.ExecutePersistentFileHandleRestartNegativeAsync(cancellationToken);
        }

        internal static Task ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerPersistenceSupport.ExecuteCapabilitySurfaceRoundTripsAndPersistsAcrossRestartAsync(cancellationToken);
        }

        internal static Task ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerPersistenceSupport.ExecuteCapabilitySurfaceReportsNegativeLockAndLookupPathsAsync(cancellationToken);
        }

        internal static Task ExecuteKerberosMountAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerKerberosSupport.ExecuteKerberosMountAsync(cancellationToken);
        }

        internal static Task ExecuteKerberosMountNotConfiguredAsync(System.Threading.CancellationToken cancellationToken)
        {
            return SampleServerKerberosSupport.ExecuteKerberosMountNotConfiguredAsync(cancellationToken);
        }
        internal static TcpListener CreateReservedListener()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            return listener;
        }

        internal static string CreateLinuxReadOnlyMountCommand(
            int mountPort,
            int nfsPort,
            string exportPath,
            string primaryFilePath,
            string nestedFilePath)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs; ",
                "cat /mnt/opennfs/", primaryFilePath, "; ",
                "cat /mnt/opennfs/", nestedFilePath, "; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        internal static string CreateLinuxSampleMountCommand(int mountPort, int nfsPort)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:/exports/sample /mnt/opennfs; ",
                "cat /mnt/opennfs/hello.txt; ",
                "cat /mnt/opennfs/docs/nested.txt; ",
                "printf 'UPDATED-FROM-LINUX-CLIENT' | dd of=/mnt/opennfs/hello.txt conv=notrunc status=none; ",
                "sync; ",
                "for attempt in 1 2 3 4 5; do ",
                "if cat /mnt/opennfs/hello.txt; then break; fi; ",
                "if [ \"$attempt\" = \"5\" ]; then exit 1; fi; ",
                "sleep 1; ",
                "done; ",
                "ls -1 /mnt/opennfs; ",
                "ls -1 /mnt/opennfs/docs; ",
                "umount /mnt/opennfs");
        }

        internal static async Task<SampleHandleResolution> ResolveSampleHandlesAsync(
            SampleOpenNfsServerProcess process,
            System.Threading.CancellationToken cancellationToken)
        {
            await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", process.MountPort)
                .Build();
            await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsMountV3Result mountResult =
                await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
            if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to return a usable MOUNT v3 root filehandle.");
            }

            byte[] rootHandle = mountResult.RootFileHandle.ToArray();

            await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", process.NfsPort)
                .Build();
            await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV3LookupResult lookupResult =
                await nfsClient.Directories.LookupV3Async(rootHandle, "hello.txt", cancellationToken).ConfigureAwait(false);
            if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to resolve 'hello.txt' to a usable filehandle.");
            }

            return new SampleHandleResolution(rootHandle, lookupResult.ObjectFileHandle.ToArray());
        }

        internal static async Task<SampleV40HandleResolution> ResolveSampleV40HandlesAsync(
            OpenNfsClient client,
            System.Threading.CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootLookup.IsSuccess || rootLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to return a usable NFSv4.0 root filehandle.");
            }

            byte[] rootHandle = rootLookup.ObjectFileHandle.ToArray();
            OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(rootHandle, "docs", cancellationToken).ConfigureAwait(false);
            if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to resolve 'docs' over the NFSv4.0 client path.");
            }

            byte[] docsHandle = docsLookup.ObjectFileHandle.ToArray();
            OpenNfsV40LookupResult nestedLookup = await client.Directories.LookupV40Async(docsHandle, "nested.txt", cancellationToken).ConfigureAwait(false);
            if (!nestedLookup.IsSuccess || nestedLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample artifact to resolve 'nested.txt' over the NFSv4.0 client path.");
            }

            return new SampleV40HandleResolution(rootHandle, docsHandle, nestedLookup.ObjectFileHandle.ToArray());
        }

        internal readonly struct SampleHandleResolution
        {
            internal SampleHandleResolution(byte[] rootHandle, byte[] helloFileHandle)
            {
                RootHandle = rootHandle;
                HelloFileHandle = helloFileHandle;
            }

            internal byte[] RootHandle { get; }

            internal byte[] HelloFileHandle { get; }
        }

        internal readonly struct SampleV40HandleResolution
        {
            internal SampleV40HandleResolution(byte[] rootHandle, byte[] docsHandle, byte[] nestedHandle)
            {
                RootHandle = rootHandle;
                DocsHandle = docsHandle;
                NestedHandle = nestedHandle;
            }

            internal byte[] RootHandle { get; }

            internal byte[] DocsHandle { get; }

            internal byte[] NestedHandle { get; }
        }

        internal static async Task WriteSampleConfigurationAsync(
            string configPath,
            object configuration,
            System.Threading.CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<auth_stat> SendRpcSecGssDataNullCallAsync(int mountPort, System.Threading.CancellationToken cancellationToken)
        {
            byte[] handle = new byte[]
            {
                0x4F, 0x70, 0x65, 0x6E, 0x4E, 0x46, 0x53, 0x2E,
                0x53, 0x61, 0x6D, 0x70, 0x6C, 0x65, 0x4B, 0x52,
            };

            RpcSecGssCredentialBody credentialBody = new RpcSecGssCredentialBody(
                version: RpcSecGssProtocolConstants.Version,
                procedure: RpcSecGssProcedure.Data,
                sequenceNumber: 1,
                service: RpcSecGssService.Integrity,
                contextHandle: handle);
            opaque_auth credential = RpcSecGssCredentialCodec.Write(credentialBody);
            opaque_auth verifier = new opaque_auth
            {
                flavor = auth_flavor.AUTH_NONE,
                body = Array.Empty<byte>(),
            };

            RpcMessageEnvelope call = RpcMessageFactory.CreateCall(
                xid: 0xC0FEEC0F,
                program: (uint)MOUNT_PROGRAM_Program.Program,
                version: (uint)MOUNT_PROGRAM_Program.Version_MOUNT_V3,
                procedure: (uint)MOUNT_PROGRAM_Program.Procedure_MOUNT_V3_MOUNTPROC3_NULL,
                credential: credential,
                verifier: verifier,
                procedurePayload: ReadOnlyMemory<byte>.Empty);

            using TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, mountPort, cancellationToken).ConfigureAwait(false);
            using NetworkStream stream = tcpClient.GetStream();
            RpcTcpTransport transport = new RpcTcpTransport(
                stream,
                new RpcTransportOptions(timeouts: new RpcTransportTimeouts(
                    readTimeout: TimeSpan.FromSeconds(15),
                    writeTimeout: TimeSpan.FromSeconds(15))));

            await transport.SendAsync(call, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);

            rpc_msg_body? body = reply.Header.body;
            if (body?.mtype != msg_type.REPLY || body.rbody is null)
            {
                throw new InvalidOperationException("Expected an RPC reply envelope from the sample MOUNT v3 listener.");
            }

            reply_body replyBody = body.rbody;
            if (replyBody.stat != reply_stat.MSG_DENIED || replyBody.rreply is null)
            {
                throw new InvalidOperationException(
                    "Expected the dispatcher to reject the RPCSEC_GSS call with MSG_DENIED. Observed reply_stat: "
                    + replyBody.stat);
            }

            rejected_reply rejected = replyBody.rreply;
            if (rejected.stat != reject_stat.AUTH_ERROR || !rejected.stat_value.HasValue)
            {
                throw new InvalidOperationException(
                    "Expected an AUTH_ERROR rejection carrying an auth_stat value. Observed reject_stat: "
                    + rejected.stat);
            }

            return rejected.stat_value.Value;
        }

        internal static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

