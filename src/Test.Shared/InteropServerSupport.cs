namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;

    internal static class InteropServerSupport
    {
        internal static OpenNfsServer CreateOpenNfsInteropServer(
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

        internal static async Task<OpenNfsServer> CreateOpenNfsV40InteropServerAsync(
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

        internal static void CreateLinuxServerExportLayout(string exportDirectory)
        {
            Directory.CreateDirectory(exportDirectory);
            Directory.CreateDirectory(Path.Combine(exportDirectory, "d"));
            File.WriteAllBytes(Path.Combine(exportDirectory, "h.txt"), Encoding.UTF8.GetBytes("0123456789ABCDEF"));
            File.WriteAllBytes(Path.Combine(exportDirectory, "d", "n.txt"), Encoding.UTF8.GetBytes("nested-from-linux"));
        }

        internal static string CreateTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "OpenNFS.Interop",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        internal static void DeleteDirectoryIfPresent(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
