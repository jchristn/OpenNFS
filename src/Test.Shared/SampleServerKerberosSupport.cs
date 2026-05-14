namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;
    using Test.Shared.Infrastructure;    using static Test.Shared.SampleServerSuiteSupport;

    /// <summary>
    /// Shared execution helpers for sample artifact Kerberos probe flows.
    /// </summary>
    internal static class SampleServerKerberosSupport
    {
        internal static async Task ExecuteKerberosMountAsync(System.Threading.CancellationToken cancellationToken)
        {
            const string TargetSpn = "nfs/sample.example.test@EXAMPLE.TEST";
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleKrb", Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourcePath);

                await using SampleOpenNfsServerProcess process = await SampleOpenNfsServerProcess.StartAsync(
                    sourcePath,
                    mappingPath,
                    denyMounts: false,
                    kerberosTargetSpn: TargetSpn,
                    kerberosKeytab: null,
                    cancellationToken).ConfigureAwait(false);

                if (!string.Equals(process.KerberosTargetSpn, TargetSpn, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The sample artifact must report the configured Kerberos SPN on its READY line. Combined output: "
                        + Environment.NewLine
                        + process.GetCombinedOutput());
                }

                auth_stat observedStatus = await SendRpcSecGssDataNullCallAsync(
                    process.MountPort,
                    cancellationToken).ConfigureAwait(false);

                if (observedStatus != auth_stat.RPCSEC_GSS_CTXPROBLEM)
                {
                    throw new InvalidOperationException(
                        "When the sample registers a Kerberos mechanism, an RPCSEC_GSS DATA call referencing an unknown context handle must be rejected with RPCSEC_GSS_CTXPROBLEM by the dispatcher's authenticator. Observed: "
                        + observedStatus
                        + Environment.NewLine
                        + process.GetCombinedOutput());
                }
            }
            finally
            {
                TryDeleteDirectory(rootDirectory);
            }
        }

        internal static async Task ExecuteKerberosMountNotConfiguredAsync(System.Threading.CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.SampleKrb", Guid.NewGuid().ToString("N"));
            string sourcePath = Path.Combine(rootDirectory, "export");
            string mappingPath = Path.Combine(rootDirectory, "handles.json");

            try
            {
                Directory.CreateDirectory(sourcePath);

                await using SampleOpenNfsServerProcess process = await SampleOpenNfsServerProcess.StartAsync(
                    sourcePath,
                    mappingPath,
                    denyMounts: false,
                    cancellationToken).ConfigureAwait(false);

                if (!string.Equals(process.KerberosTargetSpn, "off", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Without a configured Kerberos SPN the sample artifact must report kerberos=off on its READY line. Observed: "
                        + process.KerberosTargetSpn);
                }

                auth_stat observedStatus = await SendRpcSecGssDataNullCallAsync(
                    process.MountPort,
                    cancellationToken).ConfigureAwait(false);

                if (observedStatus != auth_stat.AUTH_TOOWEAK)
                {
                    throw new InvalidOperationException(
                        "When no Kerberos mechanism is registered, the dispatcher must reject RPCSEC_GSS calls with AUTH_TOOWEAK. Observed: "
                        + observedStatus
                        + Environment.NewLine
                        + process.GetCombinedOutput());
                }
            }
            finally
            {
                TryDeleteDirectory(rootDirectory);
            }
        }
    }
}

