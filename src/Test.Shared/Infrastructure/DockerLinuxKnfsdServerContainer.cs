namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    internal sealed class DockerLinuxKnfsdServerContainer : IAsyncDisposable
    {
        private DockerLinuxKnfsdServerContainer(string containerName, int mountPort, int nfsPort)
        {
            ContainerName = containerName;
            MountPort = mountPort;
            NfsPort = nfsPort;
        }

        public string ContainerName { get; }

        public int MountPort { get; }

        public int NfsPort { get; }

        public static async Task<DockerLinuxKnfsdServerContainer> StartAsync(CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                await DockerInteropImages.EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

                string containerName = "opennfs-linux-knfsd-" + Guid.NewGuid().ToString("N");

                try
                {
                    await DockerCli.RunCheckedAsync(
                        new List<string>
                        {
                            "run",
                            "--detach",
                            "--name",
                            containerName,
                            "--privileged",
                            "--publish",
                            "127.0.0.1::20048",
                            "--publish",
                            "127.0.0.1::2049",
                            "--tmpfs",
                            "/export:rw,mode=0777,size=16m",
                            DockerInteropImages.LinuxKnfsdServerImage,
                        },
                        cancellationToken,
                        timeout: TimeSpan.FromMinutes(2)).ConfigureAwait(false);

                    int mountPort = await ReadMappedPortAsync(containerName, "20048/tcp", cancellationToken).ConfigureAwait(false);
                    int nfsPort = await ReadMappedPortAsync(containerName, "2049/tcp", cancellationToken).ConfigureAwait(false);

                    await WaitForTcpAsync("127.0.0.1", mountPort, cancellationToken).ConfigureAwait(false);
                    await WaitForTcpAsync("127.0.0.1", nfsPort, cancellationToken).ConfigureAwait(false);
                    await WaitForNfsV40ReadyAsync(containerName, "127.0.0.1", nfsPort, cancellationToken).ConfigureAwait(false);

                    return new DockerLinuxKnfsdServerContainer(containerName, mountPort, nfsPort);
                }
                catch (Exception exception) when (attempt == 0)
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

            throw new InvalidOperationException(
                "The Linux kernel NFS server container did not become NFSv4.0-ready after a clean retry.",
                lastException);
        }

        public Task<string> GetLogsAsync(CancellationToken cancellationToken)
        {
            return GetLogsCoreAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await RemoveContainerAsync(ContainerName).ConfigureAwait(false);
        }

        private static async Task<string> ReadInspectValueAsync(
            string containerName,
            string template,
            CancellationToken cancellationToken)
        {
            DockerCommandResult result = await DockerCli.RunCheckedAsync(
                new List<string>
                {
                    "inspect",
                    "--format",
                    template,
                    containerName,
                },
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            return result.StandardOutput.Trim();
        }

        private static async Task<int> ReadMappedPortAsync(
            string containerName,
            string containerPort,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string hostPort = await ReadInspectValueAsync(
                    containerName,
                    "{{ with (index (index .NetworkSettings.Ports \"" + containerPort + "\") 0) }}{{ .HostPort }}{{ end }}",
                    cancellationToken).ConfigureAwait(false);
                if (int.TryParse(hostPort, out int parsedPort))
                {
                    return parsedPort;
                }

                string containerStatus = await ReadInspectValueAsync(
                    containerName,
                    "{{ .State.Status }}",
                    cancellationToken).ConfigureAwait(false);
                if (string.Equals(containerStatus, "exited", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(containerStatus, "dead", StringComparison.OrdinalIgnoreCase))
                {
                    string exitCode = await ReadInspectValueAsync(
                        containerName,
                        "{{ .State.ExitCode }}",
                        cancellationToken).ConfigureAwait(false);
                    string logs = await GetLogsAsync(containerName, cancellationToken).ConfigureAwait(false);

                    throw new InvalidOperationException(
                        "The Linux kernel NFS server container '" + containerName + "' exited before Docker published "
                        + containerPort
                        + " to the host."
                        + Environment.NewLine
                        + "status: "
                        + containerStatus
                        + Environment.NewLine
                        + "exit code: "
                        + exitCode
                        + Environment.NewLine
                        + "logs:"
                        + Environment.NewLine
                        + logs);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "Timed out waiting for Docker to publish " + containerPort + " for Linux kernel NFS server container '" + containerName + "'.");
        }

        private static async Task RemoveContainerAsync(string containerName)
        {
            try
            {
                _ = await DockerCli.RunAsync(
                    new List<string>
                    {
                        "rm",
                        "--force",
                        containerName,
                    },
                    CancellationToken.None,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task WaitForTcpAsync(string host, int port, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using TcpClient client = new TcpClient();

                try
                {
                    using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                    await client.ConnectAsync(host, port, timeoutTokenSource.Token).ConfigureAwait(false);
                    return;
                }
                catch (Exception exception) when (exception is SocketException or TimeoutException or OperationCanceledException)
                {
                    lastException = exception;
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                }
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux kernel NFS server container to accept TCP connections on " + host + ":" + port + ".",
                lastException);
        }

        private static async Task WaitForNfsV40ReadyAsync(string containerName, string host, int port, CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer(host, port)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    OpenNfsV40LookupResult rootResult =
                        await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                    if (rootResult.IsSuccess && rootResult.ObjectFileHandle.Length > 0)
                    {
                        return;
                    }
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            string logs = await GetLogsAsync(containerName, CancellationToken.None).ConfigureAwait(false);
            throw new TimeoutException(
                "Timed out waiting for the Linux kernel NFS server container to answer NFSv4.0 root-handle requests on "
                + host
                + ":"
                + port
                + "."
                + Environment.NewLine
                + "logs:"
                + Environment.NewLine
                + logs,
                lastException);
        }

        private async Task<string> GetLogsCoreAsync(CancellationToken cancellationToken)
        {
            return await GetLogsAsync(ContainerName, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> GetLogsAsync(string containerName, CancellationToken cancellationToken)
        {
            DockerCommandResult result = await DockerCli.RunAsync(
                new List<string>
                {
                    "logs",
                    containerName,
                },
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            return result.StandardOutput + Environment.NewLine + result.StandardError;
        }
    }
}
