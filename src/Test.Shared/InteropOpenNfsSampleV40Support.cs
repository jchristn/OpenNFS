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
    /// Shared execution helpers for sample-artifact NFSv4.0 interop flows.
    /// </summary>
    internal static class InteropOpenNfsSampleV40Support
    {
        internal static async Task ExecuteClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
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

        internal static async Task ExecuteNegativeClientAgainstSampleOpenNfsServerOverNfs40Async(CancellationToken cancellationToken)
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
    }
}

