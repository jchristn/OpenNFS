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
    using Test.Shared.Infrastructure;    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Shared execution helpers for sample artifact startup and Linux mount validation flows.
    /// </summary>
    internal static class SampleServerArtifactSupport
    {
        internal static async Task ExecuteSampleArtifactStartsFromConfigFileAndServesMountedSessionFlowAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleConfig", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string resolvedSourceRoot = Path.Combine(configDirectory, "content", "export");
            string resolvedMappingPath = Path.Combine(configDirectory, "state", "handles.json");

            TcpListener reservedMountListener = CreateReservedListener();
            TcpListener reservedNfsListener = CreateReservedListener();
            TcpListener reservedNfs40Listener = CreateReservedListener();

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Configured Sample",
                        exportPath = "/exports/configured",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = ((IPEndPoint)reservedMountListener.LocalEndpoint).Port,
                        nfsPort = ((IPEndPoint)reservedNfsListener.LocalEndpoint).Port,
                        nfs40Port = ((IPEndPoint)reservedNfs40Listener.LocalEndpoint).Port,
                        denyMounts = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                if (process.MountPort == ((IPEndPoint)reservedMountListener.LocalEndpoint).Port
                    || process.NfsPort == ((IPEndPoint)reservedNfsListener.LocalEndpoint).Port
                    || process.Nfs40Port == ((IPEndPoint)reservedNfs40Listener.LocalEndpoint).Port)
                {
                    throw new InvalidOperationException("Expected CLI port overrides to win over the config file when the sample process helper requests ephemeral ports.");
                }

                if (!Directory.Exists(resolvedSourceRoot)
                    || !File.Exists(Path.Combine(resolvedSourceRoot, "hello.txt"))
                    || !File.Exists(Path.Combine(resolvedSourceRoot, "docs", "nested.txt")))
                {
                    throw new InvalidOperationException("Expected config-relative source paths to resolve from the config file directory and be seeded by the sample artifact.");
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", process.NfsPort)
                    .WithMountPort(process.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                await using OpenNfsMountSession session =
                    await client.MountAsync("/exports/configured", cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsV3DirectoryEntry> rootEntries =
                    await session.Directories.ListAsync("/", cancellationToken).ConfigureAwait(false);
                string[] rootNames = rootEntries
                    .Select(static entry => entry.Name)
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();

                if (!rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to expose the seeded root entries over the public mounted-session flow.");
                }

                byte[] fileBytes = await session.Files.ReadAllBytesAsync("/hello.txt", cancellationToken).ConfigureAwait(false);
                if (!string.Equals(Encoding.UTF8.GetString(fileBytes), "hello-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to return the seeded hello.txt payload.");
                }

                if (!File.Exists(resolvedMappingPath))
                {
                    throw new InvalidOperationException("Expected the config-started sample artifact to persist filehandle mappings at the config-relative mapping path.");
                }
            }
            finally
            {
                reservedMountListener.Stop();
                reservedNfsListener.Stop();
                reservedNfs40Listener.Stop();

                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }

        internal static async Task ExecuteSampleArtifactHonorsDeniedMountsFromConfigFileAsync(
            System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleConfigDenied", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Denied Sample",
                        exportPath = "/exports/denied",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = true,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess process =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", process.NfsPort)
                    .WithMountPort(process.MountPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    _ = await client.MountAsync("/exports/denied", cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected the sample artifact to deny mounts when denyMounts=true is supplied through the config file.");
                }
                catch (InvalidOperationException exception)
                {
                    if (!exception.Message.Contains("AccessDenied", StringComparison.Ordinal)
                        || !exception.Message.Contains("/exports/denied", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected the denied sample-config mount failure to include both the export path and the MOUNT v3 access status.");
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

        internal static async Task ExecuteLinuxMountReadWriteAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleLinuxMount", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");
            string resolvedSourceRoot = Path.Combine(configDirectory, "content", "export");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Linux Sample",
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

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxSampleMountCommand(sampleServer.MountPort, sampleServer.NfsPort),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel client container failed to mount or exercise the runnable Sample.OpenNfsServer artifact from its config-file bootstrap path."
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
                if (!combinedOutput.Contains("hello-from-sample-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("nested-from-sample-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal)
                    || !combinedOutput.Contains("docs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("hello.txt", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel client container to surface and update the sample export contents from the config-file bootstrap path. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string updatedHostContent =
                    await File.ReadAllTextAsync(Path.Combine(resolvedSourceRoot, "hello.txt"), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(updatedHostContent, "UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the sample export root file to reflect the Linux client write after config-file startup, but observed '"
                        + updatedHostContent
                        + "'.");
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

        internal static async Task ExecuteLinuxMountDeniedAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleLinuxMountDenied", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    new
                    {
                        serverName = "Denied Linux Sample",
                        exportPath = "/exports/sample",
                        sourcePath = "content/export",
                        mappingPath = "state/handles.json",
                        listenerAddress = "0.0.0.0",
                        mountPort = 20048,
                        nfsPort = 2049,
                        nfs40Port = 3049,
                        denyMounts = true,
                    },
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxReadOnlyMountCommand(
                            sampleServer.MountPort,
                            sampleServer.NfsPort,
                            "/exports/sample",
                            "hello.txt",
                            "docs/nested.txt"),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    throw new InvalidOperationException(
                        "Expected the runnable Sample.OpenNfsServer artifact to deny the Linux kernel client mount from the config-file bootstrap path, but the container command succeeded."
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + result.StandardError
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (combinedOutput.Contains("hello-from-sample-opennfs", StringComparison.Ordinal)
                    || combinedOutput.Contains("nested-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the denied sample-server Linux variant not to surface mounted export contents. Output was:"
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
                if (Directory.Exists(rootDirectory))
                {
                    Directory.Delete(rootDirectory, recursive: true);
                }
            }
        }
    }
}

