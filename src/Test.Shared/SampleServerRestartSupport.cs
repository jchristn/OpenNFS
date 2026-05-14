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
    using static Test.Shared.SampleServerSuiteSupport;
    /// <summary>
    /// Shared execution helpers for sample artifact restart and filehandle persistence flows.
    /// </summary>
    internal static class SampleServerRestartSupport
    {
        internal static async Task ExecutePersistentFileHandleRestartAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleRestart", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string mappingPath = Path.Combine(configDirectory, "state", "handles.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                byte[] initialRootHandle;
                byte[] helloFileHandle;

                await using (SampleOpenNfsServerProcess firstProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false))
                {
                    SampleHandleResolution handles =
                        await ResolveSampleHandlesAsync(firstProcess, cancellationToken).ConfigureAwait(false);
                    initialRootHandle = handles.RootHandle;
                    helloFileHandle = handles.HelloFileHandle;
                }

                if (!File.Exists(mappingPath))
                {
                    throw new InvalidOperationException("Expected the sample artifact to persist a mapping file before the restart validation path.");
                }

                await using SampleOpenNfsServerProcess restartedProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                byte[] restartedRootHandle;
                await using (OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.MountPort)
                    .Build())
                {
                    await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    OpenNfsMountV3Result mountResult =
                        await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
                    if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                    {
                        throw new InvalidOperationException("Expected the restarted sample artifact to return a usable MOUNT v3 root filehandle.");
                    }

                    restartedRootHandle = mountResult.RootFileHandle.ToArray();
                }

                if (!initialRootHandle.AsSpan().SequenceEqual(restartedRootHandle))
                {
                    throw new InvalidOperationException("Expected the sample artifact to preserve the mounted root filehandle across restart when the mapping file is retained.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadResult readResult =
                    await nfsClient.Files.ReadV3Async(helloFileHandle, 0UL, 4096U, cancellationToken).ConfigureAwait(false);
                if (!readResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(readResult.Data.Span), "hello-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the pre-restart sample filehandle to remain readable after restart when the mapping file is retained.");
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

        internal static async Task ExecutePersistentFileHandleRestartNegativeAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleRestartNegative", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string originalMappingPath = Path.Combine(configDirectory, "state", "handles-a.json");
            string replacementMappingPath = Path.Combine(configDirectory, "state", "handles-b.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Negative Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles-a.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                byte[] initialRootHandle;
                byte[] helloFileHandle;

                await using (SampleOpenNfsServerProcess firstProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false))
                {
                    SampleHandleResolution handles =
                        await ResolveSampleHandlesAsync(firstProcess, cancellationToken).ConfigureAwait(false);
                    initialRootHandle = handles.RootHandle;
                    helloFileHandle = handles.HelloFileHandle;
                }

                if (!File.Exists(originalMappingPath))
                {
                    throw new InvalidOperationException("Expected the original sample mapping file to exist before the negative restart path.");
                }

                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Restart Negative Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles-b.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess restartedProcess =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                byte[] restartedRootHandle;
                await using (OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.MountPort)
                    .Build())
                {
                    await mountClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    OpenNfsMountV3Result mountResult =
                        await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
                    if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                    {
                        throw new InvalidOperationException("Expected the restarted sample artifact to return a usable MOUNT v3 root filehandle after replacing the mapping file.");
                    }

                    restartedRootHandle = mountResult.RootFileHandle.ToArray();
                }

                if (initialRootHandle.AsSpan().SequenceEqual(restartedRootHandle))
                {
                    throw new InvalidOperationException("Expected the restarted sample artifact to issue a different root filehandle after the persistent mapping file was replaced.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", restartedProcess.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadResult readResult =
                    await nfsClient.Files.ReadV3Async(helloFileHandle, 0UL, 4096U, cancellationToken).ConfigureAwait(false);
                if (readResult.Status != OpenNfsV3Status.Stale)
                {
                    throw new InvalidOperationException(
                        "Expected the pre-restart sample filehandle to become stale after replacing the persistent mapping file, but observed status '"
                        + readResult.Status.ToString()
                        + "'.");
                }

                if (File.Exists(replacementMappingPath) == false)
                {
                    throw new InvalidOperationException("Expected the replacement sample mapping file to be created during the negative restart path.");
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

