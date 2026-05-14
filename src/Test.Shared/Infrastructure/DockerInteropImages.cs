namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class DockerInteropImages
    {
        private static readonly SemaphoreSlim BuildGate = new SemaphoreSlim(1, 1);
        private static bool _built;

        public const string LinuxNfsClientImage = "opennfs-test/linux-nfs-client:local";
        public const string LinuxUserspaceNfsClientImage = "opennfs-test/linux-nfs-client-libnfs:local";
        public const string LinuxNfsServerImage = "opennfs-test/linux-nfs-server-unfs3:local";
        public const string LinuxNfsV40ServerImage = "opennfs-test/linux-nfs-server-ganesha-v4:local";
        public const string LinuxKnfsdServerImage = "opennfs-test/linux-nfs-server-knfsd:local";

        public static async Task EnsureBuiltAsync(CancellationToken cancellationToken, bool forceRebuild = false)
        {
            if (_built && !forceRebuild)
            {
                return;
            }

            await BuildGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_built && !forceRebuild)
                {
                    return;
                }

                string repositoryRoot = RepositoryPaths.GetRepositoryRoot();
                string clientContextDirectory = Path.Combine(repositoryRoot, "scripts", "interop", "linux", "nfs-client");
                string userspaceClientContextDirectory = Path.Combine(repositoryRoot, "scripts", "interop", "linux", "nfs-client-libnfs");
                string serverContextDirectory = Path.Combine(repositoryRoot, "scripts", "interop", "linux", "nfs-server-unfs3");
                string v40ServerContextDirectory = Path.Combine(repositoryRoot, "scripts", "interop", "linux", "nfs-server-ganesha-v4");
                string knfsdServerContextDirectory = Path.Combine(repositoryRoot, "scripts", "interop", "linux", "nfs-server-knfsd");

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "build",
                        "--tag",
                        LinuxNfsClientImage,
                        clientContextDirectory,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "build",
                        "--tag",
                        LinuxUserspaceNfsClientImage,
                        userspaceClientContextDirectory,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "build",
                        "--tag",
                        LinuxNfsServerImage,
                        serverContextDirectory,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(5)).ConfigureAwait(false);

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "build",
                        "--no-cache",
                        "--tag",
                        LinuxNfsV40ServerImage,
                        v40ServerContextDirectory,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

                await DockerCli.RunCheckedAsync(
                    new List<string>
                    {
                        "build",
                        "--tag",
                        LinuxKnfsdServerImage,
                        knfsdServerContextDirectory,
                    },
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);

                _built = true;
            }
            finally
            {
                BuildGate.Release();
            }
        }
    }
}
