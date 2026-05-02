namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    internal sealed class DockerInteropEnvironmentProbe
    {
        private DockerInteropEnvironmentProbe(bool isAvailable, string? dockerServerVersion, string skipReason)
        {
            IsAvailable = isAvailable;
            DockerServerVersion = dockerServerVersion;
            SkipReason = skipReason;
        }

        public static DockerInteropEnvironmentProbe Current { get; } = Create();

        public string? DockerServerVersion { get; }

        public bool IsAvailable { get; }

        public string SkipReason { get; }

        private static DockerInteropEnvironmentProbe Create()
        {
            try
            {
                DockerCommandResult result = DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "info",
                        "--format",
                        "{{.ServerVersion}}|{{.OSType}}",
                    },
                    CancellationToken.None,
                    timeout: TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

                string raw = result.StandardOutput.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return new DockerInteropEnvironmentProbe(
                        isAvailable: false,
                        dockerServerVersion: null,
                        skipReason: "The docker daemon responded, but no server version was reported.");
                }

                int separatorIndex = raw.IndexOf('|');
                string serverVersion = separatorIndex >= 0 ? raw.Substring(0, separatorIndex).Trim() : raw;
                string osType = separatorIndex >= 0 ? raw.Substring(separatorIndex + 1).Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(serverVersion))
                {
                    return new DockerInteropEnvironmentProbe(
                        isAvailable: false,
                        dockerServerVersion: null,
                        skipReason: "The docker daemon responded, but no server version was reported.");
                }

                // The OpenNFS interop images are Linux containers (alpine, ubuntu). On a Windows
                // runner Docker can be running in Windows-container mode, which would fail every
                // image build with "no matching manifest for windows/amd64". Treat that as "not
                // available" rather than letting the Touchstone before-suite hook crash the run.
                if (!string.IsNullOrWhiteSpace(osType)
                    && !string.Equals(osType, "linux", StringComparison.OrdinalIgnoreCase))
                {
                    return new DockerInteropEnvironmentProbe(
                        isAvailable: false,
                        dockerServerVersion: serverVersion,
                        skipReason: "Docker is running in '" + osType + "' container mode, not 'linux'. The OpenNFS interop images are Linux containers; switch Docker to Linux containers to enable interop tests.");
                }

                return new DockerInteropEnvironmentProbe(
                    isAvailable: true,
                    dockerServerVersion: serverVersion,
                    skipReason: string.Empty);
            }
            catch (Exception exception)
            {
                return new DockerInteropEnvironmentProbe(
                    isAvailable: false,
                    dockerServerVersion: null,
                    skipReason: "Docker is unavailable to the OpenNFS interop harness: " + exception.Message);
            }
        }
    }
}
