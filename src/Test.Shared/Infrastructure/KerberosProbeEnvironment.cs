namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Detects whether the OpenNFS Kerberos test fixture is reachable from this test process.
    /// </summary>
    /// <remarks>
    /// The Kerberos suite cases that drive the live KDC + provider end-to-end gate on:
    /// (1) Docker being available, and (2) the OpenNFS test KDC container being healthy. When
    /// either is missing, the cases skip with a clear reason rather than failing.
    /// </remarks>
    internal sealed class KerberosProbeEnvironment
    {
        private KerberosProbeEnvironment(bool isAvailable, string skipReason)
        {
            IsAvailable = isAvailable;
            SkipReason = skipReason;
        }

        internal static KerberosProbeEnvironment Current { get; } = Create();

        internal bool IsAvailable { get; }

        internal string SkipReason { get; }

        private static KerberosProbeEnvironment Create()
        {
            DockerInteropEnvironmentProbe dockerProbe = DockerInteropEnvironmentProbe.Current;
            if (!dockerProbe.IsAvailable)
            {
                return new KerberosProbeEnvironment(
                    isAvailable: false,
                    skipReason: dockerProbe.SkipReason);
            }

            try
            {
                DockerCommandResult result = DockerCli.RunAsync(
                    new List<string>
                    {
                        "ps",
                        "--filter",
                        "name=opennfs-kdc",
                        "--filter",
                        "health=healthy",
                        "--format",
                        "{{.Names}}",
                    },
                    CancellationToken.None,
                    timeout: TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

                if (result.ExitCode != 0 || !result.StandardOutput.Contains("opennfs-kdc", StringComparison.Ordinal))
                {
                    return new KerberosProbeEnvironment(
                        isAvailable: false,
                        skipReason: "OpenNFS test KDC (opennfs-kdc) is not running. Start with 'docker compose up -d' in scripts/interop/kerberos/.");
                }

                return new KerberosProbeEnvironment(isAvailable: true, skipReason: string.Empty);
            }
            catch (Exception failure)
            {
                return new KerberosProbeEnvironment(
                    isAvailable: false,
                    skipReason: "Kerberos probe environment probe failed: " + failure.Message);
            }
        }
    }
}
