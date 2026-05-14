namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Packaged server and client peer-matrix execution helpers for release-readiness suites.
    /// </summary>
    internal static class ReleaseReadinessPackagingSupport
    {
        internal static async Task ExecutePackedServerPackageServesLinuxKernelClientAsync(CancellationToken cancellationToken)
        {
            string programSource = PackagedConsumerProgramSourceFactory.CreateServerApplicationProgramSource(denyMounts: false);

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                CreateLinuxMountReadWriteCommand(process.MountPort, process.NfsPort, "/data", "hello.txt"),
                cancellationToken).ConfigureAwait(false);

            string combinedOutput = result.StandardOutput + Environment.NewLine + result.StandardError;
            if (result.ExitCode != 0
                || !combinedOutput.Contains("hello-from-packed-server", StringComparison.Ordinal)
                || !combinedOutput.Contains("UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal)
                || !combinedOutput.Contains("hello.txt", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected a clean packaged OpenNFS.Server consumer to be mountable and writable from a Linux kernel client."
                    + Environment.NewLine
                    + combinedOutput);
            }
        }

        internal static async Task ExecutePackedServerPackageDeniesLinuxKernelClientMountAsync(CancellationToken cancellationToken)
        {
            string programSource = PackagedConsumerProgramSourceFactory.CreateServerApplicationProgramSource(denyMounts: true);

            await using PackedOpenNfsServerProcess process =
                await PackedOpenNfsServerProcess.StartAsync(programSource, cancellationToken).ConfigureAwait(false);

            DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                CreateLinuxDeniedMountCommand(process.MountPort, process.NfsPort, "/data"),
                cancellationToken).ConfigureAwait(false);

            string combinedOutput = result.StandardOutput + Environment.NewLine + result.StandardError;
            if (result.ExitCode != 0
                || !combinedOutput.Contains("DENIED", StringComparison.Ordinal)
                || combinedOutput.Contains("unexpected success", StringComparison.Ordinal)
                || combinedOutput.Contains("hello-from-packed-server", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected a clean packaged OpenNFS.Server consumer to preserve denied Linux mount behavior."
                    + Environment.NewLine
                    + combinedOutput);
            }
        }

        internal static async Task ExecutePackedClientPackageExecutesAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            string sampleRoot = ReleaseReadinessSharedSupport.CreateTempDirectory("PackedClientPeerMatrixPositive.Sample");
            string sampleSource = Path.Combine(sampleRoot, "source");
            string sampleMapping = Path.Combine(sampleRoot, "handles.json");
            string unfs3Root = ReleaseReadinessSharedSupport.CreateTempDirectory("PackedClientPeerMatrixPositive.Unfs3");

            try
            {
                InteropSuiteSupport.CreateLinuxServerExportLayout(unfs3Root);

                await using SampleOpenNfsServerProcess sample = await SampleOpenNfsServerProcess.StartAsync(
                    sampleSource,
                    sampleMapping,
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsServerContainer unfs3 =
                    await DockerLinuxNfsServerContainer.StartAsync(unfs3Root, cancellationToken).ConfigureAwait(false);
                await using DockerLinuxKnfsdServerContainer knfsd =
                    await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsV40ServerContainer ganesha =
                    await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

                string programSource = PackagedConsumerProgramSourceFactory.CreateClientPeerMatrixPositiveProgramSource(
                    "127.0.0.1",
                    sample.MountPort,
                    sample.NfsPort,
                    sample.Nfs40Port,
                    "127.0.0.1",
                    unfs3.MountPort,
                    unfs3.NfsPort,
                    "127.0.0.1",
                    knfsd.MountPort,
                    knfsd.NfsPort,
                    "127.0.0.1",
                    ganesha.NfsPort);

                DotnetCommandResult result = await RunExternalPackageConsumerProjectAsync(
                    Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                    "OpenNFS.Client",
                    programSource,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(8)).ConfigureAwait(false);

                if (!result.StandardOutput.Contains("PACKAGE CLIENT MATRIX POSITIVE OK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the clean packaged OpenNFS.Client consumer to complete the current positive peer matrix."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError);
                }
            }
            finally
            {
                ReleaseReadinessSharedSupport.TryDeleteDirectory(sampleRoot);
                ReleaseReadinessSharedSupport.TryDeleteDirectory(unfs3Root);
            }
        }

        internal static async Task ExecutePackedClientPackageSurfacesNegativeResultsAgainstPeerMatrixAsync(CancellationToken cancellationToken)
        {
            string sampleRoot = ReleaseReadinessSharedSupport.CreateTempDirectory("PackedClientPeerMatrixNegative.Sample");
            string sampleSource = Path.Combine(sampleRoot, "source");
            string sampleMapping = Path.Combine(sampleRoot, "handles.json");
            string unfs3Root = ReleaseReadinessSharedSupport.CreateTempDirectory("PackedClientPeerMatrixNegative.Unfs3");

            try
            {
                InteropSuiteSupport.CreateLinuxServerExportLayout(unfs3Root);

                await using SampleOpenNfsServerProcess sample = await SampleOpenNfsServerProcess.StartAsync(
                    sampleSource,
                    sampleMapping,
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsServerContainer unfs3 =
                    await DockerLinuxNfsServerContainer.StartAsync(unfs3Root, cancellationToken).ConfigureAwait(false);
                await using DockerLinuxKnfsdServerContainer knfsd =
                    await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);
                await using DockerLinuxNfsV40ServerContainer ganesha =
                    await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

                string programSource = PackagedConsumerProgramSourceFactory.CreateClientPeerMatrixNegativeProgramSource(
                    "127.0.0.1",
                    sample.MountPort,
                    sample.NfsPort,
                    sample.Nfs40Port,
                    "127.0.0.1",
                    unfs3.MountPort,
                    unfs3.NfsPort,
                    "127.0.0.1",
                    knfsd.NfsPort,
                    "127.0.0.1",
                    ganesha.NfsPort);

                DotnetCommandResult result = await RunExternalPackageConsumerProjectAsync(
                    Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                    "OpenNFS.Client",
                    programSource,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(8)).ConfigureAwait(false);

                if (!result.StandardOutput.Contains("PACKAGE CLIENT MATRIX NEGATIVE OK", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the clean packaged OpenNFS.Client consumer to complete the current negative peer matrix."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError);
                }
            }
            finally
            {
                ReleaseReadinessSharedSupport.TryDeleteDirectory(sampleRoot);
                ReleaseReadinessSharedSupport.TryDeleteDirectory(unfs3Root);
            }
        }

        internal static async Task ExecutePackedClientPackageExecutesAgainstPackedServerPackageOverDockerNetworkAsync(CancellationToken cancellationToken)
        {
            await using DockerNetworkScope network = await DockerNetworkScope.CreateAsync(cancellationToken).ConfigureAwait(false);
            await using DockerPackedOpenNfsServerContainer server = await DockerPackedOpenNfsServerContainer.StartAsync(
                PackagedConsumerProgramSourceFactory.CreateServerApplicationProgramSource(denyMounts: false),
                network.Name,
                cancellationToken).ConfigureAwait(false);

            string programSource = PackagedConsumerProgramSourceFactory.CreateClientAgainstPackedServerProgramSource(
                server.ContainerName,
                server.MountPort,
                server.NfsPort);

            DockerCommandResult result = await RunExternalPackageConsumerProjectInDockerAsync(
                Path.Combine("src", "OpenNFS.Client", "OpenNFS.Client.csproj"),
                "OpenNFS.Client",
                programSource,
                network.Name,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(8)).ConfigureAwait(false);

            if (!result.StandardOutput.Contains("PACKAGE CLIENT TO PACKAGE SERVER OK", StringComparison.Ordinal))
            {
                string serverLogs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Expected the Docker-separated packaged OpenNFS.Client consumer to complete an end-to-end flow against the Docker-separated packaged OpenNFS.Server consumer."
                    + Environment.NewLine
                    + "client stdout:"
                    + Environment.NewLine
                    + result.StandardOutput
                    + Environment.NewLine
                    + "client stderr:"
                    + Environment.NewLine
                    + result.StandardError
                    + Environment.NewLine
                    + "server logs:"
                    + Environment.NewLine
                    + serverLogs);
            }
        }

        internal static async Task<DotnetCommandResult> RunExternalPackageConsumerProjectAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                packageProjectRelativePath,
                packageId,
                programSource,
                cancellationToken).ConfigureAwait(false);

            return await DotnetCli.RunCheckedAsync(
                new[]
                {
                    "run",
                    "--disable-build-servers",
                    "--project",
                    project.ProjectPath,
                    "-c",
                    "Release",
                    "--no-restore",
                },
                project.ProjectDirectory,
                cancellationToken,
                timeout).ConfigureAwait(false);
        }

        internal static async Task<DockerCommandResult> RunExternalPackageConsumerProjectInDockerAsync(
            string packageProjectRelativePath,
            string packageId,
            string programSource,
            string networkName,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            await using ExternalPackageConsumerProject project = await ExternalPackageConsumerSupport.CreateSinglePackageConsoleAppAsync(
                packageProjectRelativePath,
                packageId,
                programSource,
                cancellationToken).ConfigureAwait(false);

            string workspaceRoot = Directory.GetParent(project.ProjectDirectory)?.FullName
                ?? throw new InvalidOperationException("Expected the packaged consumer project directory to have a parent workspace.");
            string dockerConfigPath = Path.Combine(project.ProjectDirectory, "NuGet.Docker.Config");
            string containerName = "opennfs-packed-client-" + Guid.NewGuid().ToString("N");
            WriteDockerNuGetConfig(dockerConfigPath);

            try
            {
                return await DockerCli.RunAsync(
                    new[]
                    {
                        "run",
                        "--rm",
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
                    timeout).ConfigureAwait(false);
            }
            finally
            {
                await RemoveDockerContainerAsync(containerName).ConfigureAwait(false);
            }
        }

        internal static string CreateLinuxMountReadWriteCommand(int mountPort, int nfsPort, string exportPath, string fileName)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(),
                ",mountport=", mountPort.ToString(),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs; ",
                "cat /mnt/opennfs/", fileName, "; ",
                "printf 'UPDATED-FROM-LINUX-CLIENT' | dd of=/mnt/opennfs/", fileName, " conv=notrunc status=none; ",
                "sync; ",
                "cat /mnt/opennfs/", fileName, "; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        internal static string CreateLinuxDeniedMountCommand(int mountPort, int nfsPort, string exportPath)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "set +e; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(),
                ",mountport=", mountPort.ToString(),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs >/tmp/mount.log 2>&1; ",
                "status=$?; ",
                "cat /tmp/mount.log; ",
                "if [ $status -eq 0 ]; then echo unexpected success; umount /mnt/opennfs; exit 1; fi; ",
                "echo DENIED; ",
                "exit 0");
        }

        private static async Task RemoveDockerContainerAsync(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                return;
            }

            try
            {
                _ = await DockerCli.RunAsync(
                    new[]
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
