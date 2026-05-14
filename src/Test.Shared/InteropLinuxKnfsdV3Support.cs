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
    /// Shared execution helpers for Linux kernel-server NFSv3 interop flows.
    /// </summary>
    internal static class InteropLinuxKnfsdV3Support
    {
        internal static async Task ExecuteClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            await using DockerLinuxKnfsdServerContainer server =
                await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                OpenNfsMountV3Result mountResult = await WaitForLinuxMountV3Async(
                    "127.0.0.1",
                    server.MountPort,
                    "/export",
                    enableUdpForNfsV3: false,
                    cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected MOUNT v3 against the Linux kernel NFS server to return a usable root filehandle.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await nfsClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadDirectoryResult directoryResult =
                    await nfsClient.Directories.ReadDirectoryV3Async(
                        mountResult.RootFileHandle.ToArray(),
                        cookie: 0,
                        cookieVerifier: new byte[8],
                        count: 4096,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!directoryResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected READDIR against the Linux kernel NFS server root to succeed.");
                }

                string[] directoryNames = directoryResult.Entries
                    .Select(static entry => entry.Name)
                    .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!directoryNames.SequenceEqual(new[] { "d", "h.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFS server export to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", directoryNames) + ".");
                }

                OpenNfsV3LookupResult lookupResult =
                    await nfsClient.Directories.LookupV3Async(
                        mountResult.RootFileHandle.ToArray(),
                        "h.txt",
                        cancellationToken).ConfigureAwait(false);
                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected LOOKUP for 'h.txt' against the Linux kernel NFS server export root to succeed.");
                }

                byte[] fileHandle = lookupResult.ObjectFileHandle.ToArray();
                OpenNfsV3ReadResult initialRead =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 16, cancellationToken).ConfigureAwait(false);
                string initialContent = Encoding.UTF8.GetString(initialRead.Data.Span);
                if (!initialRead.IsSuccess || !string.Equals(initialContent, "0123456789ABCDEF", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux kernel NFS server file to contain the seeded interop payload.");
                }

                byte[] updatedBytes = Encoding.UTF8.GetBytes("FEDCBA9876543210");
                OpenNfsV3WriteResult writeResult =
                    await nfsClient.Files.WriteV3Async(
                        fileHandle,
                        offset: 0,
                        stability: OpenNfsWriteStability.FileSync,
                        data: updatedBytes,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                OpenNfsV3CommitResult commitResult =
                    await nfsClient.Files.CommitV3Async(
                        fileHandle,
                        offset: 0,
                        count: (uint)updatedBytes.Length,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                OpenNfsV3ReadResult rereadResult =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 16, cancellationToken).ConfigureAwait(false);
                OpenNfsV3LookupResult missingLookupResult =
                    await nfsClient.Directories.LookupV3Async(
                        mountResult.RootFileHandle.ToArray(),
                        "missing.txt",
                        cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess
                    || writeResult.Count != updatedBytes.Length
                    || !commitResult.IsSuccess
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux kernel NFS server flow to support grouped READ, WRITE, COMMIT, and reread successfully.");
                }

                if (missingLookupResult.Status != OpenNfsV3Status.NoEntry
                    || missingLookupResult.ObjectFileHandle.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Expected LOOKUP for a missing Linux kernel NFS server entry to return NFS3ERR_NOENT without a filehandle, but received status '"
                        + missingLookupResult.Status.ToString()
                        + "' and handle length "
                        + missingLookupResult.ObjectFileHandle.Length
                        + ".");
                }

                DockerCommandResult exportedFile = await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "exec",
                        server.ContainerName,
                        "cat",
                        "/export/h.txt",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                if (!string.Equals(exportedFile.StandardOutput.Trim(), "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux kernel NFS server export file to reflect the bytes written through the grouped client flow.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux kernel NFS server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        internal static async Task ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            await using DockerLinuxKnfsdServerContainer server =
                await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                OpenNfsMountV3Result mountResult = await WaitForLinuxMountV3Async(
                    "127.0.0.1",
                    server.MountPort,
                    "/export",
                    enableUdpForNfsV3: false,
                    cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected the negative Linux kernel NFS server client variant to obtain a root filehandle before issuing failure-path requests.");
                }

                await using OpenNfsClient warmupClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await warmupClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3LookupResult readyLookupResult = await WaitForV3LookupAsync(
                    warmupClient,
                    mountResult.RootFileHandle.ToArray(),
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);
                if (!readyLookupResult.IsSuccess || readyLookupResult.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected the negative Linux kernel NFS server client variant to warm up with a successful LOOKUP before issuing failure-path requests.");
                }

                await using OpenNfsClient negativeClient = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await negativeClient.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3LookupResult missingLookupResult =
                    await negativeClient.Directories.LookupV3Async(
                        mountResult.RootFileHandle.ToArray(),
                        "missing.txt",
                        cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV3Status.NoEntry
                    || missingLookupResult.ObjectFileHandle.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Expected LOOKUP for a missing Linux kernel NFS server entry to return NFS3ERR_NOENT without a filehandle, but received status '"
                        + missingLookupResult.Status.ToString()
                        + "' and handle length "
                        + missingLookupResult.ObjectFileHandle.Length
                        + ".");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux kernel NFS server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }
    }
}

