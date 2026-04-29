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
