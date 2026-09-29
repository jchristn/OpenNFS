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
    using static Test.Shared.MountSessionScenarioSupport;

    /// <summary>
    /// Create-mode and ownership cases: CREATE and MKDIR must carry an explicit mode so that entries created by a non-root
    /// AUTH_SYS user are usable by that user (servers such as Linux knfsd otherwise create them with mode 000).
    /// </summary>
    internal static class MountSessionPermissionSupport
    {
        private const uint OwnerUserId = 1234;
        private const uint OtherUserId = 5678;
        private static readonly uint DefaultFileMode = Convert.ToUInt32("644", 8);
        private static readonly uint DefaultDirectoryMode = Convert.ToUInt32("755", 8);

        internal static async Task RunPeerPermissionScenarioAsync(DockerNfsV3PeerContainer peer, CancellationToken cancellationToken)
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string root = "/identity-" + id;

            await using (OpenNfsClient owner = peer.CreateClientBuilder().WithAuthSysCredentials("opennfs-owner", OwnerUserId, OwnerUserId).Build())
            {
                await owner.ConnectAsync(cancellationToken).ConfigureAwait(false);
                await using OpenNfsMountSession session = await owner.MountAsync("/export", cancellationToken).ConfigureAwait(false);

                await session.Directories.CreateDirectoryAsync(root + "/sub", createParents: true, cancellationToken).ConfigureAwait(false);
                await session.Files.WriteAllBytesAsync(root + "/sub/nested.txt", Encoding.UTF8.GetBytes("first version"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                await session.Files.WriteAllBytesAsync(root + "/sub/nested.txt", Encoding.UTF8.GetBytes("v2"), OpenNfsWriteStability.Unstable, cancellationToken).ConfigureAwait(false);
                Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync(root + "/sub/nested.txt", cancellationToken).ConfigureAwait(false)) == "v2", "Expected the non-root owner to overwrite and truncate its own file.");

                await session.Directories.CreateDirectoryAsync(root + "/sub/deeper", cancellationToken).ConfigureAwait(false);
                await session.Directories.CreateFileAsync(root + "/sub/deeper/empty.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
                using (MemoryStream source = new MemoryStream(Encoding.UTF8.GetBytes("streamed")))
                {
                    await session.Files.WriteAsync(root + "/sub/deeper/streamed.txt", source, null, OpenNfsWriteStability.DataSync, cancellationToken).ConfigureAwait(false);
                }

                IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries = await session.Directories.ListWithAttributesAsync(root + "/sub", cancellationToken).ConfigureAwait(false);
                OpenNfsV3DirectoryPlusEntry nested = entries.Single(static entry => entry.Name == "nested.txt");
                OpenNfsV3DirectoryPlusEntry deeper = entries.Single(static entry => entry.Name == "deeper");
                Require((nested.Attributes!.Mode & 0xFFFU) == DefaultFileMode && nested.Attributes.UserId == OwnerUserId, "Expected nested.txt to be 0644 and owned by uid 1234 but found " + Convert.ToString(nested.Attributes.Mode & 0xFFFU, 8) + " uid " + nested.Attributes.UserId + ".");
                Require((deeper.Attributes!.Mode & 0xFFFU) == DefaultDirectoryMode && deeper.Attributes.UserId == OwnerUserId, "Expected deeper/ to be 0755 and owned by uid 1234 but found " + Convert.ToString(deeper.Attributes.Mode & 0xFFFU, 8) + ".");

                await RequireStatAsync(peer, root, "755 1234", cancellationToken).ConfigureAwait(false);
                await RequireStatAsync(peer, root + "/sub", "755 1234", cancellationToken).ConfigureAwait(false);
                await RequireStatAsync(peer, root + "/sub/nested.txt", "644 1234", cancellationToken).ConfigureAwait(false);
                await RequireStatAsync(peer, root + "/sub/deeper/empty.txt", "644 1234", cancellationToken).ConfigureAwait(false);
                await RequireStatAsync(peer, root + "/sub/deeper/streamed.txt", "644 1234", cancellationToken).ConfigureAwait(false);

                await session.Directories.DeleteFileAsync(root + "/sub/deeper/empty.txt", cancellationToken).ConfigureAwait(false);
                await session.Directories.DeleteFileAsync(root + "/sub/deeper/streamed.txt", cancellationToken).ConfigureAwait(false);
                await session.Directories.DeleteDirectoryAsync(root + "/sub/deeper", cancellationToken).ConfigureAwait(false);
                Require(!await session.Metadata.ExistsAsync(root + "/sub/deeper", cancellationToken).ConfigureAwait(false), "Expected the owner to delete its own nested directory.");
            }

            await using (OpenNfsClient other = peer.CreateClientBuilder().WithAuthSysCredentials("opennfs-other", OtherUserId, OtherUserId).Build())
            {
                await other.ConnectAsync(cancellationToken).ConfigureAwait(false);
                await using OpenNfsMountSession session = await other.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                Require(Encoding.UTF8.GetString(await session.Files.ReadAllBytesAsync(root + "/sub/nested.txt", cancellationToken).ConfigureAwait(false)) == "v2", "Expected another uid to read a 0644 file.");
                await ExpectStatusAsync(
                    () => session.Files.WriteAllBytesAsync(root + "/sub/intruder.txt", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken),
                    OpenNfsV3Status.AccessDenied,
                    OpenNfsV3Status.PermissionDenied).ConfigureAwait(false);
                await ExpectStatusAsync(
                    () => session.Files.WriteAllBytesAsync(root + "/sub/nested.txt", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken),
                    OpenNfsV3Status.AccessDenied,
                    OpenNfsV3Status.PermissionDenied).ConfigureAwait(false);
                await ExpectStatusAsync(
                    () => session.Directories.CreateDirectoryAsync(root + "/sub/intruder", cancellationToken),
                    OpenNfsV3Status.AccessDenied,
                    OpenNfsV3Status.PermissionDenied).ConfigureAwait(false);
            }

            string custom = "/custom-modes-" + id;
            await using (OpenNfsClient customClient = peer.CreateClientBuilder()
                .WithAuthSysCredentials("opennfs-owner", OwnerUserId, OwnerUserId)
                .WithDefaultCreateModes(Convert.ToUInt32("600", 8), Convert.ToUInt32("700", 8))
                .Build())
            {
                await customClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                await using OpenNfsMountSession session = await customClient.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                await session.Directories.CreateDirectoryAsync(custom + "/inner", createParents: true, cancellationToken).ConfigureAwait(false);
                await session.Files.WriteAllBytesAsync(custom + "/inner/secret.txt", Encoding.UTF8.GetBytes("s"), OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
            }

            await RequireStatAsync(peer, custom, "700 1234", cancellationToken).ConfigureAwait(false);
            await RequireStatAsync(peer, custom + "/inner", "700 1234", cancellationToken).ConfigureAwait(false);
            await RequireStatAsync(peer, custom + "/inner/secret.txt", "600 1234", cancellationToken).ConfigureAwait(false);
        }

        internal static async Task ExecuteInProcessCreateModesAsync(CancellationToken cancellationToken)
        {
            await using EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false);
            InterceptingRpcExecutor executor = new InterceptingRpcExecutor();
            await using OpenNfsClient client = await MountSessionFaultInjectionSupport.ConnectInterceptedAsync(server, executor, cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession session = await client.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);

            Require(client.Settings.DefaultFileCreateMode == DefaultFileMode && client.Settings.DefaultDirectoryCreateMode == DefaultDirectoryMode, "Expected default create modes of 0644 and 0755.");
            await session.Directories.CreateDirectoryAsync("/a/b", createParents: true, cancellationToken).ConfigureAwait(false);
            await session.Directories.CreateFileAsync("/a/b/created.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
            await session.Files.WriteAllBytesAsync("/a/b/written.txt", new byte[] { 1 }, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);

            foreach (InterceptedNfsCall call in executor.CallsFor(8))
            {
                OpenNFS.Protocol.V3.Generated.CREATE3args arguments = MountSessionFaultInjectionSupport.Decode(call.Payload, OpenNFS.Protocol.V3.Generated.CREATE3args.ReadFrom);
                Require(arguments.how!.obj_attributes!.mode!.set_it && arguments.how.obj_attributes.mode.mode!.Value!.Value == DefaultFileMode, "Expected every CREATE to carry mode 0644.");
            }

            foreach (InterceptedNfsCall call in executor.CallsFor(9))
            {
                OpenNFS.Protocol.V3.Generated.MKDIR3args arguments = MountSessionFaultInjectionSupport.Decode(call.Payload, OpenNFS.Protocol.V3.Generated.MKDIR3args.ReadFrom);
                Require(arguments.attributes!.mode!.set_it && arguments.attributes.mode.mode!.Value!.Value == DefaultDirectoryMode, "Expected every MKDIR to carry mode 0755.");
            }

            Require(executor.CallsFor(8).Count == 2 && executor.CallsFor(9).Count == 2, "Expected two CREATE and two MKDIR calls.");
            Require(((await session.Metadata.GetAttributesAsync("/a/b/created.txt", cancellationToken).ConfigureAwait(false)).Mode & 0xFFFU) == DefaultFileMode, "Expected the created file to report 0644.");
            Require(((await session.Metadata.GetAttributesAsync("/a/b", cancellationToken).ConfigureAwait(false)).Mode & 0xFFFU) == DefaultDirectoryMode, "Expected the created directory to report 0755.");

            await using OpenNfsClient readOnlyClient = server.CreateClientBuilder().WithDefaultCreateModes(Convert.ToUInt32("444", 8), DefaultDirectoryMode).Build();
            await readOnlyClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await using OpenNfsMountSession readOnlySession = await readOnlyClient.MountAsync(EphemeralOpenNfsServer.ExportPath, cancellationToken).ConfigureAwait(false);
            await readOnlySession.Directories.CreateFileAsync("/a/b/readonly.txt", failIfExists: true, cancellationToken).ConfigureAwait(false);
            uint readOnlyMode = (await readOnlySession.Metadata.GetAttributesAsync("/a/b/readonly.txt", cancellationToken).ConfigureAwait(false)).Mode & 0xFFFU;
            Require((readOnlyMode & Convert.ToUInt32("222", 8)) == 0, "Expected the in-process server to honor a requested read-only create mode, but it reported " + Convert.ToString(readOnlyMode, 8) + ".");
            if (!OperatingSystem.IsWindows())
            {
                Require(readOnlyMode == Convert.ToUInt32("444", 8), "Expected the exact requested mode on a Unix host.");
            }
        }

        private static async Task RequireStatAsync(DockerNfsV3PeerContainer peer, string exportRelativePath, string expected, CancellationToken cancellationToken)
        {
            string output = (await peer.ExecAsync(new[] { "stat", "-c", "%a %u", peer.ContainerExportRoot + exportRelativePath }, cancellationToken).ConfigureAwait(false)).Trim();
            Require(string.Equals(output, expected, StringComparison.Ordinal), "Expected stat of '" + exportRelativePath + "' on " + peer.DisplayName + " to be '" + expected + "' but found '" + output + "'.");
        }
    }
}
