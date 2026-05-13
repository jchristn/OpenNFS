namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V40.Hosting;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites covering real Docker-backed Linux client and server interop.
    /// </summary>
    public static class InteropSuites
    {
        /// <summary>
        /// Creates the shared Linux interop suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            DockerInteropEnvironmentProbe probe = DockerInteropEnvironmentProbe.Current;

            return new TestSuiteDescriptor(
                suiteId: "InteropSuites",
                displayName: "Peer and Linux Interop",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstOpenNfsServer",
                        displayName: "OpenNFS.Client mounts, browses, reads, writes, and commits against a live OpenNFS server host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteClientAgainstOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSeesDeniedMountFromOpenNfsServer",
                        displayName: "OpenNFS.Client receives a denied mount result from a live OpenNFS server host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteNegativeClientAgainstOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstSampleOpenNfsServerArtifact",
                        displayName: "OpenNFS.Client mounts, browses, reads, writes, and commits against the runnable Sample.OpenNfsServer artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteClientAgainstSampleOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSeesDeniedMountFromSampleOpenNfsServerArtifact",
                        displayName: "OpenNFS.Client receives a denied mount result from the runnable Sample.OpenNfsServer artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteNegativeClientAgainstSampleOpenNfsServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientBrowsesAndLocksAgainstOpenNfsServerOverNfs40",
                        displayName: "OpenNFS.Client browses, mutates, opens, and locks against a live OpenNFS NFSv4.0 host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteClientAgainstOpenNfsServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstOpenNfsServerOverNfs40",
                        displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a live OpenNFS host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientBrowsesAndManagesAgainstSampleOpenNfsServerArtifactOverNfs40",
                        displayName: "OpenNFS.Client browses, reads, mutates, and opens against the runnable Sample.OpenNfsServer artifact over NFSv4.0",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstSampleOpenNfsServerArtifactOverNfs40",
                        displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and read results against the runnable Sample.OpenNfsServer artifact",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        executeAsync: ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstLinuxNfs40Server",
                        displayName: "OpenNFS.Client browses, opens, writes, and commits against a real Linux NFSv4.0 server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteClientAgainstLinuxServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxNfs40Server",
                        displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a real Linux NFSv4.0 server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeClientAgainstLinuxServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstLinuxNfs41Server",
                        displayName: "OpenNFS.Client establishes an NFSv4.1 session and round-trips read/write/remove against a real Linux NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteClientAgainstLinuxServerOverNfs41Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServer",
                        displayName: "OpenNFS.Client mounts, reads, writes, and commits against a real Linux kernel NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteClientAgainstLinuxKnfsdServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServer",
                        displayName: "OpenNFS.Client surfaces negative lookup behavior against a real Linux kernel NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeClientAgainstLinuxKnfsdServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstLinuxKnfsdServerOverNfs40",
                        displayName: "OpenNFS.Client browses, opens, writes, and commits against a real Linux kernel NFSv4.0 server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxKnfsdServerOverNfs40",
                        displayName: "OpenNFS.Client surfaces negative NFSv4.0 lookup and state results against a real Linux kernel NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientReadsAndWritesAgainstLinuxNfsServer",
                        displayName: "OpenNFS.Client mounts, reads, writes, and commits against a real Linux NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteClientAgainstLinuxServerAsync),

                    new TestCaseDescriptor(
                        suiteId: "InteropSuites",
                        caseId: "OpenNfsClientSurfacesNegativeResultsAgainstLinuxNfsServer",
                        displayName: "OpenNFS.Client surfaces negative lookup behavior against a real Linux NFS server container",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteNegativeClientAgainstLinuxServerAsync),

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
                        caseId: "LinuxKernelClientMountsOpenNfsServerOverNfs40",
                        displayName: "A real Linux kernel NFSv4.0 client mounts, reads, writes, and deletes through an OpenNFS server host",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                        skip: !probe.IsAvailable,
                        skipReason: probe.SkipReason,
                        executeAsync: ExecuteLinuxClientAgainstOpenNfsServerOverNfs40Async),

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
                },
                beforeSuiteAsync: probe.IsAvailable
                    ? cancellationToken => new ValueTask(DockerInteropImages.EnsureBuiltAsync(cancellationToken))
                    : null);
        }

        private static async Task ExecuteClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteNegativeClientAgainstOpenNfsServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteNegativeClientAgainstSampleOpenNfsServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            OpenNfsServer server = await CreateOpenNfsV40InteropServerAsync(
                cancellationToken,
                includeAcls: true,
                includeDelegations: true).ConfigureAwait(false);

            await using OpenNfsTcpNfs40ServerHost host = OpenNfsTcpNfs40ServerHost.Start(
                server,
                listenerAddress: "0.0.0.0",
                nfsPort: 0);
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.NfsPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 PUTROOTFH against the OpenNFS host to return a usable root filehandle.");
            }

            byte[] rootHandle = rootResult.ObjectFileHandle.ToArray();
            OpenNfsV40ReadDirectoryResult rootDirectoryResult = await client.Directories.ReadDirectoryV40Async(
                rootHandle,
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            string[] rootNames = rootDirectoryResult.Entries
                .Select(static entry => entry.Name)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
            if (!rootDirectoryResult.IsSuccess || !rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
            {
                throw new InvalidOperationException(
                    "Expected the OpenNFS NFSv4.0 root listing to contain exactly 'docs' and 'hello.txt', but found: " + string.Join(", ", rootNames) + ".");
            }

            OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(rootHandle, "docs", cancellationToken).ConfigureAwait(false);
            if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'docs' to succeed against the OpenNFS host.");
            }

            byte[] docsHandle = docsLookup.ObjectFileHandle.ToArray();
              OpenNfsV40LookupResult noteLookup = await client.Directories.LookupV40Async(docsHandle, "notes.txt", cancellationToken).ConfigureAwait(false);
              if (!noteLookup.IsSuccess || noteLookup.ObjectFileHandle.Length == 0)
              {
                  throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'notes.txt' to succeed against the OpenNFS host.");
              }

              byte[] noteHandle = noteLookup.ObjectFileHandle.ToArray();
            OpenNfsV40SecurityInfoResult securityInfoResult = await client.Directories.GetSecurityInfoV40Async(
                docsHandle,
                "notes.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAttributesResult identityAttributesResult = await client.Files.GetAttributesV40Async(
                noteHandle,
                new[]
                {
                    OpenNfsV40AttributeKind.Type,
                    OpenNfsV40AttributeKind.Owner,
                    OpenNfsV40AttributeKind.OwnerGroup,
                },
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult initialAclResult = await client.Files.GetAclV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetAclResult setAclResult = await client.Files.SetAclV40Async(
                noteHandle,
                new[]
                {
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Allow,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.ReadData
                            | OpenNfsV40AclPermissionMask.WriteData
                            | OpenNfsV40AclPermissionMask.ReadAcl,
                        "interop-user@example.test"),
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Deny,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.Delete,
                        "EVERYONE@"),
                },
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetIdentityResult setIdentityResult = await client.Identity.SetOwnerAndGroupV40Async(
                noteHandle,
                "interop-updated-owner@example.test",
                "interop-updated-group@example.test",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult updatedAclResult = await client.Files.GetAclV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetIdentityResult updatedIdentityResult = await client.Identity.GetOwnerAndGroupV40Async(
                noteHandle,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult noteRead = await client.Files.ReadV40Async(noteHandle, 0UL, 64U, cancellationToken).ConfigureAwait(false);
            if (!noteRead.IsSuccess || !string.Equals(Encoding.UTF8.GetString(noteRead.Data.Span), "hello-v40", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the OpenNFS NFSv4.0 read path to return the seeded note payload.");
            }

            if (!securityInfoResult.IsSuccess
                || securityInfoResult.SecurityFlavors.Count != 2
                || securityInfoResult.SecurityFlavors[0].Flavor != OpenNfsRpcAuthenticationFlavor.AuthNone
                || securityInfoResult.SecurityFlavors[1].Flavor != OpenNfsRpcAuthenticationFlavor.AuthSys
                || !identityAttributesResult.IsSuccess
                || !string.Equals(identityAttributesResult.Attributes?.Owner, "interop-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(identityAttributesResult.Attributes?.OwnerGroup, "interop-group@example.test", StringComparison.Ordinal)
                || !initialAclResult.IsSuccess
                || initialAclResult.SupportedAcls != (OpenNfsV40AclSupport.AllowAcl | OpenNfsV40AclSupport.DenyAcl)
                || initialAclResult.Entries.Count != 1
                || !string.Equals(initialAclResult.Entries[0].Who, "EVERYONE@", StringComparison.Ordinal)
                || !setAclResult.IsSuccess
                || !setIdentityResult.IsSuccess
                || setIdentityResult.Identity is null
                || !string.Equals(setIdentityResult.Identity.ServerOwner, "interop-updated-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(setIdentityResult.Identity.ServerOwnerGroup, "interop-updated-group@example.test", StringComparison.Ordinal)
                || !updatedAclResult.IsSuccess
                || updatedAclResult.Entries.Count != 2
                || !string.Equals(updatedAclResult.Entries[0].Who, "interop-user@example.test", StringComparison.Ordinal)
                || updatedAclResult.Entries[1].EntryType != OpenNfsV40AclEntryType.Deny
                || !updatedIdentityResult.IsSuccess
                || updatedIdentityResult.Identity is null
                || !string.Equals(updatedIdentityResult.Identity.ServerOwner, "interop-updated-owner@example.test", StringComparison.Ordinal)
                || !string.Equals(updatedIdentityResult.Identity.ServerOwnerGroup, "interop-updated-group@example.test", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the OpenNFS NFSv4.0 peer path to surface SECINFO, identity mapping updates, and host-backed ACL round-trips.");
            }

            OpenNfsV40CreateResult createDirectoryResult = await client.Directories.CreateDirectoryV40Async(
                rootHandle,
                "scratch",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DirectoryMutationResult removeDirectoryResult = await client.Directories.RemoveEntryV40Async(
                rootHandle,
                "scratch",
                cancellationToken).ConfigureAwait(false);
            if (!createDirectoryResult.IsSuccess || !removeDirectoryResult.IsSuccess)
            {
                throw new InvalidOperationException("Expected NFSv4.0 CREATE and REMOVE to manage a temporary directory on the OpenNFS host.");
            }

            byte[] clientVerifier = new byte[] { 0x56, 0x34, 0x12, 0x90, 0x78, 0x56, 0x34, 0x12 };
            OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                "interop-v40-client",
                clientVerifier,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                setClientIdResult.ClientId,
                setClientIdResult.ConfirmationVerifier.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                docsHandle,
                setClientIdResult.ClientId,
                "interop-v40-owner",
                "notes.txt",
                OpenNfsV40ShareAccess.Both,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult delegatedOpenResult = await client.Files.OpenExistingV40Async(
                docsHandle,
                setClientIdResult.ClientId,
                "interop-v40-owner-delegation",
                "notes.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DelegationReturnResult returnDelegationResult = await client.Files.ReturnDelegationV40Async(
                noteHandle,
                delegatedOpenResult.Delegation!.StateId,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                openResult.StateId!,
                2U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LockResult lockResult = await client.Locks.LockFromOpenV40Async(
                noteHandle,
                openConfirmResult.StateId!,
                3U,
                setClientIdResult.ClientId,
                "interop-v40-lock-owner",
                1U,
                OpenNfsV40LockType.Write,
                0UL,
                5UL,
                reclaim: false,
                cancellationToken).ConfigureAwait(false);
            byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-v40-from-client");
            OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                noteHandle,
                lockResult.StateId!,
                0UL,
                OpenNfsWriteStability.DataSync,
                updatedBytes,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                noteHandle,
                0UL,
                (uint)updatedBytes.Length,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                noteHandle,
                0UL,
                64U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LockResult unlockResult = await client.Locks.UnlockV40Async(
                noteHandle,
                lockResult.StateId!,
                2U,
                OpenNfsV40LockType.Write,
                0UL,
                5UL,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                openConfirmResult.StateId!,
                4U,
                cancellationToken).ConfigureAwait(false);
            NfsReadFileResponse hostRead = await server.Settings.FileSystem.ReadFileAsync(
                new NfsReadFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    64U,
                    cancellationToken)).ConfigureAwait(false);

            if (!setClientIdResult.IsSuccess
                || !confirmClientIdResult.IsSuccess
                || !openResult.IsSuccess
                || openResult.StateId is null
                || !delegatedOpenResult.IsSuccess
                || delegatedOpenResult.Delegation is null
                || delegatedOpenResult.Delegation.DelegationType != OpenNfsV40DelegationType.Read
                || !returnDelegationResult.IsSuccess
                || !openConfirmResult.IsSuccess
                || !lockResult.IsSuccess
                || lockResult.StateId is null
                || !writeResult.IsSuccess
                || writeResult.Count != (uint)updatedBytes.Length
                || !commitResult.IsSuccess
                || !commitResult.Verifier.Span.SequenceEqual(writeResult.Verifier.Span)
                || !rereadResult.IsSuccess
                || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "updated-v40-from-client", StringComparison.Ordinal)
                || !unlockResult.IsSuccess
                || !closeResult.IsSuccess
                || !hostRead.Found
                || !string.Equals(Encoding.UTF8.GetString(hostRead.Data.Span), "updated-v40-from-client", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected NFSv4.0 stateful open, write, commit, lock, and read-back flows to succeed against the OpenNFS host.");
            }
        }

        private static async Task ExecuteNegativeClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            OpenNfsServer server = await CreateOpenNfsV40InteropServerAsync(
                cancellationToken,
                includeAcls: false,
                includeDelegations: false).ConfigureAwait(false);

            await using OpenNfsTcpNfs40ServerHost host = OpenNfsTcpNfs40ServerHost.Start(
                server,
                listenerAddress: "0.0.0.0",
                nfsPort: 0);
            await using OpenNfsClient client = new OpenNfsClientBuilder()
                .WithServer("127.0.0.1", host.NfsPort)
                .Build();
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the negative OpenNFS NFSv4.0 variant to obtain the root handle before issuing failure-path requests.");
            }

            byte[] rootHandle = rootResult.ObjectFileHandle.ToArray();
            OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                rootHandle,
                "missing.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                rootHandle,
                0UL,
                16U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40LookupResult docsLookupResult = await client.Directories.LookupV40Async(
                rootHandle,
                "docs",
                cancellationToken).ConfigureAwait(false);
              OpenNfsV40LookupResult noteLookupResult = await client.Directories.LookupV40Async(
                  docsLookupResult.ObjectFileHandle.ToArray(),
                  "notes.txt",
                  cancellationToken).ConfigureAwait(false);
            OpenNfsV40SecurityInfoResult missingSecurityInfoResult = await client.Directories.GetSecurityInfoV40Async(
                docsLookupResult.ObjectFileHandle.ToArray(),
                "missing.txt",
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40GetAclResult unsupportedAclReadResult = await client.Files.GetAclV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SetAclResult unsupportedAclWriteResult = await client.Files.SetAclV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new[]
                {
                    new OpenNfsV40AclEntry(
                        OpenNfsV40AclEntryType.Allow,
                        OpenNfsV40AclEntryFlags.None,
                        OpenNfsV40AclPermissionMask.ReadData,
                        "EVERYONE@"),
                },
                cancellationToken).ConfigureAwait(false);

              byte[] clientVerifier = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE };
              OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                  "interop-v40-negative",
                  clientVerifier,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                setClientIdResult.ClientId,
                setClientIdResult.ConfirmationVerifier.ToArray(),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult missingOpenResult = await client.Files.OpenExistingV40Async(
                rootHandle,
                setClientIdResult.ClientId,
                "interop-v40-negative-owner",
                "missing.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40OpenResult nonDelegatedOpenResult = await client.Files.OpenExistingV40Async(
                docsLookupResult.ObjectFileHandle.ToArray(),
                setClientIdResult.ClientId,
                "interop-v40-negative-owner-open",
                "notes.txt",
                OpenNfsV40ShareAccess.Read,
                OpenNfsV40ShareDeny.None,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40DelegationReturnResult badDelegationReturnResult = await client.Files.ReturnDelegationV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new OpenNfsV40StateId(1U, new byte[12]),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40WriteResult badStateWriteResult = await client.Files.WriteV40Async(
                noteLookupResult.ObjectFileHandle.ToArray(),
                new OpenNfsV40StateId(1U, new byte[12]),
                0UL,
                OpenNfsWriteStability.FileSync,
                Encoding.UTF8.GetBytes("nope"),
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40CommitResult directoryCommitResult = await client.Files.CommitV40Async(
                rootHandle,
                0UL,
                1U,
                cancellationToken).ConfigureAwait(false);
            OpenNfsV40StateIdResult badStateCloseResult = await client.Files.CloseV40Async(
                new OpenNfsV40StateId(1U, new byte[12]),
                1U,
                cancellationToken).ConfigureAwait(false);

              if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                  || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                  || !docsLookupResult.IsSuccess
                  || !noteLookupResult.IsSuccess
                  || missingSecurityInfoResult.Status != OpenNfsV40Status.NoEnt
                  || unsupportedAclReadResult.Status != OpenNfsV40Status.AttributeNotSupported
                  || unsupportedAclWriteResult.Status != OpenNfsV40Status.AttributeNotSupported
                  || !setClientIdResult.IsSuccess
                  || !confirmClientIdResult.IsSuccess
                  || missingOpenResult.Status != OpenNfsV40Status.NoEnt
                  || !nonDelegatedOpenResult.IsSuccess
                  || nonDelegatedOpenResult.Delegation is not null
                  || badDelegationReturnResult.Status != OpenNfsV40Status.BadStateId
                  || badStateWriteResult.Status != OpenNfsV40Status.BadStateId
                  || directoryCommitResult.Status != OpenNfsV40Status.IsDirectory
                  || badStateCloseResult.Status != OpenNfsV40Status.BadStateId)
              {
                  throw new InvalidOperationException("Expected negative NFSv4.0 peer validation against the OpenNFS host to preserve NOENT, SECINFO NOENT, ACL ATTRNOTSUPP, ISDIR, BAD_STATEID, and directory-commit behavior.");
              }
        }

        private static async Task ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
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

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", sampleServer.Nfs40Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveSampleExportRootV40Async(client, cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadDirectoryResult directoryResult = await client.Directories.ReadDirectoryV40Async(
                    exportRootHandle,
                    0UL,
                    new byte[8],
                    4096U,
                    cancellationToken).ConfigureAwait(false);
                string[] rootNames = directoryResult.Entries
                    .Select(static entry => entry.Name)
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!directoryResult.IsSuccess || !rootNames.SequenceEqual(new[] { "docs", "hello.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the sample NFSv4.0 export listing to contain exactly 'docs' and 'hello.txt', but found: " + string.Join(", ", rootNames) + ".");
                }

                OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "docs",
                    cancellationToken).ConfigureAwait(false);
                if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'docs' to succeed against the sample artifact.");
                }

                byte[] docsHandle = docsLookup.ObjectFileHandle.ToArray();
                OpenNfsV40LookupResult readmeLookup = await client.Directories.LookupV40Async(
                    docsHandle,
                    "nested.txt",
                    cancellationToken).ConfigureAwait(false);
                if (!readmeLookup.IsSuccess || readmeLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException("Expected NFSv4.0 LOOKUP for 'nested.txt' to succeed against the sample artifact.");
                }

                OpenNfsV40ReadResult readmeRead = await client.Files.ReadV40Async(
                    readmeLookup.ObjectFileHandle.ToArray(),
                    0UL,
                    128U,
                    cancellationToken).ConfigureAwait(false);
                if (!readmeRead.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(readmeRead.Data.Span), "nested-from-sample-opennfs", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected NFSv4.0 READ against the sample artifact to return the seeded nested payload.");
                }

                OpenNfsV40CreateResult createDirectoryResult = await client.Directories.CreateDirectoryV40Async(
                    exportRootHandle,
                    "scratch",
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40DirectoryMutationResult removeDirectoryResult = await client.Directories.RemoveEntryV40Async(
                    exportRootHandle,
                    "scratch",
                    cancellationToken).ConfigureAwait(false);
                if (!createDirectoryResult.IsSuccess || !removeDirectoryResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected NFSv4.0 CREATE and REMOVE to manage a temporary directory on the sample artifact.");
                }

                byte[] clientVerifier = new byte[] { 0x44, 0x33, 0x22, 0x11, 0x88, 0x77, 0x66, 0x55 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "sample-v40-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "sample-v40-owner",
                    "hello.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.StateId!,
                    2U,
                    cancellationToken).ConfigureAwait(false);
                byte[] updatedBytes = Encoding.UTF8.GetBytes("updated-from-sample-v40-client");
                OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    openConfirmResult.StateId!,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    updatedBytes,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    0UL,
                    (uint)updatedBytes.Length,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    0UL,
                    128U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    openConfirmResult.StateId!,
                    3U,
                    cancellationToken).ConfigureAwait(false);
                string hostContent = await File.ReadAllTextAsync(
                    Path.Combine(sourceDirectory, "hello.txt"),
                    cancellationToken).ConfigureAwait(false);

                if (!setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || openResult.ObjectFileHandle.Length == 0
                    || !openConfirmResult.IsSuccess
                    || !writeResult.IsSuccess
                    || writeResult.Count != (uint)updatedBytes.Length
                    || !commitResult.IsSuccess
                    || !commitResult.Verifier.Span.SequenceEqual(writeResult.Verifier.Span)
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "updated-from-sample-v40-client", StringComparison.Ordinal)
                    || !closeResult.IsSuccess
                    || !string.Equals(hostContent, "updated-from-sample-v40-client", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected NFSv4.0 OPEN, WRITE, COMMIT, read-back, and CLOSE against the sample artifact to succeed.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
                DeleteDirectoryIfPresent(sourceDirectory);
            }
        }

        private static async Task ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
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

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", sampleServer.Nfs40Port)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveSampleExportRootV40Async(client, cancellationToken).ConfigureAwait(false);
                OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "missing.txt",
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                    exportRootHandle,
                    0UL,
                    16U,
                    cancellationToken).ConfigureAwait(false);
                byte[] clientVerifier = new byte[] { 0x91, 0x82, 0x73, 0x64, 0x55, 0x46, 0x37, 0x28 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "sample-v40-negative",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "sample-v40-negative-owner",
                    "hello.txt",
                    OpenNfsV40ShareAccess.Read,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.StateId!,
                    2U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40WriteResult openModeWriteResult = await client.Files.WriteV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    openConfirmResult.StateId!,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    Encoding.UTF8.GetBytes("nope"),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40CommitResult directoryCommitResult = await client.Files.CommitV40Async(
                    exportRootHandle,
                    0UL,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    openConfirmResult.StateId!,
                    3U,
                    cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                    || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                    || !setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !openConfirmResult.IsSuccess
                    || openModeWriteResult.Status != OpenNfsV40Status.OpenMode
                    || directoryCommitResult.Status != OpenNfsV40Status.IsDirectory
                    || !closeResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected negative NFSv4.0 sample validation to preserve NOENT, ISDIR, directory-commit, and OPENMODE behavior.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(mappingDirectory);
                DeleteDirectoryIfPresent(sourceDirectory);
            }
        }

        private static async Task ExecuteClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxNfsV40ServerContainer server =
                await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);

                OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                    exportRootHandle,
                    0UL,
                    new byte[8],
                    4096U,
                    cancellationToken).ConfigureAwait(false);
                if (!rootListing.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 READDIR against the Linux server export root to succeed, but received status '"
                        + rootListing.Status.ToString()
                        + "'.");
                }

                    string[] rootNames = rootListing.Entries
                        .Select(static entry => entry.Name)
                        .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                        .OrderBy(static name => name, StringComparer.Ordinal)
                        .ToArray();
                    if (!rootNames.SequenceEqual(new[] { "d", "h.txt" }))
                    {
                        throw new InvalidOperationException(
                            "Expected the Linux NFSv4.0 server root to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", rootNames) + ".");
                    }

                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);
                if (!fileLookup.IsSuccess || fileLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 LOOKUP for 'h.txt' against the Linux server export root to succeed, but received status '"
                        + fileLookup.Status.ToString()
                        + "'.");
                }

                byte[] fileHandle = fileLookup.ObjectFileHandle.ToArray();
                OpenNfsV40ReadResult initialRead = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                if (!initialRead.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(initialRead.Data.Span), "0123456789ABCDEF", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server file to contain the seeded interop payload.");
                }

                byte[] clientVerifier = new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-v40-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-v40-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                (OpenNfsV40StateId openStateId, uint closeSequenceId) =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);

                byte[] updatedBytes = Encoding.UTF8.GetBytes("FEDCBA9876543210");
                OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                    fileHandle,
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    updatedBytes,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                    fileHandle,
                    0UL,
                    (uint)updatedBytes.Length,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileHandle,
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);

                if (!setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !writeResult.IsSuccess
                    || writeResult.Count != (uint)updatedBytes.Length
                    || !commitResult.IsSuccess
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "FEDCBA9876543210", StringComparison.Ordinal)
                    || !closeResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server flow to support grouped OPEN, WRITE, COMMIT, READ, and CLOSE successfully.");
                }

                DockerCommandResult exportedFile = await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "exec",
                        server.ContainerName,
                        "cat",
                        "/export-real/h.txt",
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                if (!string.Equals(exportedFile.StandardOutput.Trim(), "FEDCBA9876543210", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 server export file to reflect the bytes written through the grouped client flow.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        private static async Task ExecuteNegativeClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxNfsV40ServerContainer server =
                await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);

                OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "missing.txt",
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                    exportRootHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);

                byte[] clientVerifier = new byte[] { 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-v40-negative-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-v40-negative-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                (OpenNfsV40StateId openStateId, uint closeSequenceId) =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40WriteResult staleWriteResult = await client.Files.WriteV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    Encoding.UTF8.GetBytes("stale-stateid"),
                    cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                    || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                    || !fileLookup.IsSuccess
                    || !setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !closeResult.IsSuccess
                    || staleWriteResult.Status != OpenNfsV40Status.BadStateId)
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.0 negative path to preserve NOENT, ISDIR, and BAD_STATEID behavior.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        private static async Task ExecuteClientAgainstLinuxServerOverNfs41Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxNfsV40ServerContainer server =
                await DockerLinuxNfsV40ServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                    endpoint: new IPEndPoint(IPAddress.Loopback, server.NfsPort),
                    clientOwner: BuildInteropV41ClientOwner());
                options.RequestedSlots = 4;
                options.ConnectTimeout = TimeSpan.FromSeconds(30);
                options.CallTimeout = TimeSpan.FromSeconds(30);

                await using OpenNfsV41ClientSession session = await OpenNfsV41ClientSession
                    .EstablishAsync(options, cancellationToken)
                    .ConfigureAwait(false);
                await EnsureV41ReclaimCompleteAsync(session, cancellationToken).ConfigureAwait(false);

                OpenNfsV41MountSession mountSession = session.CreateMountSession();
                string exportRootPath = await ResolveLinuxExportRootPathV41Async(mountSession, cancellationToken).ConfigureAwait(false);
                string[] exportRootNames = await ReadLinuxExportRootNamesV41Async(mountSession, exportRootPath, cancellationToken).ConfigureAwait(false);
                if (!exportRootNames.Contains("d", StringComparer.Ordinal)
                    || !exportRootNames.Contains("h.txt", StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux NFSv4.1 export root to expose 'd' and 'h.txt', but found: "
                        + string.Join(", ", exportRootNames)
                        + ".");
                }

                string seededFilePath = CombineV41Path(exportRootPath, "h.txt");
                OpenNfsV41CompoundResult seededOpenResult = await WaitForV41OpenReadyAsync(
                    token => mountSession.Files.OpenExistingAsync(
                        seededFilePath,
                        session.ClientId,
                        "linux-v41-seeded-reader",
                        1U,
                        (uint)Nfs41Constants.OPEN4_SHARE_ACCESS_READ,
                        (uint)Nfs41Constants.OPEN4_SHARE_DENY_NONE,
                        token),
                    cancellationToken).ConfigureAwait(false);
                stateid4 seededStateId = OpenNfsV41CompoundResultReaders.GetOpenStateIdOrThrow(
                    seededOpenResult,
                    "Linux NFSv4.1 OPEN existing seeded file");
                OpenNfsV41CompoundResult seededReadResult = await mountSession.Files.ReadAsync(
                    seededFilePath,
                    seededStateId,
                    offset: 0UL,
                    count: 64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV41ReadPayload seededPayload = OpenNfsV41CompoundResultReaders.GetReadPayloadOrThrow(
                    seededReadResult,
                    "Linux NFSv4.1 READ seeded file");
                if (!string.Equals(Encoding.UTF8.GetString(seededPayload.Data), "0123456789ABCDEF", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.1 seeded file to contain the interop payload.");
                }

                _ = (await mountSession.Files.CloseAsync(
                    seededFilePath,
                    seededStateId,
                    2U,
                    cancellationToken).ConfigureAwait(false)).GetOutcomeOrThrow("Linux NFSv4.1 CLOSE seeded file");

                string createdFilePath = CombineV41Path(
                    exportRootPath,
                    "v41-created-" + Guid.NewGuid().ToString("N") + ".txt");
                byte[] writtenBytes = Encoding.UTF8.GetBytes("created-from-v41-client");

                OpenNfsV41CompoundResult createResult = await WaitForV41OpenReadyAsync(
                    token => mountSession.Files.CreateAndOpenAsync(
                        createdFilePath,
                        session.ClientId,
                        "linux-v41-writer",
                        1U,
                        (uint)Nfs41Constants.OPEN4_SHARE_ACCESS_BOTH,
                        (uint)Nfs41Constants.OPEN4_SHARE_DENY_NONE,
                        createmode4.UNCHECKED4,
                        token),
                    cancellationToken).ConfigureAwait(false);
                stateid4 createdStateId = OpenNfsV41CompoundResultReaders.GetOpenStateIdOrThrow(
                    createResult,
                    "Linux NFSv4.1 CREATE/OPEN created file");

                _ = (await mountSession.Files.WriteAsync(
                    createdFilePath,
                    createdStateId,
                    offset: 0UL,
                    stable: stable_how4.FILE_SYNC4,
                    data: writtenBytes,
                    cancellationToken).ConfigureAwait(false)).GetOutcomeOrThrow("Linux NFSv4.1 WRITE created file");

                _ = (await mountSession.Files.CloseAsync(
                    createdFilePath,
                    createdStateId,
                    2U,
                    cancellationToken).ConfigureAwait(false)).GetOutcomeOrThrow("Linux NFSv4.1 CLOSE created file");

                OpenNfsV41CompoundResult verifyOpenResult = await WaitForV41OpenReadyAsync(
                    token => mountSession.Files.OpenExistingAsync(
                        createdFilePath,
                        session.ClientId,
                        "linux-v41-verifier",
                        1U,
                        (uint)Nfs41Constants.OPEN4_SHARE_ACCESS_READ,
                        (uint)Nfs41Constants.OPEN4_SHARE_DENY_NONE,
                        token),
                    cancellationToken).ConfigureAwait(false);
                stateid4 verifyStateId = OpenNfsV41CompoundResultReaders.GetOpenStateIdOrThrow(
                    verifyOpenResult,
                    "Linux NFSv4.1 OPEN created file for verification");
                OpenNfsV41CompoundResult verifyReadResult = await mountSession.Files.ReadAsync(
                    createdFilePath,
                    verifyStateId,
                    offset: 0UL,
                    count: 4096U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV41ReadPayload verifyPayload = OpenNfsV41CompoundResultReaders.GetReadPayloadOrThrow(
                    verifyReadResult,
                    "Linux NFSv4.1 READ created file for verification");
                if (!verifyPayload.Data.SequenceEqual(writtenBytes))
                {
                    throw new InvalidOperationException("Expected the Linux NFSv4.1 created file to round-trip the written payload.");
                }

                _ = (await mountSession.Files.CloseAsync(
                    createdFilePath,
                    verifyStateId,
                    2U,
                    cancellationToken).ConfigureAwait(false)).GetOutcomeOrThrow("Linux NFSv4.1 CLOSE verification file");

                _ = (await mountSession.Directories.RemoveAsync(
                    createdFilePath,
                    cancellationToken).ConfigureAwait(false)).GetOutcomeOrThrow("Linux NFSv4.1 REMOVE created file");

                OpenNfsV41CompoundResult missingAfterRemoveResult = await mountSession.Files.OpenExistingAsync(
                    createdFilePath,
                    session.ClientId,
                    "linux-v41-post-remove",
                    1U,
                    (uint)Nfs41Constants.OPEN4_SHARE_ACCESS_READ,
                    (uint)Nfs41Constants.OPEN4_SHARE_DENY_NONE,
                    cancellationToken).ConfigureAwait(false);
                if (!missingAfterRemoveResult.ReachedServer
                    || !missingAfterRemoveResult.HasPartialResults
                    || missingAfterRemoveResult.Outcome?.Response.status != nfsstat4.NFS4ERR_NOENT)
                {
                    throw new InvalidOperationException("Expected reopening the removed Linux NFSv4.1 file to surface NFS4ERR_NOENT.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux NFSv4.1 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        private static async Task ExecuteClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
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

        private static async Task ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxKnfsdServerContainer server =
                await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(
                    "127.0.0.1",
                    server.NfsPort,
                    cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                    exportRootHandle,
                    0UL,
                    new byte[8],
                    4096U,
                    cancellationToken).ConfigureAwait(false);
                if (!rootListing.IsSuccess)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 READDIR against the Linux kernel NFS server export root to succeed, but received status '"
                        + rootListing.Status.ToString()
                        + "'.");
                }

                string[] rootNames = rootListing.Entries
                    .Select(static entry => entry.Name)
                    .Where(static name => !string.Equals(name, ".", StringComparison.Ordinal) && !string.Equals(name, "..", StringComparison.Ordinal))
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!rootNames.SequenceEqual(new[] { "d", "h.txt" }))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFSv4.0 server root to contain exactly 'd' and 'h.txt', but found: " + string.Join(", ", rootNames) + ".");
                }

                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);
                if (!fileLookup.IsSuccess || fileLookup.ObjectFileHandle.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Expected NFSv4.0 LOOKUP for 'h.txt' against the Linux kernel server export root to succeed, but received status '"
                        + fileLookup.Status.ToString()
                        + "'.");
                }

                byte[] clientVerifier = new byte[] { 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-knfsd-v40-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-knfsd-v40-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                openResult = await WaitForV40OpenReadyAsync(
                    client,
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-knfsd-v40-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    openResult,
                    cancellationToken).ConfigureAwait(false);
                (OpenNfsV40StateId openStateId, uint closeSequenceId) =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);
                byte[] fileHandle = fileLookup.ObjectFileHandle.ToArray();
                OpenNfsV40ReadResult initialRead = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);

                byte[] updatedBytes = Encoding.UTF8.GetBytes("FEDCBA9876543210");
                OpenNfsV40WriteResult writeResult = await client.Files.WriteV40Async(
                    fileHandle,
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    updatedBytes,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40CommitResult commitResult = await client.Files.CommitV40Async(
                    fileHandle,
                    0UL,
                    (uint)updatedBytes.Length,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult rereadResult = await client.Files.ReadV40Async(
                    fileHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileHandle,
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);

                if (!setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !initialRead.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(initialRead.Data.Span), "0123456789ABCDEF", StringComparison.Ordinal)
                    || !writeResult.IsSuccess
                    || writeResult.Count != (uint)updatedBytes.Length
                    || !commitResult.IsSuccess
                    || !rereadResult.IsSuccess
                    || !string.Equals(Encoding.UTF8.GetString(rereadResult.Data.Span), "FEDCBA9876543210", StringComparison.Ordinal)
                    || !closeResult.IsSuccess)
                {
                    throw new InvalidOperationException("Expected the Linux kernel NFSv4.0 server flow to support grouped OPEN, WRITE, COMMIT, READ, and CLOSE successfully.");
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
                    throw new InvalidOperationException("Expected the Linux kernel NFSv4.0 server export file to reflect the bytes written through the grouped client flow.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux kernel NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
        }

        private static async Task ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            await using DockerLinuxKnfsdServerContainer server =
                await DockerLinuxKnfsdServerContainer.StartAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                byte[] exportRootHandle = await ResolveLinuxExportRootV40Async(
                    "127.0.0.1",
                    server.NfsPort,
                    cancellationToken).ConfigureAwait(false);

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", server.NfsPort)
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                OpenNfsV40LookupResult missingLookupResult = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "missing.txt",
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40LookupResult fileLookup = await client.Directories.LookupV40Async(
                    exportRootHandle,
                    "h.txt",
                    cancellationToken).ConfigureAwait(false);

                byte[] clientVerifier = new byte[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7 };
                OpenNfsV40SetClientIdResult setClientIdResult = await client.Sessions.SetClientIdV40Async(
                    "linux-knfsd-v40-negative-client",
                    clientVerifier,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40SessionResult confirmClientIdResult = await client.Sessions.ConfirmClientIdV40Async(
                    setClientIdResult.ClientId,
                    setClientIdResult.ConfirmationVerifier.ToArray(),
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40OpenResult openResult = await client.Files.OpenExistingV40Async(
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-knfsd-v40-negative-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    cancellationToken).ConfigureAwait(false);
                openResult = await WaitForV40OpenReadyAsync(
                    client,
                    exportRootHandle,
                    setClientIdResult.ClientId,
                    "linux-knfsd-v40-negative-owner",
                    "h.txt",
                    OpenNfsV40ShareAccess.Both,
                    OpenNfsV40ShareDeny.None,
                    1U,
                    openResult,
                    cancellationToken).ConfigureAwait(false);
                (OpenNfsV40StateId openStateId, uint closeSequenceId) =
                    await ConfirmOpenIfRequiredAsync(client, openResult, 2U, cancellationToken).ConfigureAwait(false);
                OpenNfsV40ReadResult invalidReadResult = await client.Files.ReadV40Async(
                    exportRootHandle,
                    0UL,
                    64U,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40StateIdResult closeResult = await client.Files.CloseV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    closeSequenceId,
                    cancellationToken).ConfigureAwait(false);
                OpenNfsV40WriteResult staleWriteResult = await client.Files.WriteV40Async(
                    fileLookup.ObjectFileHandle.ToArray(),
                    openStateId,
                    0UL,
                    OpenNfsWriteStability.FileSync,
                    Encoding.UTF8.GetBytes("stale-stateid"),
                    cancellationToken).ConfigureAwait(false);

                if (missingLookupResult.Status != OpenNfsV40Status.NoEnt
                    || invalidReadResult.Status != OpenNfsV40Status.IsDirectory
                    || !fileLookup.IsSuccess
                    || !setClientIdResult.IsSuccess
                    || !confirmClientIdResult.IsSuccess
                    || !openResult.IsSuccess
                    || !closeResult.IsSuccess
                    || staleWriteResult.Status != OpenNfsV40Status.BadStateId)
                {
                    throw new InvalidOperationException("Expected the Linux kernel NFSv4.0 negative path to preserve NOENT, ISDIR, and BAD_STATEID behavior.");
                }
            }
            catch (Exception exception)
            {
                string logs = await server.GetLogsAsync(CancellationToken.None).ConfigureAwait(false);
                throw new InvalidOperationException(
                    exception.Message
                    + Environment.NewLine
                    + "Linux kernel NFSv4.0 server logs:"
                    + Environment.NewLine
                    + logs,
                    exception);
            }
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
                        CreateLinuxReadWriteDeleteMountCommand(host.MountPort, host.NfsPort),
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
                    || !combinedOutput.Contains("created-from-linux-client", StringComparison.Ordinal)
                    || !combinedOutput.Contains("delete-verified", StringComparison.Ordinal)
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

        private static async Task ExecuteLinuxClientAgainstOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
        {
            OpenNfsServer server = await CreateOpenNfsV40InteropServerAsync(
                cancellationToken,
                includeAcls: false,
                includeDelegations: false).ConfigureAwait(false);

            await using OpenNfsTcpNfs40ServerHost host = OpenNfsTcpNfs40ServerHost.Start(
                server,
                listenerAddress: "0.0.0.0",
                nfsPort: 0);

            DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                CreateLinuxV40ReadWriteDeleteMountCommand(host.NfsPort),
                cancellationToken).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "The Linux kernel NFSv4.0 client container failed to mount or mutate the OpenNFS server."
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
            if (!combinedOutput.Contains("hello-from-v40", StringComparison.Ordinal)
                || !combinedOutput.Contains("created-from-linux-v40-client", StringComparison.Ordinal)
                || !combinedOutput.Contains("delete-v40-verified", StringComparison.Ordinal)
                || !combinedOutput.Contains("docs", StringComparison.Ordinal)
                || !combinedOutput.Contains("hello.txt", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected the Linux kernel NFSv4.0 client container to surface and mutate the mounted OpenNFS export. Output was:"
                    + Environment.NewLine
                    + combinedOutput);
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

        private static OpenNfsServer CreateOpenNfsInteropServer(
            string mappingPath,
            string sourceRoot,
            StaticMountAuthorization? authorization = null)
        {
            Dictionary<string, NfsPathKind> pathKinds = new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
            {
                [sourceRoot] = NfsPathKind.Directory,
                [Path.Combine(sourceRoot, "d")] = NfsPathKind.Directory,
                [Path.Combine(sourceRoot, "d", "n.txt")] = NfsPathKind.File,
                [Path.Combine(sourceRoot, "h.txt")] = NfsPathKind.File,
            };

            Dictionary<string, byte[]> fileContents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.Combine(sourceRoot, "d", "n.txt")] = Encoding.UTF8.GetBytes("nested-from-opennfs"),
                [Path.Combine(sourceRoot, "h.txt")] = Encoding.UTF8.GetBytes("hello-from-opennfs"),
            };

            SerializedNfsFileSystem fileSystem = new SerializedNfsFileSystem(
                new DictionaryNfsFileSystem(pathKinds, fileContents));

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .UseFileHandleProvider(new PersistentMappingHandleProvider(mappingPath))
                .WithListenerAddress("0.0.0.0")
                .AddExport("/export", sourceRoot);

            if (authorization is not null)
            {
                builder.UseMountAuthorization(authorization);
            }

            return builder.Build();
        }

        private static async Task<OpenNfsServer> CreateOpenNfsV40InteropServerAsync(
            CancellationToken cancellationToken,
            bool includeAcls,
            bool includeDelegations)
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\exports\hello.txt"] = NfsPathKind.File,
                });

            await fileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    @"C:\exports\docs\notes.txt",
                    0UL,
                    Encoding.UTF8.GetBytes("hello-v40"),
                    NfsWriteStability.FileSync,
                    cancellationToken)).ConfigureAwait(false);
            await fileSystem.WriteFileAsync(
                new NfsWriteFileRequest(
                    @"C:\exports\hello.txt",
                    0UL,
                    Encoding.UTF8.GetBytes("hello-from-v40"),
                    NfsWriteStability.FileSync,
                    cancellationToken)).ConfigureAwait(false);

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .UseIdMapper(new TestNfsIdMapper("interop-owner@example.test", "interop-group@example.test"))
                .WithListenerAddress("0.0.0.0")
                .AddExport("/", @"C:\exports");

            if (includeAcls)
            {
                builder.UseAcls(
                    new TestNfsAcls(
                        initialEntries: new Dictionary<string, IReadOnlyList<NfsAclEntry>>(StringComparer.OrdinalIgnoreCase)
                        {
                            [@"C:\exports\docs\notes.txt"] = new[]
                            {
                                new NfsAclEntry(
                                    NfsAclEntryType.Allow,
                                    NfsAclEntryFlags.None,
                                    NfsAclPermissionMask.ReadData | NfsAclPermissionMask.ReadAcl,
                                    "EVERYONE@"),
                            },
                        }));
            }

            if (includeDelegations)
            {
                builder.UseDelegations(
                    new TestNfsDelegations(
                        new Dictionary<string, OpenNFS.Server.Delegations.NfsDelegationKind>(StringComparer.OrdinalIgnoreCase)
                        {
                            [@"C:\exports\docs\notes.txt"] = OpenNFS.Server.Delegations.NfsDelegationKind.Read,
                        }));
            }

            return builder.Build();
        }

        private static async Task<byte[]> ResolveSampleExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootResult.IsSuccess || rootResult.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected NFSv4.0 PUTROOTFH against the sample artifact to return a usable pseudo-root filehandle.");
            }

            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);
            if (!rootListing.IsSuccess)
            {
                throw new InvalidOperationException("Expected NFSv4.0 READDIR against the sample artifact root to succeed.");
            }

            bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                static entry => string.Equals(entry.Name, "docs", StringComparison.Ordinal)
                    || string.Equals(entry.Name, "hello.txt", StringComparison.Ordinal));
            if (rootAlreadyLooksLikeExport)
            {
                return rootResult.ObjectFileHandle.ToArray();
            }

            OpenNfsV40LookupResult exportsLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                "exports",
                cancellationToken).ConfigureAwait(false);
            if (!exportsLookup.IsSuccess || exportsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample NFSv4.0 pseudo-root to expose an 'exports' directory.");
            }

            OpenNfsV40LookupResult sampleLookup = await client.Directories.LookupV40Async(
                exportsLookup.ObjectFileHandle.ToArray(),
                "sample",
                cancellationToken).ConfigureAwait(false);
            if (!sampleLookup.IsSuccess || sampleLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample NFSv4.0 export tree to expose an 'exports/sample' directory.");
            }

            return sampleLookup.ObjectFileHandle.ToArray();
        }

        private static string CreateLinuxMountCommand(int mountPort, int nfsPort)
        {
            return CreateLinuxReadOnlyMountCommand(mountPort, nfsPort, "/export", "h.txt", "d/n.txt");
        }

        private static string CreateLinuxReadWriteDeleteMountCommand(int mountPort, int nfsPort)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:/export /mnt/opennfs; ",
                "cat /mnt/opennfs/h.txt; ",
                "cat /mnt/opennfs/d/n.txt; ",
                "printf 'created-from-linux-client' > /mnt/opennfs/linux-created.txt; ",
                "sync; ",
                "cat /mnt/opennfs/linux-created.txt; ",
                "rm /mnt/opennfs/linux-created.txt; ",
                "sync; ",
                "if [ -e /mnt/opennfs/linux-created.txt ]; then exit 1; fi; ",
                "echo delete-verified; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        private static string CreateLinuxReadOnlyMountCommand(
            int mountPort,
            int nfsPort,
            string exportPath,
            string primaryFilePath,
            string nestedFilePath)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:", exportPath, " /mnt/opennfs; ",
                "cat /mnt/opennfs/", primaryFilePath, "; ",
                "cat /mnt/opennfs/", nestedFilePath, "; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        private static string CreateLinuxV40ReadWriteDeleteMountCommand(int nfs40Port)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs4 -o vers=4.0,minorversion=0,port=", nfs40Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",soft,timeo=10,retrans=1 host.docker.internal:/ /mnt/opennfs; ",
                "cat /mnt/opennfs/hello.txt; ",
                "printf 'created-from-linux-v40-client' > /mnt/opennfs/linux-v40-created.txt; ",
                "sync; ",
                "cat /mnt/opennfs/linux-v40-created.txt; ",
                "rm /mnt/opennfs/linux-v40-created.txt; ",
                "sync; ",
                "if [ -e /mnt/opennfs/linux-v40-created.txt ]; then exit 1; fi; ",
                "echo delete-v40-verified; ",
                "ls -1 /mnt/opennfs; ",
                "umount /mnt/opennfs");
        }

        private static string CreateLinuxSampleMountCommand(int mountPort, int nfsPort)
        {
            return string.Concat(
                "set -eu; ",
                "mkdir -p /mnt/opennfs; ",
                "mount -t nfs -o vers=3,proto=tcp,mountproto=tcp,port=", nfsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",mountport=", mountPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",nolock,soft,timeo=10,retrans=1 host.docker.internal:/exports/sample /mnt/opennfs; ",
                "cat /mnt/opennfs/hello.txt; ",
                "cat /mnt/opennfs/docs/nested.txt; ",
                "printf 'UPDATED-FROM-LINUX-CLIENT' | dd of=/mnt/opennfs/hello.txt conv=notrunc status=none; ",
                "sync; ",
                "for attempt in 1 2 3 4 5; do ",
                "if cat /mnt/opennfs/hello.txt; then break; fi; ",
                "if [ \"$attempt\" = \"5\" ]; then exit 1; fi; ",
                "sleep 1; ",
                "done; ",
                "ls -1 /mnt/opennfs; ",
                "ls -1 /mnt/opennfs/docs; ",
                "umount /mnt/opennfs");
        }

        private static void CreateLinuxServerExportLayout(string exportDirectory)
        {
            Directory.CreateDirectory(exportDirectory);
            Directory.CreateDirectory(Path.Combine(exportDirectory, "d"));
            File.WriteAllBytes(Path.Combine(exportDirectory, "h.txt"), Encoding.UTF8.GetBytes("0123456789ABCDEF"));
            File.WriteAllBytes(Path.Combine(exportDirectory, "d", "n.txt"), Encoding.UTF8.GetBytes("nested-from-linux"));
        }

        private static async Task<OpenNfsV40LookupResult> WaitForLinuxV40RootAsync(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;
            OpenNfsV40LookupResult? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    OpenNfsV40LookupResult result = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.ObjectFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException("Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle.", lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        private static async Task<OpenNfsV3LookupResult> WaitForV3LookupAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;
            OpenNfsV3LookupResult? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    OpenNfsV3LookupResult result = await client.Directories.LookupV3Async(
                        directoryHandle,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.ObjectFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux NFSv3 server to return a usable filehandle for '" + entryName + "'.",
                    lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux NFSv3 server to return a usable filehandle for '"
                + entryName
                + "'. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        private static async Task<byte[]> ResolveLinuxExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await WaitForLinuxV40RootAsync(client, cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);

            if (rootListing.IsSuccess)
            {
                bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                    static entry => string.Equals(entry.Name, "d", StringComparison.Ordinal)
                        || string.Equals(entry.Name, "h.txt", StringComparison.Ordinal));
                if (rootAlreadyLooksLikeExport)
                {
                    return rootResult.ObjectFileHandle.ToArray();
                }
            }

            OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                "export",
                cancellationToken).ConfigureAwait(false);
            if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException(
                    "Expected the Linux NFSv4.0 pseudo-root to expose an 'export' directory, but received status '"
                    + exportLookup.Status.ToString()
                    + "'.");
            }

            return exportLookup.ObjectFileHandle.ToArray();
        }

        private static async Task<OpenNfsMountV3Result> WaitForLinuxMountV3Async(
            string host,
            int mountPort,
            string exportPath,
            bool enableUdpForNfsV3,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            Exception? lastException = null;
            OpenNfsMountV3Result? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer(host, mountPort)
                        .WithUdpForNfsV3(enableUdpForNfsV3)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    OpenNfsMountV3Result result =
                        await client.Exports.MountV3Async(exportPath, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.RootFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux MOUNT v3 server to return a usable root filehandle for '" + exportPath + "'.",
                    lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux MOUNT v3 server to return a usable root filehandle for '"
                + exportPath
                + "'. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        private static async Task<byte[]> ResolveLinuxExportRootV40Async(
            string host,
            int nfsPort,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer(host, nfsPort)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    return await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle.", lastException);
        }

        private static OpenNfsV41ClientOwner BuildInteropV41ClientOwner()
        {
            return new OpenNfsV41ClientOwner(
                verifier: new byte[] { 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51, 0x51 },
                ownerId: new byte[] { 0x49, 0x4E, 0x54, 0x45, 0x52, 0x4F, 0x50, 0x34, 0x31 });
        }

        private static async Task<string[]> ReadLinuxExportRootNamesV41Async(
            OpenNfsV41MountSession mountSession,
            string exportRootPath,
            CancellationToken cancellationToken)
        {
            OpenNfsV41CompoundResult listingResult = await mountSession.Directories.ListAsync(
                exportRootPath,
                cookie: 0UL,
                cookieVerifier: new byte[8],
                dircount: 4096U,
                maxcount: 4096U,
                attributeMask: OpenNfsV41PathOperations.BuildStatLikeAttributeMask(),
                cancellationToken).ConfigureAwait(false);

            return ExtractV41DirectoryEntryNames(
                listingResult,
                "Linux NFSv4.1 READDIR " + exportRootPath)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static async Task<string> ResolveLinuxExportRootPathV41Async(
            OpenNfsV41MountSession mountSession,
            CancellationToken cancellationToken)
        {
            string[] rootNames = await ReadLinuxExportRootNamesV41Async(
                mountSession,
                "/",
                cancellationToken).ConfigureAwait(false);

            if (rootNames.Contains("d", StringComparer.Ordinal)
                || rootNames.Contains("h.txt", StringComparer.Ordinal))
            {
                return "/";
            }

            if (rootNames.Contains("export", StringComparer.Ordinal))
            {
                return "/export";
            }

            throw new InvalidOperationException(
                "Expected the Linux NFSv4.1 pseudo-root to expose either the export contents or an 'export' directory, but found: "
                + string.Join(", ", rootNames)
                + ".");
        }

        private static IReadOnlyList<string> ExtractV41DirectoryEntryNames(
            OpenNfsV41CompoundResult result,
            string operationName)
        {
            OpenNfsV41CompoundOutcome outcome = result.GetOutcomeOrThrow(operationName);
            nfs_resop4[] results = outcome.Response.resarray ?? Array.Empty<nfs_resop4>();
            if (results.Length == 0
                || results[results.Length - 1].resop != nfs_opnum4.OP_READDIR
                || results[results.Length - 1].opreaddir?.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException(operationName + " completed without a successful READDIR result.");
            }

            List<string> names = new List<string>();
            entry4? current = results[results.Length - 1].opreaddir?.resok4?.reply?.entries;
            while (current is not null)
            {
                byte[]? nameBytes = current.name?.Value?.Value?.Value;
                if (nameBytes is null)
                {
                    throw new InvalidOperationException(operationName + " returned a directory entry without a name payload.");
                }

                names.Add(Encoding.UTF8.GetString(nameBytes));
                current = current.nextentry;
            }

            return names;
        }

        private static string CombineV41Path(string directoryPath, string entryName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entryName);

            if (string.IsNullOrEmpty(directoryPath)
                || string.Equals(directoryPath, "/", StringComparison.Ordinal))
            {
                return "/" + entryName;
            }

            return directoryPath.TrimEnd('/') + "/" + entryName;
        }

        private static async Task EnsureV41ReclaimCompleteAsync(
            OpenNfsV41ClientSession session,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(session);

            OpenNfsV41CompoundOutcome reclaimCompleteOutcome = await session.SendCompoundAsync(
                operations: new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_RECLAIM_COMPLETE,
                        opreclaim_complete = new RECLAIM_COMPLETE4args
                        {
                            rca_one_fs = false,
                        },
                    },
                },
                cacheReply: false,
                tag: "v41-reclaim-complete",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            nfsstat4? status = reclaimCompleteOutcome.Response.status;
            if (status == nfsstat4.NFS4_OK
                || status == nfsstat4.NFS4ERR_NO_GRACE
                || status == nfsstat4.NFS4ERR_COMPLETE_ALREADY
                || status == nfsstat4.NFS4ERR_NOTSUPP)
            {
                return;
            }

            throw new InvalidOperationException(
                "Expected NFSv4.1 RECLAIM_COMPLETE to succeed or be benignly unnecessary, but received "
                + status?.ToString()
                + ".");
        }

        private static async Task<OpenNfsV41CompoundResult> WaitForV41OpenReadyAsync(
            Func<CancellationToken, Task<OpenNfsV41CompoundResult>> openAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(openAsync);

            OpenNfsV41CompoundResult currentResult = await openAsync(cancellationToken).ConfigureAwait(false);
            if (currentResult.IsFullSuccess)
            {
                return currentResult;
            }

            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsRetryableV41OpenStatus(currentResult))
                {
                    return currentResult;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

                try
                {
                    currentResult = await openAsync(cancellationToken).ConfigureAwait(false);
                    if (currentResult.IsFullSuccess)
                    {
                        return currentResult;
                    }
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux NFSv4.1 server to allow stateful OPEN after startup grace.",
                    lastException);
            }

            return currentResult;
        }

        private static bool IsRetryableV41OpenStatus(OpenNfsV41CompoundResult result)
        {
            nfsstat4? status = result.Outcome?.Response.status;
            return result.ReachedServer
                && (status == nfsstat4.NFS4ERR_GRACE || status == nfsstat4.NFS4ERR_DELAY);
        }

        private static async Task<OpenNfsV40OpenResult> WaitForV40OpenReadyAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            OpenNfsV40OpenResult initialResult,
            CancellationToken cancellationToken)
        {
            OpenNfsV40OpenResult currentResult = initialResult;
            if (currentResult.IsSuccess)
            {
                return currentResult;
            }

            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentResult.Status != OpenNfsV40Status.Grace
                    && currentResult.Status != OpenNfsV40Status.Delay)
                {
                    return currentResult;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

                try
                {
                    currentResult = await client.Files.OpenExistingV40Async(
                        directoryHandle,
                        clientId,
                        openOwner,
                        entryName,
                        shareAccess,
                        shareDeny,
                        sequenceId,
                        cancellationToken).ConfigureAwait(false);
                    if (currentResult.IsSuccess)
                    {
                        return currentResult;
                    }
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux NFSv4.0 server to allow stateful OPEN after startup grace.",
                    lastException);
            }

            return currentResult;
        }

        private static async Task<(OpenNfsV40StateId StateId, uint NextSequenceId)> ConfirmOpenIfRequiredAsync(
            OpenNfsClient client,
            OpenNfsV40OpenResult openResult,
            uint confirmSequenceId,
            CancellationToken cancellationToken)
        {
            if (!openResult.IsSuccess || openResult.StateId is null)
            {
                throw new InvalidOperationException("Expected a successful NFSv4.0 OPEN result with a returned stateid.");
            }

            if (!openResult.RequiresConfirmation)
            {
                return (openResult.StateId, confirmSequenceId);
            }

            OpenNfsV40StateIdResult openConfirmResult;
            if (openResult.ObjectFileHandle.Length > 0)
            {
                openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    openResult.StateId,
                    confirmSequenceId,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.StateId,
                    confirmSequenceId,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!openConfirmResult.IsSuccess || openConfirmResult.StateId is null)
            {
                throw new InvalidOperationException("Expected a successful NFSv4.0 OPEN_CONFIRM result with a returned stateid.");
            }

            return (openConfirmResult.StateId, confirmSequenceId + 1U);
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "OpenNFS.Interop",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectoryIfPresent(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
