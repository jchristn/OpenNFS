namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class DockerLinuxNfsClient
    {
        public static async Task<DockerCommandResult> RunCommandAsync(string shellCommand, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(shellCommand);

            await DockerInteropImages.EnsureBuiltAsync(cancellationToken).ConfigureAwait(false);

            DockerCommandResult result = await RunCommandCoreAsync(shellCommand, cancellationToken).ConfigureAwait(false);
            if (NeedsImageRebuildRetry(result))
            {
                await DockerInteropImages.EnsureBuiltAsync(cancellationToken, forceRebuild: true).ConfigureAwait(false);
                result = await RunCommandCoreAsync(shellCommand, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task<DockerCommandResult> RunCommandCoreAsync(string shellCommand, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(shellCommand);

            string containerName = "opennfs-linux-client-" + Guid.NewGuid().ToString("N");

            try
            {
                return await DockerCli.RunAsync(
                    new List<string>
                    {
                        "run",
                        "--name",
                        containerName,
                        "--privileged",
                        "--add-host",
                        "host.docker.internal:host-gateway",
                        DockerInteropImages.LinuxNfsClientImage,
                        shellCommand,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            }
            finally
            {
                await RemoveContainerAsync(containerName).ConfigureAwait(false);
            }
        }

        private static bool NeedsImageRebuildRetry(DockerCommandResult result)
        {
            return result.ExitCode != 0
                && result.StandardError.Contains("pull access denied", StringComparison.OrdinalIgnoreCase)
                && result.StandardError.Contains(DockerInteropImages.LinuxNfsClientImage, StringComparison.Ordinal);
        }

        private static async Task RemoveContainerAsync(string containerName)
        {
            try
            {
                _ = await DockerCli.RunAsync(
                    new List<string>
                    {
                        "rm",
                        "--force",
                        containerName,
                    },
                    CancellationToken.None,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
