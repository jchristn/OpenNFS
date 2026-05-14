namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientV40SuiteSupport;

    internal static class ClientV40ReadOnlyFoundationCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedApisPositive",
                    displayName: "Grouped NFSv4.0 file and directory APIs prepare, execute, and decode successful read-only flows",
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
                        NfsFileHandle shortcutHandle = await server.CreateFileHandleAsync(
                            new NfsFileHandleTarget("/", @"C:\exports\docs\shortcut"),
                            cancellationToken).ConfigureAwait(false);

                        await RunAgainstLoopbackServiceAsync(
                            server,
                            expectedCallCount: 6,
                            async client =>
                            {
                                OpenNfsCompoundPlan lookupPlan = await client.Directories.PrepareLookupV40Async(
                                    docsHandle.ToArray(),
                                    "notes.txt",
                                    cancellationToken).ConfigureAwait(false);
                                OpenNfsCompoundPlan readPlan = await client.Files.PrepareReadV40Async(
                                    noteHandle.ToArray(),
                                    0UL,
                                    5U,
                                    cancellationToken).ConfigureAwait(false);

                                if (lookupPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || lookupPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                    || lookupPlan.Operations.Count != 4
                                    || readPlan.ProtocolVersion != OpenNfsProtocolVersion.Nfs40
                                    || readPlan.TransportPolicy != OpenNfsClientTransportPolicy.TcpOnly
                                    || readPlan.Operations.Count != 2)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 planning to stay on TCP and emit the expected COMPOUND shape.");
                                }

                                OpenNfsV40GetAttributesResult getattrResult =
                                    await client.Files.GetAttributesV40Async(noteHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                OpenNfsV40AccessResult accessResult =
                                    await client.Files.AccessV40Async(noteHandle.ToArray(), OpenNfsV40AccessMask.Read, cancellationToken).ConfigureAwait(false);
                                OpenNfsV40LookupResult lookupResult =
                                    await client.Directories.LookupV40Async(docsHandle.ToArray(), "notes.txt", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadResult readResult =
                                    await client.Files.ReadV40Async(noteHandle.ToArray(), 0UL, 5U, cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadLinkResult readLinkResult =
                                    await client.Files.ReadLinkV40Async(shortcutHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadDirectoryResult readDirectoryResult =
                                    await client.Directories.ReadDirectoryV40Async(rootHandle.ToArray(), 0UL, new byte[8], 4096U, cancellationToken).ConfigureAwait(false);

                                if (!getattrResult.IsSuccess
                                    || getattrResult.Attributes?.FileType != OpenNfsV40FileType.RegularFile
                                    || getattrResult.Attributes.SizeBytes != 8UL
                                    || !accessResult.IsSuccess
                                    || accessResult.GrantedAccess != OpenNfsV40AccessMask.Read
                                    || !lookupResult.IsSuccess
                                    || !lookupResult.ObjectFileHandle.Span.SequenceEqual(noteHandle.ToArray())
                                    || lookupResult.ObjectAttributes?.FileType != OpenNfsV40FileType.RegularFile
                                    || !readResult.IsSuccess
                                    || Encoding.UTF8.GetString(readResult.Data.Span) != "hello"
                                    || !readLinkResult.IsSuccess
                                    || !string.Equals(readLinkResult.TargetPath, "notes.txt", StringComparison.Ordinal)
                                    || !readDirectoryResult.IsSuccess
                                    || readDirectoryResult.Entries.Count != 1
                                    || !string.Equals(readDirectoryResult.Entries[0].Name, "docs", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 APIs to execute and decode the current read-only protocol surface.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),

                new TestCaseDescriptor(
                    suiteId: "ClientV40Suites",
                    caseId: "GroupedApisNegative",
                    displayName: "Grouped NFSv4.0 file and directory APIs surface negative protocol results cleanly",
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
                            expectedCallCount: 4,
                            async client =>
                            {
                                OpenNfsV40LookupResult missingLookup =
                                    await client.Directories.LookupV40Async(docsHandle.ToArray(), "missing.txt", cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadResult directoryRead =
                                    await client.Files.ReadV40Async(rootHandle.ToArray(), 0UL, 16U, cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadLinkResult invalidReadLink =
                                    await client.Files.ReadLinkV40Async(noteHandle.ToArray(), cancellationToken).ConfigureAwait(false);
                                OpenNfsV40ReadDirectoryResult badCookieRead =
                                    await client.Directories.ReadDirectoryV40Async(rootHandle.ToArray(), 1UL, new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }, 4096U, cancellationToken).ConfigureAwait(false);

                                if (missingLookup.Status != OpenNfsV40Status.NoEnt
                                    || directoryRead.Status != OpenNfsV40Status.IsDirectory
                                    || invalidReadLink.Status != OpenNfsV40Status.Invalid
                                    || badCookieRead.Status != OpenNfsV40Status.BadCookie)
                                {
                                    throw new InvalidOperationException("Expected grouped NFSv4.0 APIs to preserve negative protocol results for missing LOOKUP, invalid READ, invalid READLINK, and bad-cookie READDIR.");
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }),
            };
        }
    }
}
