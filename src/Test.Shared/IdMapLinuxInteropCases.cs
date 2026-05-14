namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    internal static class IdMapLinuxInteropCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases(DockerInteropEnvironmentProbe probe)
        {
            return new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "IdMapSuites",
                    caseId: "LinuxMountedOwnerMappingRoundTrip",
                    displayName: "A Linux NFSv4.0 kernel client maps owner strings and round-trips chown against the sample server",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteLinuxMountedOwnerMappingRoundTripAsync),

                new TestCaseDescriptor(
                    suiteId: "IdMapSuites",
                    caseId: "LinuxMountedOwnerMappingRequiresLocalPrincipals",
                    displayName: "A Linux NFSv4.0 kernel client does not surface friendly owner names when the local mapping principals are missing",
                    tags: new List<string> { TestCategories.Interop, TestCategories.Privileged, TestCategories.Automated },
                    skip: !probe.IsAvailable,
                    skipReason: probe.SkipReason,
                    executeAsync: ExecuteLinuxMountedOwnerMappingRequiresLocalPrincipalsAsync),
            };
        }

        private static async Task ExecuteLinuxMountedOwnerMappingRoundTripAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.IdMapLinuxPositive", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    owner: "alice@example.test",
                    ownerGroup: "authors@example.test",
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                    CreateLinuxV40OwnerMappingCommand(
                        sampleServer.Nfs40Port,
                        domain: "example.test",
                        createLocalPrincipals: true,
                        includeChownRoundTrip: true),
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel NFSv4.0 client failed to mount or exercise owner mapping against the sample server."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (!combinedOutput.Contains("BEFORE=alice:authors", StringComparison.Ordinal)
                    || !combinedOutput.Contains("AFTER=bob:writers", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFSv4.0 client to map owner strings to local principals and round-trip chown values. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                await using OpenNfsClient client = new OpenNfsClientBuilder()
                    .WithServer("127.0.0.1", sampleServer.Nfs40Port)
                    .WithIdentityPolicy(new OpenNfsLinuxStyleIdentityPolicy())
                    .Build();
                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                byte[] nestedHandle = await ResolveSampleNestedHandleV40Async(client, cancellationToken).ConfigureAwait(false);
                OpenNfsV40GetIdentityResult identityResult = await client.Identity.GetOwnerAndGroupV40Async(
                    nestedHandle,
                    cancellationToken).ConfigureAwait(false);

                if (!identityResult.IsSuccess
                    || identityResult.Identity is null
                    || string.IsNullOrWhiteSpace(identityResult.Identity.ServerOwner)
                    || string.IsNullOrWhiteSpace(identityResult.Identity.ServerOwnerGroup)
                    || string.Equals(identityResult.Identity.ServerOwner, "alice@example.test", StringComparison.Ordinal)
                    || string.Equals(identityResult.Identity.ServerOwnerGroup, "authors@example.test", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the sample server to retain a changed Linux-mounted owner/group update through the public identity surface, but observed status '"
                        + identityResult.Status
                        + "', server owner '"
                        + identityResult.Identity?.ServerOwner
                        + "', server group '"
                        + identityResult.Identity?.ServerOwnerGroup
                        + "', client owner '"
                        + identityResult.Identity?.ClientOwner
                        + "', and client group '"
                        + identityResult.Identity?.ClientOwnerGroup
                        + "'.");
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(rootDirectory);
            }
        }

        private static async Task ExecuteLinuxMountedOwnerMappingRequiresLocalPrincipalsAsync(CancellationToken cancellationToken)
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "OpenNFS.IdMapLinuxNegative", Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(rootDirectory, "config");
            string configPath = Path.Combine(configDirectory, "sample-config.json");

            try
            {
                Directory.CreateDirectory(configDirectory);
                await WriteSampleConfigurationAsync(
                    configPath,
                    owner: "alice@example.test",
                    ownerGroup: "authors@example.test",
                    cancellationToken).ConfigureAwait(false);

                await using SampleOpenNfsServerProcess sampleServer =
                    await SampleOpenNfsServerProcess.StartAsync(configPath, cancellationToken).ConfigureAwait(false);

                DockerCommandResult result = await DockerLinuxNfsClient.RunCommandAsync(
                    CreateLinuxV40OwnerMappingCommand(
                        sampleServer.Nfs40Port,
                        domain: "example.test",
                        createLocalPrincipals: false,
                        includeChownRoundTrip: false),
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "The Linux kernel NFSv4.0 client failed before it could demonstrate the missing-principal owner-mapping path."
                        + Environment.NewLine
                        + "stdout:"
                        + Environment.NewLine
                        + result.StandardOutput
                        + Environment.NewLine
                        + "stderr:"
                        + Environment.NewLine
                        + result.StandardError
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }

                string combinedOutput = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
                if (!combinedOutput.Contains("BEFORE=", StringComparison.Ordinal)
                    || combinedOutput.Contains("BEFORE=alice:authors", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Expected the Linux kernel NFSv4.0 client not to surface friendly owner/group names when the local mapping principals are missing. Output was:"
                        + Environment.NewLine
                        + combinedOutput
                        + Environment.NewLine
                        + "sample:"
                        + Environment.NewLine
                        + sampleServer.GetCombinedOutput());
                }
            }
            finally
            {
                DeleteDirectoryIfPresent(rootDirectory);
            }
        }

        private static string CreateLinuxV40OwnerMappingCommand(
            int nfs40Port,
            string domain,
            bool createLocalPrincipals,
            bool includeChownRoundTrip)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("set -eu; ");
            builder.Append("mkdir -p /var/lib/nfs/rpc_pipefs /mnt/opennfs; ");
            builder.Append("mount -t rpc_pipefs sunrpc /var/lib/nfs/rpc_pipefs >/dev/null 2>&1 || true; ");
            builder.Append("cat >/etc/idmapd.conf <<'EOF'\n");
            builder.Append("[General]\n");
            builder.Append("Domain = ");
            builder.Append(domain);
            builder.Append("\nEOF\n");

            if (createLocalPrincipals)
            {
                builder.Append("addgroup -S authors >/dev/null 2>&1 || true; ");
                builder.Append("adduser -D -G authors alice >/dev/null 2>&1 || true; ");
                builder.Append("addgroup -S writers >/dev/null 2>&1 || true; ");
                builder.Append("adduser -D -G writers bob >/dev/null 2>&1 || true; ");
            }

            builder.Append("rpc.idmapd >/dev/null 2>&1 || true; ");
            builder.Append("sleep 1; ");
            builder.Append("mount -t nfs4 -o vers=4.0,minorversion=0,port=");
            builder.Append(nfs40Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(",soft,timeo=10,retrans=1 host.docker.internal:/ /mnt/opennfs; ");
            builder.Append("stat -c 'BEFORE=%U:%G' /mnt/opennfs/docs/nested.txt; ");

            if (includeChownRoundTrip)
            {
                builder.Append("chown bob:writers /mnt/opennfs/docs/nested.txt; ");
                builder.Append("sync; ");
                builder.Append("for attempt in 1 2 3 4 5; do ");
                builder.Append("if stat -c 'AFTER=%U:%G' /mnt/opennfs/docs/nested.txt; then break; fi; ");
                builder.Append("if [ \"$attempt\" = \"5\" ]; then exit 1; fi; ");
                builder.Append("sleep 1; ");
                builder.Append("done; ");
            }

            builder.Append("umount /mnt/opennfs; ");
            builder.Append("umount /var/lib/nfs/rpc_pipefs >/dev/null 2>&1 || true");
            return builder.ToString();
        }

        private static async Task<byte[]> ResolveSampleNestedHandleV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootLookup = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
            if (!rootLookup.IsSuccess || rootLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to return a usable NFSv4.0 root filehandle.");
            }

            OpenNfsV40LookupResult docsLookup = await client.Directories.LookupV40Async(
                rootLookup.ObjectFileHandle.ToArray(),
                "docs",
                cancellationToken).ConfigureAwait(false);
            if (!docsLookup.IsSuccess || docsLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to resolve the 'docs' directory over NFSv4.0.");
            }

            OpenNfsV40LookupResult nestedLookup = await client.Directories.LookupV40Async(
                docsLookup.ObjectFileHandle.ToArray(),
                "nested.txt",
                cancellationToken).ConfigureAwait(false);
            if (!nestedLookup.IsSuccess || nestedLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException("Expected the sample server to resolve 'docs/nested.txt' over NFSv4.0.");
            }

            return nestedLookup.ObjectFileHandle.ToArray();
        }

        private static async Task WriteSampleConfigurationAsync(
            string configPath,
            string owner,
            string ownerGroup,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(owner);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGroup);

            string? configDirectory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            string sourceRoot = Path.Combine(configDirectory ?? string.Empty, "content", "export", "docs");
            Directory.CreateDirectory(sourceRoot);
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "nested.txt"),
                "hello-idmap",
                cancellationToken).ConfigureAwait(false);

            string configurationJson = JsonSerializer.Serialize(
                new
                {
                    serverName = "IdMap Sample",
                    exportPath = "/exports/sample",
                    sourcePath = "content/export",
                    owner = owner,
                    ownerGroup = ownerGroup,
                    mappingPath = "state/handles.json",
                    listenerAddress = "0.0.0.0",
                    mountPort = 20048,
                    nfsPort = 2049,
                    nfs40Port = 3049,
                    denyMounts = false,
                });

            await File.WriteAllTextAsync(configPath, configurationJson, cancellationToken).ConfigureAwait(false);
        }

        private static void DeleteDirectoryIfPresent(string directoryPath)
        {
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
