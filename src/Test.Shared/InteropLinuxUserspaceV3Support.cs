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
    /// Shared execution helpers for Linux userspace-server NFSv3 interop flows.
    /// </summary>
    internal static class InteropLinuxUserspaceV3Support
    {
        internal static async Task ExecuteClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            string exportDirectory = CreateTempDirectory();

            try
            {
                CreateLinuxServerExportLayout(exportDirectory);

                await using DockerLinuxNfsServerContainer server =
                    await DockerLinuxNfsServerContainer.StartAsync(exportDirectory, cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", server.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports =
                    await mountClient.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                if (exports.Count != 1
                    || !string.Equals(exports[0].ExportPath, "/export", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFS server container to expose exactly one '/export' root.");
                }

                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Expected MOUNT v3 to return a usable Linux server root filehandle, but received status '"
                        + mountResult.Status.ToString()
                        + "' with handle length "
                        + mountResult.RootFileHandle.Length
                        + ".");
                }

                byte[] rootHandle = mountResult.RootFileHandle.ToArray();

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", server.NfsPort)
                    .Build();
                await nfsClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadDirectoryResult directoryResult =
                    await nfsClient.Directories.ReadDirectoryV3Async(
                        rootHandle,
                        cookie: 0,
                        cookieVerifier: new byte[8],
                        count: 4096,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!directoryResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected READDIR against the Linux server root to succeed.");
                }

                string[] directoryNames = directoryResult.Entries
                    .Select(static entry => entry.Name)
                    .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!directoryNames.SequenceEqual(new[] { "d", "h.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux server export to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", directoryNames) + ".");
                }

                OpenNfsV3LookupResult lookupResult =
                    await nfsClient.Directories.LookupV3Async(rootHandle, "h.txt", cancellationToken).ConfigureAwait(false);
                if (!lookupResult.IsSuccess || lookupResult.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected LOOKUP for 'h.txt' against the Linux server export root to succeed.");
                }

                byte[] fileHandle = lookupResult.ObjectFileHandle.ToArray();
                OpenNfsV3ReadResult initialRead =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 16, cancellationToken).ConfigureAwait(false);

                string initialContent = Encoding.UTF8.GetString(initialRead.Data.Span);
                if (!initialRead.IsSuccess || !string.Equals(initialContent, "0123456789ABCDEF", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux server file to contain the seeded interop payload.");
                }

                byte[] updatedBytes = Encoding.UTF8.GetBytes("FEDCBA9876543210");
                OpenNfsV3WriteResult writeResult =
                    await nfsClient.Files.WriteV3Async(
                        fileHandle,
                        offset: 0,
                        stability: OpenNfsWriteStability.FileSync,
                        data: updatedBytes,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess || writeResult.Count != updatedBytes.Length)
                {
                    throw new InvalidOperationException("Expected WRITE against the Linux server file to acknowledge the full payload.");
                }

                OpenNfsV3CommitResult commitResult =
                    await nfsClient.Files.CommitV3Async(
                        fileHandle,
                        offset: 0,
                        count: (uint)updatedBytes.Length,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!commitResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected COMMIT against the Linux server file to succeed.");
                }

                OpenNfsV3ReadResult rereadResult =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 16, cancellationToken).ConfigureAwait(false);
                string rereadContent = Encoding.UTF8.GetString(rereadResult.Data.Span);

                if (!rereadResult.IsSuccess || !string.Equals(rereadContent, "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected READ after WRITE/COMMIT to return the updated Linux server payload.");
                }

                string hostFileContent = await File.ReadAllTextAsync(Path.Combine(exportDirectory, "h.txt"), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(hostFileContent, "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the host-side export file to reflect the bytes written through the real Linux NFS server.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(exportDirectory);
            }
        }

        internal static async Task ExecuteNegativeClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            string exportDirectory = CreateTempDirectory();

            try
            {
                CreateLinuxServerExportLayout(exportDirectory);

                await using DockerLinuxNfsServerContainer server =
                    await DockerLinuxNfsServerContainer.StartAsync(exportDirectory, cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", server.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected the negative Linux-server client variant to obtain a root filehandle before issuing failure-path requests.");
                }

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", server.NfsPort)
                    .Build();
                await nfsClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3LookupResult missingLookupResult =
                    await nfsClient.Directories.LookupV3Async(
                        mountResult.RootFileHandle.ToArray(),
                        "missing.txt",
                        cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV3Status.NoEntry
                    || missingLookupResult.ObjectFileHandle.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Expected LOOKUP for a missing Linux-server entry to return NFS3ERR_NOENT without a filehandle, but received status '"
                        + missingLookupResult.Status.ToString()
                        + "' and handle length "
                        + missingLookupResult.ObjectFileHandle.Length
                        + ".");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(exportDirectory);
            }
        }
    }
}

