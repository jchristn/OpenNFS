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
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Touchstone suites that run the v0.1.1 mounted-session scenario matrix against Docker-hosted Linux knfsd and NFS-Ganesha
    /// peers, exercise portmapper discovery against a real rpcbind, and drive SETATTR (truncate, times, mode) from the Linux
    /// kernel NFS client against the runnable sample server.
    /// </summary>
    public static class InteropMountSessionSuites
    {
        private const string SuiteId = "InteropMountSessionSuites";

        /// <summary>
        /// Creates the Docker-backed mounted-session interop suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Mounted Session Peer Interop",
                cases: new List<TestCaseDescriptor>
                {
                    Interop(probe, "MountSessionScenariosAgainstLinuxKnfsd", "The mounted-session scenario matrix (including 1,500-entry READDIRPLUS paging and 32-way concurrency) passes against a Linux knfsd container", cancellationToken => ExecuteScenariosAgainstPeerAsync(DockerNfsV3PeerKind.LinuxKnfsd, cancellationToken)),
                    Interop(probe, "MountSessionScenariosAgainstGanesha", "The mounted-session scenario matrix (including 1,500-entry READDIRPLUS paging and 32-way concurrency) passes against an NFS-Ganesha container", cancellationToken => ExecuteScenariosAgainstPeerAsync(DockerNfsV3PeerKind.Ganesha, cancellationToken)),
                    Interop(probe, "CreateModesAndOwnershipAgainstLinuxKnfsd", "CREATE and MKDIR send 0644/0755 (or configured) modes so a non-root AUTH_SYS user can use what it creates on Linux knfsd, and other users are denied", cancellationToken => ExecutePermissionsAgainstPeerAsync(DockerNfsV3PeerKind.LinuxKnfsd, cancellationToken)),
                    Interop(probe, "CreateModesAndOwnershipAgainstGanesha", "CREATE and MKDIR send 0644/0755 (or configured) modes so a non-root AUTH_SYS user can use what it creates on NFS-Ganesha, and other users are denied", cancellationToken => ExecutePermissionsAgainstPeerAsync(DockerNfsV3PeerKind.Ganesha, cancellationToken)),
                    Interop(probe, "CreateModesAndOwnershipAgainstUnfs3", "CREATE and MKDIR send 0644/0755 (or configured) modes so a non-root AUTH_SYS user can use what it creates on unfs3, and other users are denied", cancellationToken => ExecutePermissionsAgainstPeerAsync(DockerNfsV3PeerKind.Unfs3, cancellationToken)),
                    Interop(probe, "PortmapperDiscoveryFindsLinuxKnfsdMountd", "Portmapper discovery finds rpc.mountd through a real rpcbind when no mount endpoint is configured", ExecutePortmapperAgainstKnfsdAsync),
                    Interop(probe, "LinuxKernelClientTruncatesThroughSampleServer", "The Linux kernel NFSv3 client truncates (O_TRUNC and truncate), sets times, and chmods files through the sample server", ExecuteLinuxClientSetAttrAgainstSampleServerAsync),
                    Interop(probe, "LinuxKernelClientTruncatesThroughSampleServerOverNfs40", "The Linux kernel NFSv4.0 client truncates (O_TRUNC and truncate) and sets times through the sample server", ExecuteLinuxClientV40SetAttrAgainstSampleServerAsync),
                });
        }

        private static TestCaseDescriptor Interop(
            DockerInteropEnvironmentProbe probe,
            string caseId,
            string displayName,
            Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId: SuiteId,
                caseId: caseId,
                displayName: displayName,
                tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                skip: !probe.IsAvailable,
                skipReason: probe.SkipReason,
                executeAsync: executeAsync);
        }

        private static async Task ExecuteScenariosAgainstPeerAsync(DockerNfsV3PeerKind kind, CancellationToken cancellationToken)
        {
            await using DockerNfsV3PeerContainer peer = await DockerNfsV3PeerContainer.StartAsync(kind, publishPortmapper: false, cancellationToken).ConfigureAwait(false);

            try
            {
                (OpenNfsClient client, OpenNfsMountSession session) = await peer.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
                await using (client.ConfigureAwait(false))
                await using (session.ConfigureAwait(false))
                {
                    MountSessionScenarioTarget target = new MountSessionScenarioTarget(peer.DisplayName, client, session, modeRoundTripsExactly: true);
                    await RunCoreScenariosAsync(target, cancellationToken).ConfigureAwait(false);
                    await RunLargeDirectoryScenarioAsync(target, 1500, cancellationToken).ConfigureAwait(false);
                    await RunConcurrencyScenarioAsync(target, 32, cancellationToken).ConfigureAwait(false);

                    string written = "/peer-visible-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt";
                    await session.Files.WriteAllBytesAsync(written, Encoding.UTF8.GetBytes("0123456789"), OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
                    await session.Files.WriteAllBytesAsync(written, Encoding.UTF8.GetBytes("abc"), OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
                    string serverPath = (kind == DockerNfsV3PeerKind.LinuxKnfsd ? "/export" : "/export-real") + written;
                    string serverContent = await peer.ExecAsync(new[] { "cat", serverPath }, cancellationToken).ConfigureAwait(false);
                    Require(serverContent == "abc", "Expected the peer's own file system to hold exactly 'abc' after an overwrite, but found '" + serverContent + "'.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                string logs = await peer.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(exception.Message + Environment.NewLine + peer.DisplayName + " logs:" + Environment.NewLine + logs, exception);
            }
        }

        private static async Task ExecutePermissionsAgainstPeerAsync(DockerNfsV3PeerKind kind, CancellationToken cancellationToken)
        {
            await using DockerNfsV3PeerContainer peer = await DockerNfsV3PeerContainer.StartAsync(kind, publishPortmapper: false, cancellationToken).ConfigureAwait(false);
            try
            {
                await MountSessionPermissionSupport.RunPeerPermissionScenarioAsync(peer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                string logs = await peer.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(exception.Message + Environment.NewLine + peer.DisplayName + " logs:" + Environment.NewLine + logs, exception);
            }
        }

        private static async Task ExecutePortmapperAgainstKnfsdAsync(CancellationToken cancellationToken)
        {
            await using DockerNfsV3PeerContainer peer = await DockerNfsV3PeerContainer.StartAsync(DockerNfsV3PeerKind.LinuxKnfsd, publishPortmapper: true, cancellationToken).ConfigureAwait(false);

            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", peer.NfsPort)
                .WithPortmapperDiscovery()
                .WithPortmapperPort(peer.PortmapperPort)
                .WithAuthSysCredentials("opennfs-interop", 0, 0)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            Require(
                client.DiscoveredMountEndpoint?.Port == peer.MountPort,
                "Expected portmapper discovery to find rpc.mountd on port " + peer.MountPort + " but discovered " + (client.DiscoveredMountEndpoint?.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "nothing") + ".");
            Require(client.DiscoveredNfsEndpoint is null, "Expected the explicit NFS port to take precedence over discovery.");

            await using OpenNfsMountSession session = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false);
            Require(await session.Metadata.ExistsAsync("/h.txt", cancellationToken).ConfigureAwait(false), "Expected the seeded knfsd file to be visible through the discovered mount.");

            await using OpenNfsClient withoutDiscovery = new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", peer.NfsPort)
                .WithAuthSysCredentials("opennfs-interop", 0, 0)
                .WithRetryPolicy(1, TimeSpan.FromMilliseconds(10))
                .Build();
            await withoutDiscovery.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await ExpectAsync<OpenNfsClientException>(() => withoutDiscovery.MountAsync("/export", cancellationToken)).ConfigureAwait(false);
        }

        private static async Task ExecuteLinuxClientSetAttrAgainstSampleServerAsync(CancellationToken cancellationToken)
        {
            string sourceDirectory = InteropSuiteSupport.CreateTempDirectory();
            string mappingDirectory = InteropSuiteSupport.CreateTempDirectory();

            try
            {
                await using SampleOpenNfsServerProcess sampleServer = await SampleOpenNfsServerProcess.StartAsync(
                    sourceDirectory,
                    Path.Combine(mappingDirectory, "handles.json"),
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);

                string mountCommand = string.Concat(
                    "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", sampleServer.NfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ",mountport=", sampleServer.MountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ",nolock,soft,timeo=10,retrans=1,actimeo=0 host.docker.internal:/exports/sample /mnt/opennfs; ");
                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(CreateSetAttrShellCommand(mountCommand), cancellationToken).ConfigureAwait(false);
                await VerifySetAttrResultAsync(result, sourceDirectory, sampleServer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                InteropSuiteSupport.DeleteDirectoryIfPresent(mappingDirectory);
                InteropSuiteSupport.DeleteDirectoryIfPresent(sourceDirectory);
            }
        }

        private static async Task ExecuteLinuxClientV40SetAttrAgainstSampleServerAsync(CancellationToken cancellationToken)
        {
            string sourceDirectory = InteropSuiteSupport.CreateTempDirectory();
            string mappingDirectory = InteropSuiteSupport.CreateTempDirectory();

            try
            {
                await using SampleOpenNfsServerProcess sampleServer = await SampleOpenNfsServerProcess.StartAsync(
                    sourceDirectory,
                    Path.Combine(mappingDirectory, "handles.json"),
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);

                string mountCommand = string.Concat(
                    "mkdir -p /var/lib/nfs/rpc_pipefs; ",
                    "mount -t rpc_pipefs sunrpc /var/lib/nfs/rpc_pipefs >/dev/null 2>&1 || true; ",
                    "mount -t nfs4 -o vers=4.0,minorversion=0,port=", sampleServer.Nfs40Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ",soft,timeo=10,retrans=1,actimeo=0 host.docker.internal:/ /mnt/opennfs; ");
                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(CreateSetAttrShellCommand(mountCommand), cancellationToken).ConfigureAwait(false);
                await VerifySetAttrResultAsync(result, sourceDirectory, sampleServer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                InteropSuiteSupport.DeleteDirectoryIfPresent(mappingDirectory);
                InteropSuiteSupport.DeleteDirectoryIfPresent(sourceDirectory);
            }
        }

        private static string CreateSetAttrShellCommand(string mountCommand)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                mountCommand,
                "cat /mnt/opennfs/hello.txt; echo; ",
                "printf 'short' > /mnt/opennfs/hello.txt; ",
                "sync; ",
                "echo \"otrunc-size=$(stat -c %s /mnt/opennfs/hello.txt)\"; ",
                "printf 'abcdefghij' > /mnt/opennfs/truncate-me.txt; ",
                "truncate -s 3 /mnt/opennfs/truncate-me.txt; ",
                "echo \"shrunk-size=$(stat -c %s /mnt/opennfs/truncate-me.txt)\"; ",
                "truncate -s 8 /mnt/opennfs/truncate-me.txt; ",
                "echo \"extended-size=$(stat -c %s /mnt/opennfs/truncate-me.txt)\"; ",
                "printf 'data' > /mnt/opennfs/empty-me.txt; ",
                ": > /mnt/opennfs/empty-me.txt; ",
                "echo \"emptied-size=$(stat -c %s /mnt/opennfs/empty-me.txt)\"; ",
                "TZ=UTC touch -m -d '2001-02-03 04:05:06' /mnt/opennfs/truncate-me.txt; ",
                "echo \"mtime=$(stat -c %Y /mnt/opennfs/truncate-me.txt)\"; ",
                "chmod 0644 /mnt/opennfs/truncate-me.txt && echo chmod-ok; ",
                "sync; ",
                "umount /mnt/opennfs");
        }

        private static async Task VerifySetAttrResultAsync(
            DockerCommandResult result,
            string sourceDirectory,
            SampleOpenNfsServerProcess sampleServer,
            CancellationToken cancellationToken)
        {
            string output = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "The Linux kernel client failed to truncate, set times, or chmod through the sample server (exit "
                    + result.ExitCode + ")." + Environment.NewLine + output + Environment.NewLine + "sample:" + Environment.NewLine + sampleServer.GetCombinedOutput());
            }

            string[] expectedLines = { "otrunc-size=5", "shrunk-size=3", "extended-size=8", "emptied-size=0", "mtime=981173106", "chmod-ok" };
            string[] missing = expectedLines.Where(line => !output.Contains(line, StringComparison.Ordinal)).ToArray();
            Require(missing.Length == 0, "Expected the Linux client output to contain " + string.Join(", ", missing) + ". Output was:" + Environment.NewLine + output);

            string hello = await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "hello.txt"), cancellationToken).ConfigureAwait(false);
            Require(hello == "short", "Expected the O_TRUNC rewrite to leave exactly 'short' on the host, but found '" + hello + "'.");

            byte[] truncated = await File.ReadAllBytesAsync(Path.Combine(sourceDirectory, "truncate-me.txt"), cancellationToken).ConfigureAwait(false);
            Require(
                truncated.Length == 8 && Encoding.ASCII.GetString(truncated, 0, 3) == "abc" && truncated.Skip(3).All(static value => value == 0),
                "Expected truncate -s 3 then -s 8 to leave 'abc' plus five zero bytes on the host.");
            Require(
                File.GetLastWriteTimeUtc(Path.Combine(sourceDirectory, "truncate-me.txt")) == DateTimeOffset.FromUnixTimeSeconds(981173106).UtcDateTime,
                "Expected touch -d to set the host modification time.");
            Require(new FileInfo(Path.Combine(sourceDirectory, "empty-me.txt")).Length == 0, "Expected ': >' to truncate the host file to zero bytes.");
        }
    }
}
