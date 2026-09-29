namespace Test.Shared.Infrastructure
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;

    /// <summary>
    /// In-process OpenNFS server serving a temporary directory over MOUNT v3 and NFSv3 on loopback ephemeral ports.
    /// Built exclusively from the public OpenNFS.Server surface, exactly as documented in README.md for downstream test harnesses.
    /// </summary>
    internal sealed class EphemeralOpenNfsServer : IAsyncDisposable
    {
        internal const string ExportPath = "/export";

        private readonly OpenNfsServerApplication _application;

        private EphemeralOpenNfsServer(OpenNfsServerApplication application, string rootDirectory, string sourceRoot)
        {
            _application = application;
            RootDirectory = rootDirectory;
            SourceRoot = sourceRoot;
        }

        internal string RootDirectory { get; }

        internal string SourceRoot { get; }

        internal int MountPort => _application.MountPort;

        internal int NfsPort => _application.NfsPort;

        internal OpenNfsServerApplication Application => _application;

        internal static async Task<EphemeralOpenNfsServer> StartAsync(CancellationToken cancellationToken)
        {
            return await StartAsync(configure: null, cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<EphemeralOpenNfsServer> StartAsync(
            Action<OpenNfsServerBuilder, string>? configure,
            CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.Ephemeral", Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(rootDirectory, "export");
            Directory.CreateDirectory(sourceRoot);

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .WithServerName("opennfs-ephemeral")
                .UseFileHandleProvider(new PersistentMappingHandleProvider(Path.Combine(rootDirectory, "handles.json")))
                .AddExport(ExportPath, sourceRoot);

            if (configure is null)
            {
                builder.UseLocalFileSystem();
            }
            else
            {
                configure(builder, sourceRoot);
            }

            OpenNfsServerApplication application = builder.BuildApplication(new OpenNfsServerApplicationOptions
            {
                ListenerAddress = "127.0.0.1",
                MountPort = 0,
                NfsPort = 0,
                EnableNfs40 = false,
            });

            try
            {
                await application.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await application.DisposeAsync().ConfigureAwait(false);
                DeleteDirectory(rootDirectory);
                throw;
            }

            return new EphemeralOpenNfsServer(application, rootDirectory, sourceRoot);
        }

        internal OpenNfsClientBuilder CreateClientBuilder()
        {
            return new OpenNfsClientBuilder()
                .WithPrimaryEndpoint("127.0.0.1", NfsPort)
                .WithMountEndpoint("127.0.0.1", MountPort);
        }

        internal async Task<(OpenNfsClient Client, OpenNfsMountSession Session)> ConnectAndMountAsync(CancellationToken cancellationToken)
        {
            OpenNfsClient client = CreateClientBuilder().Build();
            try
            {
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                OpenNfsMountSession session = await client.MountAsync(ExportPath, cancellationToken).ConfigureAwait(false);
                return (client, session);
            }
            catch
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        internal string GetHostPath(string exportRelativePath)
        {
            string relative = exportRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(SourceRoot, relative);
        }

        public async ValueTask DisposeAsync()
        {
            await _application.DisposeAsync().ConfigureAwait(false);
            DeleteDirectory(RootDirectory);
        }

        internal static void DeleteDirectory(string directory)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        return;
                    }

                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        FileAttributes attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                        }
                    }

                    Directory.Delete(directory, recursive: true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
