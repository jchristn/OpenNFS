namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.FileSystems;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Built-in public local file-system surface cases.
    /// </summary>
    internal static class ServerSurfaceLocalFileSystemCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ServerSurfaceSuites",
                    caseId: "BuiltInLocalFileSystemSupportsRealDiskOperations",
                    displayName: "Built-in local file system supports real disk-backed export operations",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        string exportRoot = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"), "export");
                        string seedFilePath = Path.Combine(exportRoot, "seed.txt");
                        string createdFilePath = Path.Combine(exportRoot, "created.txt");
                        string renamedFilePath = Path.Combine(exportRoot, "renamed.txt");

                        try
                        {
                            Directory.CreateDirectory(exportRoot);
                            await File.WriteAllBytesAsync(seedFilePath, Encoding.UTF8.GetBytes("seed"), cancellationToken).ConfigureAwait(false);

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseLocalFileSystem()
                                .AddExport("/exports/disk", exportRoot)
                                .Build();

                            if (!ReferenceEquals(server.Settings.FileSystem, LocalNfsFileSystem.Default))
                            {
                                throw new InvalidOperationException("Expected UseLocalFileSystem() to wire the built-in public local file system.");
                            }

                            NfsGetExportsResponse exportResponse =
                                await server.GetExportsAsync(new NfsGetExportsRequest(cancellationToken)).ConfigureAwait(false);
                            if (exportResponse.Exports.Count != 1
                                || !string.Equals(exportResponse.Exports[0].SourcePath, exportRoot, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to validate and expose the configured disk-backed export.");
                            }

                            NfsLookupPathResponse lookupResponse =
                                await server.Settings.FileSystem.LookupPathAsync(
                                    new NfsLookupPathRequest(exportRoot, "seed.txt", cancellationToken)).ConfigureAwait(false);
                            if (!lookupResponse.PathInfo.Exists || lookupResponse.PathInfo.Kind != NfsPathKind.File)
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to resolve a real child file beneath the export root.");
                            }

                            NfsReadFileResponse readResponse =
                                await server.Settings.FileSystem.ReadFileAsync(
                                    new NfsReadFileRequest(seedFilePath, 0, 16, cancellationToken)).ConfigureAwait(false);
                            if (Encoding.UTF8.GetString(readResponse.Data.ToArray()) != "seed")
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to read bytes from a real host file.");
                            }

                            NfsCreatePathResponse createResponse =
                                await server.Settings.FileSystem.CreatePathAsync(
                                    new NfsCreatePathRequest(exportRoot, "created.txt", NfsPathKind.File, failIfExists: true, cancellationToken)).ConfigureAwait(false);
                            if (!createResponse.CreatedNew || createResponse.PathInfo.Kind != NfsPathKind.File)
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to create a new host file beneath the export root.");
                            }

                            NfsWriteFileResponse writeResponse =
                                await server.Settings.FileSystem.WriteFileAsync(
                                    new NfsWriteFileRequest(
                                        createdFilePath,
                                        0,
                                        Encoding.UTF8.GetBytes("hello"),
                                        NfsWriteStability.FileSync,
                                        cancellationToken)).ConfigureAwait(false);
                            if (writeResponse.CommittedStability != NfsWriteStability.FileSync || writeResponse.BytesWritten != 5)
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to write and durably acknowledge real host bytes.");
                            }

                            _ = await server.Settings.FileSystem.CommitFileAsync(
                                new NfsCommitFileRequest(createdFilePath, 0, 5, cancellationToken)).ConfigureAwait(false);

                            NfsRenamePathResponse renameResponse =
                                await server.Settings.FileSystem.RenamePathAsync(
                                    new NfsRenamePathRequest(
                                        exportRoot,
                                        "created.txt",
                                        exportRoot,
                                        "renamed.txt",
                                        NfsPathKind.File,
                                        replaceExistingDestination: false,
                                        cancellationToken)).ConfigureAwait(false);
                            if (renameResponse.SourcePathInfo.Exists
                                || !renameResponse.DestinationPathInfo.Exists
                                || renameResponse.DestinationPathInfo.Kind != NfsPathKind.File)
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to rename a real host file beneath the export root.");
                            }

                            NfsDeletePathResponse deleteResponse =
                                await server.Settings.FileSystem.DeletePathAsync(
                                    new NfsDeletePathRequest(exportRoot, "renamed.txt", NfsPathKind.File, cancellationToken)).ConfigureAwait(false);
                            if (deleteResponse.PathInfo.Exists || File.Exists(renamedFilePath))
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to delete a real host file beneath the export root.");
                            }
                        }
                        finally
                        {
                            if (Directory.Exists(Path.GetDirectoryName(exportRoot) ?? exportRoot))
                            {
                                Directory.Delete(Path.GetDirectoryName(exportRoot) ?? exportRoot, recursive: true);
                            }
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "ServerSurfaceSuites",
                    caseId: "BuiltInLocalFileSystemReportsNegativeDiskCases",
                    displayName: "Built-in local file system reports missing paths and existing entries truthfully",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        string exportRoot = Path.Combine(Path.GetTempPath(), "OpenNFS.Tests", Guid.NewGuid().ToString("N"), "export");
                        string existingFilePath = Path.Combine(exportRoot, "existing.txt");

                        try
                        {
                            Directory.CreateDirectory(exportRoot);
                            await File.WriteAllBytesAsync(existingFilePath, Encoding.UTF8.GetBytes("alpha"), cancellationToken).ConfigureAwait(false);

                            OpenNfsServer server = new OpenNfsServerBuilder()
                                .UseLocalFileSystem()
                                .AddExport("/exports/disk", exportRoot)
                                .Build();

                            NfsLookupPathResponse missingLookupResponse =
                                await server.Settings.FileSystem.LookupPathAsync(
                                    new NfsLookupPathRequest(exportRoot, "missing.txt", cancellationToken)).ConfigureAwait(false);
                            if (missingLookupResponse.PathInfo.Exists || missingLookupResponse.PathInfo.Kind != NfsPathKind.Missing)
                            {
                                throw new InvalidOperationException("Expected the built-in local file system to report missing child entries truthfully.");
                            }

                            NfsReadFileResponse missingReadResponse =
                                await server.Settings.FileSystem.ReadFileAsync(
                                    new NfsReadFileRequest(Path.Combine(exportRoot, "missing.txt"), 0, 16, cancellationToken)).ConfigureAwait(false);
                            if (missingReadResponse.Found || !missingReadResponse.EndOfFile || missingReadResponse.Data.Length != 0)
                            {
                                throw new InvalidOperationException("Expected reads against a missing host file to report not-found without phantom data.");
                            }

                            NfsCreatePathResponse existingCreateResponse =
                                await server.Settings.FileSystem.CreatePathAsync(
                                    new NfsCreatePathRequest(exportRoot, "existing.txt", NfsPathKind.File, failIfExists: true, cancellationToken)).ConfigureAwait(false);
                            if (existingCreateResponse.CreatedNew
                                || existingCreateResponse.PathInfo.Kind != NfsPathKind.File
                                || !string.Equals(existingCreateResponse.PathInfo.Path, existingFilePath, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected create requests against an existing host path to report reuse instead of pretending a new entry was created.");
                            }

                            NfsDeletePathResponse missingDeleteResponse =
                                await server.Settings.FileSystem.DeletePathAsync(
                                    new NfsDeletePathRequest(exportRoot, "still-missing.txt", NfsPathKind.File, cancellationToken)).ConfigureAwait(false);
                            if (missingDeleteResponse.PathInfo.Exists || missingDeleteResponse.PathInfo.Kind != NfsPathKind.Missing)
                            {
                                throw new InvalidOperationException("Expected deletes against a missing host file to report the resulting path as missing.");
                            }
                        }
                        finally
                        {
                            if (Directory.Exists(Path.GetDirectoryName(exportRoot) ?? exportRoot))
                            {
                                Directory.Delete(Path.GetDirectoryName(exportRoot) ?? exportRoot, recursive: true);
                            }
                        }
                    }),
            };
        }
    }
}
