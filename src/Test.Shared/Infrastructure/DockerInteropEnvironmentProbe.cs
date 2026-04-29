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
                        "{{.ServerVersion}}",
                    },
                    CancellationToken.None,
                    timeout: TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

                string serverVersion = result.StandardOutput.Trim();
                if (string.IsNullOrWhiteSpace(serverVersion))
                {
                    return new DockerInteropEnvironmentProbe(
                        isAvailable: false,
                        dockerServerVersion: null,
                        skipReason: "The docker daemon responded, but no server version was reported.");
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
