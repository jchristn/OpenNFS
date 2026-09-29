namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal sealed class DockerPackedOpenNfsServerContainer : IAsyncDisposable
    {
        private readonly ExternalPackageConsumerProject project;

        private DockerPackedOpenNfsServerContainer(
            ExternalPackageConsumerProject project,
            string containerName,
            int mountPort,
            int nfsPort)
        {
            this.project = project;
            ContainerName = containerName;
            MountPort = mountPort;
            NfsPort = nfsPort;
        }

        internal string ContainerName { get; }

        internal int MountPort { get; }

        internal int NfsPort { get; }

        internal static async Task<DockerPackedOpenNfsServerContainer> StartAsync(
            string programSource,
            string networkName,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programSource);
            ArgumentException.ThrowIfNullOrWhiteSpace(networkName);

            ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                Path.Combine("src", "OpenNFS.Server", "OpenNFS.Server.csproj"),
                "OpenNFS.Server",
                programSource,
                cancellationToken).ConfigureAwait(false);

            string workspaceRoot = Directory.GetParent(project.ProjectDirectory)?.FullName
                ?? throw new InvalidOperationException("Expected the packaged server consumer project directory to have a parent workspace.");
            string dockerConfigPath = Path.Combine(project.ProjectDirectory, "NuGet.Docker.Config");
            string containerName = "opennfs-packed-server-" + Guid.NewGuid().ToString("N");

            try
            {
                WriteDockerNuGetConfig(dockerConfigPath);

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "run",
                        "--detach",
                        "--interactive",
                        "--name",
                        containerName,
                        "--network",
                        networkName,
                        "--volume",
                        Path.GetFullPath(workspaceRoot) + ":/workspace",
                        "--workdir",
                        "/workspace/consumer",
                        "mcr.microsoft.com/dotnet/sdk:8.0",
                        "sh",
                        "-lc",
                        "dotnet restore Consumer.csproj --disable-build-servers --configfile NuGet.Docker.Config && dotnet run --disable-build-servers --project Consumer.csproj -c Release --no-restore",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                (int mountPort, int nfsPort) = await WaitForReadyAsync(containerName, cancellationToken).ConfigureAwait(false);
                return new DockerPackedOpenNfsServerContainer(project, containerName, mountPort, nfsPort);
            }
            catch
            {
                await RemoveContainerAsync(containerName).ConfigureAwait(false);
                await project.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await RemoveContainerAsync(ContainerName).ConfigureAwait(false);
            await project.DisposeAsync().ConfigureAwait(false);
        }

        internal async Task<string> GetLogsAsync(CancellationToken cancellationToken)
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

        private static async Task<(int MountPort, int NfsPort)> WaitForReadyAsync(
            string containerName,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(90));

            while (true)
            {
                timeoutSource.Token.ThrowIfCancellationRequested();

                string logs = await GetLogsCoreAsync(containerName, timeoutSource.Token).ConfigureAwait(false);
                if (TryParseReadyLine(logs, out int mountPort, out int nfsPort))
                {
                    return (mountPort, nfsPort);
                }

                if (!await IsRunningAsync(containerName, timeoutSource.Token).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        "The Dockerized packed OpenNFS server consumer exited before announcing readiness."
                        + Environment.NewLine
                        + logs);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutSource.Token).ConfigureAwait(false);
            }
        }

        private static async Task<string> GetLogsCoreAsync(string containerName, CancellationToken cancellationToken)
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

        private static async Task<bool> IsRunningAsync(string containerName, CancellationToken cancellationToken)
        {
            DockerCommandResult result = await DockerCli.RunAsync(
                new List<string>
                {
                    "inspect",
                    "--format",
                    "{{.State.Running}}",
                    containerName,
                },
                cancellationToken,
                timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            return result.ExitCode == 0
                && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseReadyLine(string logs, out int mountPort, out int nfsPort)
        {
            mountPort = 0;
            nfsPort = 0;

            if (string.IsNullOrWhiteSpace(logs))
            {
                return false;
            }

            string[] lines = logs.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (!line.StartsWith("READY ", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int tokenIndex = 1; tokenIndex < tokens.Length; tokenIndex++)
                {
                    int separatorIndex = tokens[tokenIndex].IndexOf('=');
                    if (separatorIndex <= 0 || separatorIndex >= tokens[tokenIndex].Length - 1)
                    {
                        continue;
                    }

                    values[tokens[tokenIndex].Substring(0, separatorIndex)] = tokens[tokenIndex].Substring(separatorIndex + 1);
                }

                if (values.TryGetValue("mountPort", out string? mountPortValue)
                    && values.TryGetValue("nfsPort", out string? nfsPortValue)
                    && int.TryParse(mountPortValue, out mountPort)
                    && int.TryParse(nfsPortValue, out nfsPort))
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task RemoveContainerAsync(string containerName)
        {
            await DockerContainerCleanup.RemoveAsync(containerName).ConfigureAwait(false);
        }

        private static void WriteDockerNuGetConfig(string configPath)
        {
            XDocument document = new XDocument(
                new XElement(
                    "configuration",
                    new XElement(
                        "packageSources",
                        new XElement("clear"),
                        new XElement(
                            "add",
                            new XAttribute("key", "local"),
                            new XAttribute("value", "/workspace/feed")))));
            document.Save(configPath);
        }
    }
}
