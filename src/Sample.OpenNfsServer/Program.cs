namespace Sample.OpenNfsServer
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Security.Kerberos;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Radiant;
    using Sample.OpenNfsServer.Providers;

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

                using RadiantHost? telemetryHost = SampleTelemetryHost.TryStart(configuration.Telemetry);
                Directory.CreateDirectory(configuration.SourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(configuration.MappingPath) ?? configuration.SourcePath);
                SampleContentSeeder.EnsureSeeded(configuration.SourcePath);
                SampleDurableFileSystem fileSystem = new SampleDurableFileSystem(
                    configuration.SourcePath,
                    configuration.MappingPath,
                    configuration.Owner,
                    configuration.OwnerGroup);

                OpenNfsServerBuilder builder = new OpenNfsServerBuilder()
                    .WithServerName(configuration.ServerName)
                    .WithListenerAddress(configuration.ListenerAddress)
                    .UseFileSystem(fileSystem)
                    .UseFileHandleProvider(new PersistentMappingHandleProvider(configuration.MappingPath))
                    .UseCopyClone(SampleCopyCloneCapability.Instance)
                    .UseSparseFiles(SampleSparseCapability.Instance)
                    .UseMountAuthorization(configuration.DenyMounts ? SampleMountAuthorization.DenyAll : SampleMountAuthorization.AllowAll)
                    .AddExport(configuration.ExportPath, configuration.SourcePath);

                if (!string.IsNullOrWhiteSpace(configuration.KerberosTargetSpn))
                {
                    if (!string.IsNullOrWhiteSpace(configuration.KerberosKeytab))
                    {
                        Environment.SetEnvironmentVariable("KRB5_KTNAME", "FILE:" + configuration.KerberosKeytab);
                    }

                    OpenNfsKerberosMechanismOptions kerberosOptions = new OpenNfsKerberosMechanismOptions(configuration.KerberosTargetSpn);
                    builder.UseRpcSecGssMechanism(new OpenNfsKerberosMechanism(kerberosOptions));
                }

                if (configuration.NfsPort > 0)
                {
                    builder.WithListenerPort(configuration.NfsPort);
                }

                await using OpenNfsServerApplication application = builder.BuildApplication(
                    new OpenNfsServerApplicationOptions
                    {
                        ListenerAddress = configuration.ListenerAddress,
                        MountPort = configuration.MountPort,
                        NfsPort = configuration.NfsPort,
                        Nfs40Port = configuration.Nfs40Port,
                    });

                using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellationTokenSource.Cancel();
                };

                await application.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                Console.WriteLine("Sample.OpenNfsServer started.");
                Console.WriteLine(
                    "READY mountPort="
                    + application.MountPort
                    + " nfsPort="
                    + application.NfsPort
                    + " nfs40Port="
                    + application.Nfs40Port
                    + " exportPath="
                    + configuration.ExportPath
                    + " kerberos="
                    + (string.IsNullOrWhiteSpace(configuration.KerberosTargetSpn) ? "off" : configuration.KerberosTargetSpn));
                Console.WriteLine("Export source: " + configuration.SourcePath);
                Console.WriteLine(
                    "Telemetry: "
                    + (telemetryHost is null
                        ? "off"
                        : "OTLP " + configuration.Telemetry.OtlpEndpoint
                            + (configuration.Telemetry.PrometheusEnabled
                                ? ", Prometheus http://" + configuration.Telemetry.PrometheusHostname + ":" + configuration.Telemetry.PrometheusPort + "/metrics"
                                : string.Empty)));
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
