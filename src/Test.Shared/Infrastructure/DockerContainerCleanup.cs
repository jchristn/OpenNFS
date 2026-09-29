namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Removes a Docker container and verifies that it is gone. A single <c>docker rm --force</c> can kill a privileged
    /// knfsd container without removing it while the kernel NFS server is still tearing down, so removal is retried until
    /// <c>docker inspect</c> no longer finds the container (bounded to about two minutes). Never throws.
    /// </summary>
    internal static class DockerContainerCleanup
    {
        private static readonly TimeSpan RemovalDeadline = TimeSpan.FromMinutes(2);

        internal static async Task RemoveAsync(string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                return;
            }

            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(RemovalDeadline);
            while (true)
            {
                try
                {
                    _ = await DockerCli.RunAsync(
                        new List<string> { "rm", "--force", "--volumes", containerName },
                        CancellationToken.None,
                        timeout: TimeSpan.FromSeconds(60)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                try
                {
                    DockerCommandResult inspect = await DockerCli.RunAsync(
                        new List<string> { "inspect", "--format", "{{ .Id }}", containerName },
                        CancellationToken.None,
                        timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    if (inspect.ExitCode != 0)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
        }
    }
}
