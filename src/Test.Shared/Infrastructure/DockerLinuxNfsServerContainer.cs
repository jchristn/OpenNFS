namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DockerLinuxNfsServerContainer : IAsyncDisposable
    {
        private DockerLinuxNfsServerContainer(string containerName, int mountPort, int nfsPort)
        {
            ContainerName = containerName;
            MountPort = mountPort;
            NfsPort = nfsPort;
        }

        public string ContainerName { get; }

        public int MountPort { get; }

        public int NfsPort { get; }

        public static async Task<DockerLinuxNfsServerContainer> StartAsync(
            string exportDirectory,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

            await DockerInteropImages.EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

            string fullExportDirectory = Path.GetFullPath(exportDirectory);
            string containerName = "opennfs-linux-server-" + Guid.NewGuid().ToString("N");

            try
            {
                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "run",
                        "--detach",
                        "--name",
                        containerName,
                        "--publish",
                        "127.0.0.1::20048",
                        "--publish",
                        "127.0.0.1::2049",
                        "--volume",
                        fullExportDirectory + ":/export",
                        DockerInteropImages.LinuxNfsServerImage,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(2)).ConfigureAwait(false);

                int mountPort = await ReadMappedPortAsync(containerName, "20048/tcp", cancellationToken).ConfigureAwait(false);
                int nfsPort = await ReadMappedPortAsync(containerName, "2049/tcp", cancellationToken).ConfigureAwait(false);

                await WaitForTcpAsync("127.0.0.1", mountPort, cancellationToken).ConfigureAwait(false);
                await WaitForTcpAsync("127.0.0.1", nfsPort, cancellationToken).ConfigureAwait(false);

                return new DockerLinuxNfsServerContainer(containerName, mountPort, nfsPort);
            }
            catch
            {
                await RemoveContainerAsync(containerName).ConfigureAwait(false);
                throw;
            }
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
            string hostPort = await ReadInspectValueAsync(
                containerName,
                "{{ (index (index .NetworkSettings.Ports \"" + containerPort + "\") 0).HostPort }}",
                cancellationToken).ConfigureAwait(false);

            if (!int.TryParse(hostPort, out int parsedPort))
            {
                throw new InvalidOperationException(
                    "The docker container '" + containerName + "' reported unmappable host port value '" + hostPort + "' for " + containerPort + ".");
            }

            return parsedPort;
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
                "Timed out waiting for the Linux NFS server container to accept TCP connections on " + host + ":" + port + ".",
                lastException);
        }

        private async Task<string> GetLogsCoreAsync(CancellationToken cancellationToken)
        {
            DockerCommandResult result = await DockerCli.RunAsync(
                new List<string>
                {
                    "logs",
                    ContainerName,
                },
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            return result.StandardOutput + Environment.NewLine + result.StandardError;
        }
    }
}
