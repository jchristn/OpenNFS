namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V40.Hosting;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.InteropSuiteSupport;

    /// <summary>
    /// Linux kernel-client mount and denial interop suites.
    /// </summary>
    internal static class InteropLinuxClientCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "LinuxKernelClientMountsOpenNfsServer",
                        displayName: "A real Linux kernel NFS client mounts and reads through an OpenNFS server host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxClientAgainstOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "LinuxKernelClientSeesDeniedMountFromOpenNfsServer",
                        displayName: "A real Linux kernel NFS client receives a failed mount when OpenNFS.Server denies access",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeLinuxClientAgainstOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "LinuxKernelClientMountsSampleOpenNfsServerArtifact",
                        displayName: "A real Linux kernel NFS client mounts, reads, and writes through the runnable Sample.OpenNfsServer artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxClientAgainstSampleOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "LinuxKernelClientSeesDeniedMountFromSampleOpenNfsServerArtifact",
                        displayName: "A real Linux kernel NFS client receives a failed mount when the runnable Sample.OpenNfsServer artifact denies access",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeLinuxClientAgainstSampleOpenNfsServerAsync),
            };
        }

        private static async Task ExecuteLinuxClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            string mappingDirectory = CreateTempDirectory();

            try
            {
                string sourceRoot = Path.Combine(@"C:\OpenNfsInterop", "Export");
                OpenNfsServer server = CreateOpenNfsInteropServer(Path.Combine(mappingDirectory, "handles.json"), sourceRoot);

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxMountCommand(host.MountPort, host.NfsPort),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel client container failed to mount or read from the OpenNFS server."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError);
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();

                if (!combinedOutput.Contains("hello-from-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("nested-from-opennfs", StringComparison.Ordinal)
                    || !combinedOutput.Contains("d", StringComparison.Ordinal)
                    || !combinedOutput.Contains("h.txt", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel client container to surface the mounted OpenNFS export contents. Output was:"
                        + Environment.NewLine
                        + combinedOutput);
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
            }
        }


        private static async Task ExecuteNegativeLinuxClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            string mappingDirectory = CreateTempDirectory();

            try
            {
                string sourceRoot = Path.Combine(@"C:\OpenNfsInterop", "Export");
                StaticMountAuthorization authorization = new StaticMountAuthorization(
                    new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal)
                    {
                        ["/export"] = NfsMountAccessDisposition.Deny,
                    });
                OpenNfsServer server = CreateOpenNfsInteropServer(
                    Path.Combine(mappingDirectory, "handles.json"),
                    sourceRoot,
                    authorization);

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxMountCommand(host.MountPort, host.NfsPort),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel client mount to fail when OpenNFS.Server denies the export, but the container command succeeded."
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + result.StandardError);
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (combinedOutput.Contains("hello-from-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the denied Linux kernel client variant not to surface mounted export contents. Output was:"
                        + Environment.NewLine
                        + combinedOutput);
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
            }
        }


        private static async Task ExecuteLinuxClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            string sourceDirectory = CreateTempDirectory();
            string mappingDirectory = CreateTempDirectory();

            try
            {
                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(
                        sourceDirectory,
                        Path.Combine(mappingDirectory, "handles.json"),
                        denyMounts: false,
                        cancellationToken).ConfigureAwait(false);

                DockerCommandResult result =
                    await DockerLinuxNfsClient.RunCommandAsync(
                        CreateLinuxSampleMountCommand(sampleServer.MountPort, sampleServer.NfsPort),
                        cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel client container failed to mount or exercise the runnable Sample.OpenNfsServer artifact."
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
                        "Expected the Linux kernel client container to surface and update the sample export contents. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string updatedHostContent =
                    await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "hello.txt"), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(updatedHostContent, "UPDATED-FROM-LINUX-CLIENT", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the sample export root file to reflect the Linux client write, but observed '"
                        + updatedHostContent
                        + "'.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
                DeleteDirectoryIfPresent(sourceDirectory);
            }
        }


        private static async Task ExecuteNegativeLinuxClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            string sourceDirectory = CreateTempDirectory();
            string mappingDirectory = CreateTempDirectory();

            try
            {
                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(
                        sourceDirectory,
                        Path.Combine(mappingDirectory, "handles.json"),
                        denyMounts: true,
                        cancellationToken).ConfigureAwait(false);

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
                        "Expected the runnable Sample.OpenNfsServer artifact to deny the Linux kernel client mount, but the container command succeeded."
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
                        "Expected the denied sample-server variant not to surface mounted export contents. Output was:"
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
                DeleteDirectoryIfPresent(mappingDirectory);
                DeleteDirectoryIfPresent(sourceDirectory);
            }
        }


    }
}
