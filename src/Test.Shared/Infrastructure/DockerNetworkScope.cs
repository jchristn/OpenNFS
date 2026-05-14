namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DockerNetworkScope : IAsyncDisposable
    {
        private DockerNetworkScope(string name)
        {
            Name = name;
        }

        internal string Name { get; }

        internal static async Task<DockerNetworkScope> CreateAsync(CancellationToken cancellationToken)
        {
            string networkName = "opennfs-test-net-" + Guid.NewGuid().ToString("N");

            try
            {
                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "network",
                        "create",
                        networkName,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromSeconds(30)).ConfigureAwait(false);

                return new DockerNetworkScope(networkName);
            }
            catch
            {
                await RemoveAsync(networkName).ConfigureAwait(false);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await RemoveAsync(Name).ConfigureAwait(false);
        }

        private static async Task RemoveAsync(string networkName)
        {
            if (string.IsNullOrWhiteSpace(networkName))
            {
                return;
            }

            try
            {
                _ = await DockerCli.RunAsync(
                    new List<string>
                    {
                        "network",
                        "rm",
                        networkName,
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
