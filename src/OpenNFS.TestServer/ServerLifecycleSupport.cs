namespace OpenNFS.TestServer
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using static OpenNFS.TestServer.ServerBackingStoreSupport;
    using static OpenNFS.TestServer.ServerRuntimeState;

    internal static class ServerLifecycleSupport
    {
        internal static async Task StartAsync(CancellationToken cancellationToken)
        {
            if (Application is not null && Application.IsRunning)
            {
                Console.WriteLine("[INFO] Server is already running.");
                return;
            }

            EnsureBackingStoreReady();

            OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                .WithServerName(Configuration.ServerName)
                .WithListenerAddress(Configuration.ListenerAddress)
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(Configuration.MappingPath))
                .AddExport(Configuration.ExportPath, Configuration.RootPath, Configuration.ReadOnly);

            if (Configuration.DenyMounts)
            {
                builder.UseMountAuthorization(DenyAllMountAuthorization.Instance);
            }

            Application = builder.BuildApplication(
                new OpenNfsServerApplicationOptions
                {
                    EnableNfsV3 = Configuration.EnableNfsV3,
                    EnableNfs40 = Configuration.EnableNfsV40,
                    EnableNfs41 = Configuration.EnableNfsV41,
                    EnableNfs42 = Configuration.EnableNfsV42,
                    ListenerAddress = Configuration.ListenerAddress,
                    MountPort = Configuration.MountPort,
                    NfsPort = Configuration.NfsPort,
                    Nfs40Port = Configuration.Nfs40Port,
                    Nfs41Port = Configuration.Nfs41Port,
                    Nfs42Port = Configuration.Nfs42Port,
                });

            try
            {
                await Application.StartAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("[OK] Server started.");
                Console.WriteLine(
                    "READY mountPort="
                    + Application.MountPort.ToString(CultureInfo.InvariantCulture)
                    + " nfsPort="
                    + Application.NfsPort.ToString(CultureInfo.InvariantCulture)
                    + " nfs40Port="
                    + Application.Nfs40Port.ToString(CultureInfo.InvariantCulture)
                    + " nfs41Port="
                    + Application.Nfs41Port.ToString(CultureInfo.InvariantCulture)
                    + " nfs42Port="
                    + Application.Nfs42Port.ToString(CultureInfo.InvariantCulture)
                    + " exportPath="
                    + Configuration.ExportPath
                    + " rootPath="
                    + Configuration.RootPath);
            }
            catch
            {
                await Application.DisposeAsync().ConfigureAwait(false);
                Application = null;
                throw;
            }
        }

        internal static async Task StopAsync(CancellationToken cancellationToken)
        {
            if (Application is null)
            {
                return;
            }

            await Application.StopAsync(cancellationToken).ConfigureAwait(false);
            await Application.DisposeAsync().ConfigureAwait(false);
            Application = null;
            Console.WriteLine("[OK] Server stopped.");
        }

        internal static async Task ResetBackingStoreAsync(CancellationToken cancellationToken)
        {
            bool wasRunning = Application is not null && Application.IsRunning;
            if (wasRunning)
            {
                await StopAsync(cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists(Configuration.RootPath))
            {
                Directory.Delete(Configuration.RootPath, recursive: true);
            }

            if (File.Exists(Configuration.MappingPath))
            {
                File.Delete(Configuration.MappingPath);
            }

            EnsureBackingStoreReady();
            Console.WriteLine("[OK] Backing store reset and reseeded.");

            if (wasRunning)
            {
                await StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
