namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientV40SuiteSupport;

    internal static class ClientV40MutationFoundationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedMutationApisPositive",
                    displayName: "Grouped NFSv4.0 directory mutation APIs prepare, execute, and decode successful namespace changes",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsServer server = CreateServer();
                        NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 6,
                            async client =>
                            {
                                OpenNfsCompoundPlan lookuppPlan = await client.Directories.PrepareLookupParentV40Async(
                                    docsHandle.ToArray(),
                                    cancellationToken).ConfigureAwait(false);
                                OpenNfsCompoundPlan createPlan = await client.Directories.PrepareCreateDirectoryV40Async(
                                    docsHandle.ToArray(),
                                    "newdir",
                                    cancellationToken).ConfigureAwait(false);

                                if (lookuppPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || lookuppPlan.Operations.Count != 4
                                    || createPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || createPlan.Operations.Count != 4)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 mutation planning to preserve the expected COMPOUND shapes.");
                                }

                                OpenNfsV40LookupResult parentLookup =
                                    await client.Directories.LookupParentV40Async(docsHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                OpenNfsV40CreateResult createDirectory =
                                    await client.Directories.CreateDirectoryV40Async(docsHandle.ToArray(), "newdir", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40CreateResult createSymbolicLink =
                                    await client.Directories.CreateSymbolicLinkV40Async(docsHandle.ToArray(), "generated-link", "notes.txt", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40LinkResult createHardLink =
                                    await client.Directories.CreateHardLinkV40Async(noteHandle.ToArray(), rootHandle.ToArray(), "notes-link.txt", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40RenameResult renameResult =
                                    await client.Directories.RenameV40Async(docsHandle.ToArray(), "newdir", rootHandle.ToArray(), "renamed-dir", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40DirectoryMutationResult removeResult =
                                    await client.Directories.RemoveEntryV40Async(rootHandle.ToArray(), "renamed-dir", cancellationToken).ConfigureAwait(false);

                                if (!parentLookup.IsSuccess
                                    || !parentLookup.ObjectFileHandle.Span.SequenceEqual(rootHandle.ToArray())
                                    || parentLookup.ObjectAttributes?.FileType != OpenNfsV40FileType.Directory
                                    || !createDirectory.IsSuccess
                                    || createDirectory.ObjectAttributes?.FileType != OpenNfsV40FileType.Directory
                                    || !createSymbolicLink.IsSuccess
                                    || createSymbolicLink.ObjectAttributes?.FileType != OpenNfsV40FileType.SymbolicLink
                                    || !createHardLink.IsSuccess
                                    || createHardLink.DirectoryChangeInfo is null
                                    || !renameResult.IsSuccess
                                    || renameResult.SourceDirectoryChangeInfo is null
                                    || renameResult.TargetDirectoryChangeInfo is null
                                    || !removeResult.IsSuccess
                                    || removeResult.DirectoryChangeInfo is null)
                                {
                                    throw new InvalidOperationException(
                                        "Expected grouped NFSv4.0 mutation APIs to execute and decode successful LOOKUPP, CREATE, LINK, RENAME, and REMOVE flows. "
                                        + "ParentLookup=" + parentLookup.Status.ToString()
                                        + ", CreateDirectory=" + createDirectory.Status.ToString()
                                        + ", CreateDirectoryType=" + createDirectory.ObjectAttributes?.FileType.ToString()
                                        + ", CreateSymbolicLink=" + createSymbolicLink.Status.ToString()
                                        + ", CreateSymbolicLinkType=" + createSymbolicLink.ObjectAttributes?.FileType.ToString()
                                        + ", CreateHardLink=" + createHardLink.Status.ToString()
                                        + ", Rename=" + renameResult.Status.ToString()
                                        + ", Remove=" + removeResult.Status.ToString()
                                        + ".");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedMutationApisNegative",
                    displayName: "Grouped NFSv4.0 directory mutation APIs surface negative namespace results cleanly",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        OpenNfsServer server = CreateCrossExportServer();
                        NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle noteHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                            cancellationToken).ConfigureAwait(false);
                        NfsFileHandle otherRootHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/other", @"C:\other"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 5,
                            async client =>
                            {
                                OpenNfsV40LookupResult rootParent =
                                    await client.Directories.LookupParentV40Async(rootHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                OpenNfsV40CreateResult existingDirectory =
                                    await client.Directories.CreateDirectoryV40Async(rootHandle.ToArray(), "docs", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40LinkResult invalidHardLink =
                                    await client.Directories.CreateHardLinkV40Async(docsHandle.ToArray(), rootHandle.ToArray(), "dir-link", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40RenameResult crossExportRename =
                                    await client.Directories.RenameV40Async(docsHandle.ToArray(), "notes.txt", otherRootHandle.ToArray(), "moved.txt", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40DirectoryMutationResult missingRemove =
                                    await client.Directories.RemoveEntryV40Async(rootHandle.ToArray(), "missing", cancellationToken).ConfigureAwait(false);

                                if (rootParent.Status != OpenNfsV40Status.NoEnt
                                    || existingDirectory.Status != OpenNfsV40Status.Exists
                                    || invalidHardLink.Status != OpenNfsV40Status.IsDirectory
                                    || crossExportRename.Status != OpenNfsV40Status.CrossDevice
                                    || missingRemove.Status != OpenNfsV40Status.NoEnt)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 mutation APIs to preserve negative protocol results for LOOKUPP, CREATE, LINK, RENAME, and REMOVE.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),
            };
        }
    }
}
