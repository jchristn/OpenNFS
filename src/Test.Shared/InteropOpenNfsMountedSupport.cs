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
    /// Shared execution helpers for OpenNFS mounted-session interop flows.
    /// </summary>
    internal static class InteropOpenNfsMountedSupport
    {
        internal static async Task ExecuteClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
        {
            string mappingDirectory = CreateTempDirectory();

            try
            {
                string sourceRoot = Path.Combine(@"C:\OpenNfsInterop", "Export");
                OpenNfsServer server = CreateOpenNfsInteropServer(Path.Combine(mappingDirectory, "handles.json"), sourceRoot);

                await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", host.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports =
                    await mountClient.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                if (exports.Count != 1
                    || !string.Equals(exports[0].ExportPath, "/export", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the OpenNFS server host to expose exactly one '/export' root.");
                }

                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected MOUNT v3 against the OpenNFS server host to return a usable root filehandle.");
                }

                byte[] rootHandle = mountResult.RootFileHandle.ToArray();

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", host.NfsPort)
                    .Build();
                await nfsClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadDirectoryResult rootDirectoryResult =
                    await nfsClient.Directories.ReadDirectoryV3Async(
                        rootHandle,
                        cookie: 0,
                        cookieVerifier: new byte[8],
                        count: 4096,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!rootDirectoryResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected READDIR against the OpenNFS server root to succeed.");
                }

                string[] rootNames = rootDirectoryResult.Entries
                    .Select(static entry => entry.Name)
                    .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!rootNames.SequenceEqual(new[] { "d", "h.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the OpenNFS server root listing to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", rootNames) + ".");
                }

                OpenNfsV3LookupResult nestedDirectoryLookup =
                    await nfsClient.Directories.LookupV3Async(rootHandle, "d", cancellationToken).ConfigureAwait(false);
                if (!nestedDirectoryLookup.IsSuccess || nestedDirectoryLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected LOOKUP for the nested directory to succeed against the OpenNFS server host.");
                }

                OpenNfsV3ReadDirectoryResult nestedDirectoryResult =
                    await nfsClient.Directories.ReadDirectoryV3Async(
                        nestedDirectoryLookup.ObjectFileHandle.ToArray(),
                        cookie: 0,
                        cookieVerifier: new byte[8],
                        count: 4096,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!nestedDirectoryResult.IsSuccess
                    || nestedDirectoryResult.Entries.All(static entry => !string.Equals(entry.Name, "n.txt", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Expected the nested OpenNFS server directory to contain 'n.txt'.");
                }

                OpenNfsV3LookupResult fileLookup =
                    await nfsClient.Directories.LookupV3Async(rootHandle, "h.txt", cancellationToken).ConfigureAwait(false);
                if (!fileLookup.IsSuccess || fileLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected LOOKUP for 'h.txt' to succeed against the OpenNFS server host.");
                }

                byte[] fileHandle = fileLookup.ObjectFileHandle.ToArray();
                OpenNfsV3ReadResult initialRead =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 64, cancellationToken).ConfigureAwait(false);
                string initialContent = Encoding.UTF8.GetString(initialRead.Data.Span);
                if (!initialRead.IsSuccess || !string.Equals(initialContent, "hello-from-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the OpenNFS server file to contain the seeded interop payload.");
                }

                byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-from-opennfs-client");
                OpenNfsV3WriteResult writeResult =
                    await nfsClient.Files.WriteV3Async(
                        fileHandle,
                        offset: 0,
                        stability: OpenNfsWriteStability.FileSync,
                        data: updatedBytes,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!writeResult.IsSuccess || writeResult.Count != updatedBytes.Length)
                {
                    throw new InvalidOperationException("Expected WRITE against the OpenNFS server host to acknowledge the full payload.");
                }

                OpenNfsV3CommitResult commitResult =
                    await nfsClient.Files.CommitV3Async(
                        fileHandle,
                        offset: 0,
                        count: (uint)updatedBytes.Length,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!commitResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected COMMIT against the OpenNFS server host to succeed.");
                }

                OpenNfsV3ReadResult rereadResult =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 64, cancellationToken).ConfigureAwait(false);
                string rereadContent = Encoding.UTF8.GetString(rereadResult.Data.Span);
                if (!rereadResult.IsSuccess || !string.Equals(rereadContent, "updated-from-opennfs-client", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected READ after WRITE/COMMIT to return the updated OpenNFS server payload.");
                }

                NfsReadFileResponse hostRead =
                    await server.Settings.FileSystem.ReadFileAsync(
                        new NfsReadFileRequest(
                            Path.Combine(sourceRoot, "h.txt"),
                            0,
                            64,
                            cancellationToken)).ConfigureAwait(false);
                string hostContent = Encoding.UTF8.GetString(hostRead.Data.Span);
                if (!hostRead.Found || !string.Equals(hostContent, "updated-from-opennfs-client", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the host-side OpenNFS file system to reflect the bytes written through OpenNFS.Client.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
            }
        }


        internal static async Task ExecuteNegativeClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
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

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", host.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports =
                    await mountClient.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/export", cancellationToken).ConfigureAwait(false);

                if (exports.Count != 0
                    || mountResult.Status != OpenNfsMountV3Status.AccessDenied
                    || mountResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the negative OpenNFS server variant to hide '/export' from EXPORT and deny the direct mount request.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
            }
        }


        internal static async Task ExecuteClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
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

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", sampleServer.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports =
                    await mountClient.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                if (exports.Count != 1
                    || !string.Equals(exports[0].ExportPath, "/exports/sample", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the sample artifact to expose exactly one '/exports/sample' root.");
                }

                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);
                if (!mountResult.IsSuccess || mountResult.RootFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected MOUNT v3 against the sample artifact to return a usable root filehandle.");
                }

                byte[] rootHandle = mountResult.RootFileHandle.ToArray();

                await using OpenNfsClient nfsClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", sampleServer.NfsPort)
                    .Build();
                await nfsClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV3ReadDirectoryResult rootDirectoryResult =
                    await nfsClient.Directories.ReadDirectoryV3Async(
                        rootHandle,
                        cookie: 0,
                        cookieVerifier: new byte[8],
                        count: 4096,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                string[] rootNames = rootDirectoryResult.Entries
                    .Select(static entry => entry.Name)
                    .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!rootDirectoryResult.IsSuccess || !rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the sample artifact root listing to contain exactly 'docs' and 'hello.txt', but found: " + string.Join(", ", rootNames) + ".");
                }

                OpenNfsV3LookupResult fileLookup =
                    await nfsClient.Directories.LookupV3Async(rootHandle, "hello.txt", cancellationToken).ConfigureAwait(false);
                if (!fileLookup.IsSuccess || fileLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected LOOKUP for 'hello.txt' to succeed against the sample artifact.");
                }

                byte[] fileHandle = fileLookup.ObjectFileHandle.ToArray();
                OpenNfsV3ReadResult initialRead =
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 64, cancellationToken).ConfigureAwait(false);
                if (!initialRead.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(initialRead.Data.Span), "hello-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the sample artifact file to contain the seeded sample payload.");
                }

                byte[] updatedBytes = Encoding.UTF8.GetBytes("UPDATED-FROM-OPENNFS-CLIENT");
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
                    await nfsClient.Files.ReadV3Async(fileHandle, offset: 0, count: 64, cancellationToken).ConfigureAwait(false);

                if (!writeResult.IsSuccess
                    || !commitResult.IsSuccess
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "UPDATED-FROM-OPENNFS-CLIENT", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected WRITE and COMMIT against the sample artifact to persist the updated payload.");
                }

                string updatedHostContent =
                    await File.ReadAllTextAsync(Path.Combine(sourceDirectory, "hello.txt"), cancellationToken).ConfigureAwait(false);
                if (!string.Equals(updatedHostContent, "UPDATED-FROM-OPENNFS-CLIENT", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the sample export root file to reflect the bytes written through OpenNFS.Client.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
                DeleteDirectoryIfPresent(sourceDirectory);
            }
        }


        internal static async Task ExecuteNegativeClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
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

                await using OpenNfsClient mountClient = new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("127.0.0.1", sampleServer.MountPort)
                    .Build();
                await mountClient.OpenAsync(cancellationToken).ConfigureAwait(false);

                IReadOnlyList<OpenNfsExportV3Entry> exports =
                    await mountClient.Exports.ListExportsV3Async(cancellationToken).ConfigureAwait(false);
                OpenNfsMountV3Result mountResult =
                    await mountClient.Exports.MountV3Async("/exports/sample", cancellationToken).ConfigureAwait(false);

                if (exports.Count != 0
                    || mountResult.Status != OpenNfsMountV3Status.AccessDenied
                    || mountResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the negative sample artifact variant to hide '/exports/sample' from EXPORT and deny the direct mount request.");
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

