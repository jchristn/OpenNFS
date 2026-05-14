namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;

    internal static class NfsV40NamespaceCaseSupport
    {
        internal static Task<Nfs40NamespaceCaseContext> CreateSingleExportContextAsync(CancellationToken cancellationToken)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                });

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .Build();

            return CreateContextAsync(server, cancellationToken);
        }

        internal static async Task<Nfs40NamespaceCaseContext> CreateCrossExportContextAsync(CancellationToken cancellationToken)
        {
            DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\docs"] = NfsPathKind.Directory,
                    [@"C:\exports\docs\notes.txt"] = NfsPathKind.File,
                    [@"C:\other"] = NfsPathKind.Directory,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\docs\notes.txt"] = Encoding.UTF8.GetBytes("hello-v4"),
                });

            OpenNfsServer server = new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .AddExport("/", @"C:\exports")
                .AddExport("/other", @"C:\other")
                .Build();

            Nfs40NamespaceCaseContext context = await CreateContextAsync(server, cancellationToken).ConfigureAwait(false);
            NfsFileHandle otherRootHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/other", @"C:\other"),
                cancellationToken).ConfigureAwait(false);
            return new Nfs40NamespaceCaseContext(
                context.Server,
                context.Service,
                context.RootHandle,
                context.DocsHandle,
                context.NotesHandle,
                otherRootHandle);
        }

        private static async Task<Nfs40NamespaceCaseContext> CreateContextAsync(
            OpenNfsServer server,
            CancellationToken cancellationToken)
        {
            Nfs40CompoundService service = new Nfs40CompoundService(server);
            NfsFileHandle rootHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle docsHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs"),
                cancellationToken).ConfigureAwait(false);
            NfsFileHandle notesHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/", @"C:\exports\docs\notes.txt"),
                cancellationToken).ConfigureAwait(false);

            return new Nfs40NamespaceCaseContext(server, service, rootHandle, docsHandle, notesHandle, null);
        }
    }

    internal readonly struct Nfs40NamespaceCaseContext
    {
        internal Nfs40NamespaceCaseContext(
            OpenNfsServer server,
            Nfs40CompoundService service,
            NfsFileHandle rootHandle,
            NfsFileHandle docsHandle,
            NfsFileHandle notesHandle,
            NfsFileHandle? otherRootHandle)
        {
            Server = server;
            Service = service;
            RootHandle = rootHandle;
            DocsHandle = docsHandle;
            NotesHandle = notesHandle;
            OtherRootHandle = otherRootHandle;
        }

        internal OpenNfsServer Server { get; }

        internal Nfs40CompoundService Service { get; }

        internal NfsFileHandle RootHandle { get; }

        internal NfsFileHandle DocsHandle { get; }

        internal NfsFileHandle NotesHandle { get; }

        internal NfsFileHandle? OtherRootHandle { get; }
    }
}
