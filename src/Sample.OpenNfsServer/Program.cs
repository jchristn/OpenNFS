namespace Sample.OpenNfsServer
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Hosting;
    using OpenNFS.Protocol.V40.Hosting;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                SampleServerConfiguration configuration = SampleServerConfiguration.Parse(args);
                if (configuration.ShowHelp)
                {
                    Console.WriteLine(SampleServerConfiguration.GetUsage());
                    return 0;
                }

                Directory.CreateDirectory(configuration.SourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(configuration.MappingPath) ?? configuration.SourcePath);
                SampleContentSeeder.EnsureSeeded(configuration.SourcePath);

                OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                    .WithServerName(configuration.ServerName)
                    .WithListenerAddress(configuration.ListenerAddress)
                    .UseLocalFileSystem()
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(configuration.MappingPath))
                    .UseMountAuthorization(configuration.DenyMounts ? SampleMountAuthorization.DenyAll : SampleMountAuthorization.AllowAll)
                    .AddExport(configuration.ExportPath, configuration.SourcePath);

                if (configuration.NfsPort > 0)
                {
                    builder.WithListenerPort(configuration.NfsPort);
                }

                OpenNfsServer server = builder.Build();

                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellationTokenSource.Cancel();
                };

                await using OpenNfsTcpServerHost host = OpenNfsTcpServerHost.Start(
                    server,
                    listenerAddress: configuration.ListenerAddress,
                    mountPort: configuration.MountPort,
                    nfsPort: configuration.NfsPort);
                await using OpenNfsTcpNfs40ServerHost nfs40Host = OpenNfsTcpNfs40ServerHost.Start(
                    server,
                    listenerAddress: configuration.ListenerAddress,
                    nfsPort: configuration.Nfs40Port);

                Console.WriteLine("Sample.OpenNfsServer started.");
                Console.WriteLine(
                    "READY mountPort="
                    + host.MountPort
                    + " nfsPort="
                    + host.NfsPort
                    + " nfs40Port="
                    + nfs40Host.NfsPort
                    + " exportPath="
                    + configuration.ExportPath);
                Console.WriteLine("Export source: " + configuration.SourcePath);
                Console.WriteLine("Press Ctrl+C to stop.");

                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token).ConfigureAwait(false);
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Sample.OpenNfsServer failed to start.");
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }
        }
    }
}
