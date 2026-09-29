namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    /// <summary>
    /// The Docker NFS server implementation hosted by <see cref="DockerNfsV3PeerContainer"/>.
    /// </summary>
    internal enum DockerNfsV3PeerKind
    {
        /// <summary>
        /// The Linux kernel NFS server (knfsd) with rpc.mountd and rpcbind.
        /// </summary>
        LinuxKnfsd,

        /// <summary>
        /// The NFS-Ganesha userspace server configured for NFSv3 and NFSv4.
        /// </summary>
        Ganesha,

        /// <summary>
        /// The unfs3 userspace NFSv3 server serving a tmpfs export (so Unix modes and ownership are real).
        /// </summary>
        Unfs3,
    }

    /// <summary>
    /// Docker-hosted NFSv3 peer server (Linux knfsd or NFS-Ganesha) serving a tmpfs <c>/export</c> for mounted-session interop.
    /// The container is always removed on dispose, including when startup fails.
    /// </summary>
    internal sealed class DockerNfsV3PeerContainer : IAsyncDisposable
    {
        private DockerNfsV3PeerContainer(
            DockerNfsV3PeerKind kind,
            string containerName,
            int mountPort,
            int nfsPort,
            int portmapperPort)
        {
            Kind = kind;
            ContainerName = containerName;
            MountPort = mountPort;
            NfsPort = nfsPort;
            PortmapperPort = portmapperPort;
        }

        internal DockerNfsV3PeerKind Kind { get; }

        internal string ContainerName { get; }

        internal int MountPort { get; }

        internal int NfsPort { get; }

        /// <summary>
        /// Gets the host port mapped to the container's portmapper (111/tcp), or <c>0</c> when it was not published.
        /// </summary>
        internal int PortmapperPort { get; }

        internal string DisplayName => Kind switch
        {
            DockerNfsV3PeerKind.LinuxKnfsd => "Linux knfsd",
            DockerNfsV3PeerKind.Ganesha => "NFS-Ganesha",
            _ => "unfs3",
        };

        /// <summary>
        /// Gets the in-container directory that backs the <c>/export</c> NFS export.
        /// </summary>
        internal string ContainerExportRoot => Kind == DockerNfsV3PeerKind.Ganesha ? "/export-real" : "/export";

        /// <summary>
        /// Starts the peer. When <paramref name="publishPortmapper"/> is true, the portmapper is published and rpc.mountd
        /// (knfsd only) listens on a host port that is published 1:1, so a portmapper GETPORT answer is directly reachable.
        /// </summary>
        internal static async Task<DockerNfsV3PeerContainer> StartAsync(
            DockerNfsV3PeerKind kind,
            bool publishPortmapper,
            CancellationToken cancellationToken)
        {
            if (publishPortmapper && kind != DockerNfsV3PeerKind.LinuxKnfsd)
            {
                throw new ArgumentException("Portmapper publishing with a 1:1 mountd port is only supported for the Linux knfsd peer.", nameof(publishPortmapper));
            }

            await DockerInteropImages.EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

            Exception? lastException = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string containerName = "opennfs-v3-peer-" + kind.ToString().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N");
                int fixedMountPort = publishPortmapper ? GetUnusedPort() : 0;

                try
                {
                    List<string> arguments = new List<string>
                    {
                        "run",
                        "--detach",
                        "--name",
                        containerName,
                        "--publish",
                        "127.0.0.1::2049",
                    };

                    if (publishPortmapper)
                    {
                        arguments.Add("--publish");
                        arguments.Add("127.0.0.1::111");
                        arguments.Add("--publish");
                        arguments.Add("127.0.0.1:" + fixedMountPort.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + fixedMountPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        arguments.Add("--env");
                        arguments.Add("MOUNTD_PORT=" + fixedMountPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        arguments.Add("--publish");
                        arguments.Add("127.0.0.1::20048");
                    }

                    if (kind == DockerNfsV3PeerKind.LinuxKnfsd)
                    {
                        arguments.Add("--privileged");
                        arguments.Add("--tmpfs");
                        arguments.Add("/export:rw,mode=0777,size=256m");
                        arguments.Add(DockerInteropImages.LinuxKnfsdServerImage);
                    }
                    else if (kind == DockerNfsV3PeerKind.Unfs3)
                    {
                        arguments.Add("--tmpfs");
                        arguments.Add("/export:rw,mode=0777,size=64m");
                        arguments.Add(DockerInteropImages.LinuxNfsServerImage);
                    }
                    else
                    {
                        arguments.Add("--cap-add");
                        arguments.Add("DAC_READ_SEARCH");
                        arguments.Add("--env");
                        arguments.Add("GANESHA_CONFIG=/etc/ganesha/ganesha-v3.conf");
                        arguments.Add("--tmpfs");
                        arguments.Add("/export-real:rw,mode=0777,size=256m");
                        arguments.Add(DockerInteropImages.LinuxNfsV40ServerImage);
                    }

                    await DockerCli.RunCheckedAsync(arguments, cancellationToken, timeout: TimeSpan.FromMinutes(2)).ConfigureAwait(false);

                    int nfsPort = await ReadMappedPortAsync(containerName, "2049/tcp", cancellationToken).ConfigureAwait(false);
                    int mountPort = publishPortmapper
                        ? fixedMountPort
                        : await ReadMappedPortAsync(containerName, "20048/tcp", cancellationToken).ConfigureAwait(false);
                    int portmapperPort = publishPortmapper
                        ? await ReadMappedPortAsync(containerName, "111/tcp", cancellationToken).ConfigureAwait(false)
                        : 0;

                    DockerNfsV3PeerContainer container = new DockerNfsV3PeerContainer(kind, containerName, mountPort, nfsPort, portmapperPort);
                    await container.WaitForMountReadyAsync(cancellationToken).ConfigureAwait(false);
                    return container;
                }
                catch (Exception exception) when (attempt == 0 && exception is not OperationCanceledException)
                {
                    lastException = exception;
                    await RemoveContainerAsync(containerName).ConfigureAwait(false);
                }
                catch
                {
                    await RemoveContainerAsync(containerName).ConfigureAwait(false);
                    throw;
                }
            }

            throw new InvalidOperationException("The " + kind + " NFSv3 peer container did not become ready after a clean retry.", lastException);
        }

        internal OpenNfsClientBuilder CreateClientBuilder()
        {
            return new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", NfsPort)
                .WithMountEndpoint("127.0.0.1", MountPort)
                .WithAuthSysCredentials("opennfs-interop", 0, 0);
        }

        internal async Task<(OpenNfsClient Client, OpenNfsMountSession Session)> ConnectAndMountAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = CreateClientBuilder().Build();
            try
            {
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                OpenNfsMountSession session = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                return (client, session);
            }
            catch
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        internal async Task<string> GetLogsAsync(CancellationToken cancellationToken)
        {
            DockerCommandResult result = await DockerCli.RunAsync(
                new List<string> { "logs", ContainerName },
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            return result.StandardOutput + Environment.NewLine + result.StandardError;
        }

        internal async Task<string> ExecAsync(IReadOnlyList<string> command, CancellationToken cancellationToken)
        {
            List<string> arguments = new List<string> { "exec", ContainerName };
            arguments.AddRange(command);
            DockerCommandResult result = await DockerCli.RunCheckedAsync(arguments, cancellationToken, timeout: TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            return result.StandardOutput;
        }

        public async ValueTask DisposeAsync()
        {
            await RemoveContainerAsync(ContainerName).ConfigureAwait(false);
        }

        private static int GetUnusedPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<int> ReadMappedPortAsync(string containerName, string containerPort, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DockerCommandResult result = await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "inspect",
                        "--format",
                        "{{ with (index (index .NetworkSettings.Ports \"" + containerPort + "\") 0) }}{{ .HostPort }}{{ end }}",
                        containerName,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

                if (int.TryParse(result.StandardOutput.Trim(), out int port))
                {
                    return port;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for Docker to publish " + containerPort + " for container '" + containerName + "'.");
        }

        private static async Task RemoveContainerAsync(string containerName)
        {
            await DockerContainerCleanup.RemoveAsync(containerName).ConfigureAwait(false);
        }

        private async Task WaitForMountReadyAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(120);
            Exception? lastException = null;
            string lastStatus = "no attempt";

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using OpenNfsClient client = CreateClientBuilder()
                        .WithConnectionTimeout(TimeSpan.FromSeconds(3))
                        .WithResponseTimeout(TimeSpan.FromSeconds(5))
                        .WithRetryPolicy(1, TimeSpan.FromMilliseconds(50))
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    OpenNfsMountV3Result mountResult = await client.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                    lastStatus = mountResult.Status.ToString();
                    if (mountResult.IsSuccess)
                    {
                        OpenNfsV3GetAttributesResult attributes =
                            await client.Files.GetAttributesV3Async(mountResult.RootFileHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                        if (attributes.IsSuccess)
                        {
                            await client.Exports.UnmountV3Async("/export", cancellationToken).ConfigureAwait(false);
                            return;
                        }

                        lastStatus = "GETATTR " + attributes.Status;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            string logs = await GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException(
                "Timed out waiting for the " + DisplayName + " container to accept MOUNT v3 on 127.0.0.1:" + MountPort
                + " (last status: " + lastStatus + ")." + Environment.NewLine + "logs:" + Environment.NewLine + logs,
                lastException);
        }
    }
}
