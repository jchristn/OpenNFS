namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using OpenNFS.Server;
    using OpenNFS.Server.FileSystems;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Server capability discovery and authorization surface cases.
    /// </summary>
    internal static class ServerSurfaceCapabilityCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ServerSurfaceSuites",
                    caseId: "MountAuthorizationDefaultsToAllowAllAndPreservesExplicitService",
                    displayName: "Server mount authorization defaults to allow-all and preserves explicit services",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));
                        StaticMountAuthorization mountAuthorization = new StaticMountAuthorization(
                            new Dictionary<string, NfsMountAccessDisposition>(StringComparer.Ordinal));

                        OpenNfsServer defaultServer = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .Build();

                        OpenNfsServer configuredServer = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .UseMountAuthorization(mountAuthorization)
                            .Build();

                        if (defaultServer.Settings.MountAuthorization is null
                            || defaultServer.Settings.MountAuthorization is StaticMountAuthorization)
                        {
                            throw new InvalidOperationException("Expected the public server surface to provide a non-null default mount-authorization contract.");
                        }

                        if (!ReferenceEquals(configuredServer.Settings.MountAuthorization, mountAuthorization))
                        {
                            throw new InvalidOperationException("Expected an explicitly configured mount-authorization contract to be preserved by immutable server settings.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "ServerSurfaceSuites",
                    caseId: "OptionalCapabilitiesDefaultToAbsent",
                    displayName: "Unconfigured optional capabilities stay absent",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        DictionaryNfsFileSystem fileSystem = new DictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));

                        OpenNfsServer server = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .Build();

                        if (server.Capabilities.AdvertisedCapabilities.Count != 0)
                        {
                            throw new InvalidOperationException("Expected a server with no optional capability registrations to advertise no optional capabilities.");
                        }

                        foreach (NfsCapabilityKind capabilityKind in (NfsCapabilityKind[])Enum.GetValues(typeof(NfsCapabilityKind)))
                        {
                            if (server.Capabilities.Supports(capabilityKind))
                            {
                                throw new InvalidOperationException("Expected capability '" + capabilityKind + "' to remain absent when the host did not register it.");
                            }
                        }

                        if (server.Capabilities.Locking is not null
                            || server.Capabilities.Acls is not null
                            || server.Capabilities.Delegations is not null
                            || server.Capabilities.CopyClone is not null
                            || server.Capabilities.Sparse is not null
                            || server.Capabilities.IdMapper is not null)
                        {
                            throw new InvalidOperationException("Expected the optional capability catalog to expose null service references when no capabilities were configured.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "ServerSurfaceSuites",
                    caseId: "OptionalCapabilitiesAreDiscoveredAndAdvertised",
                    displayName: "Optional capabilities are auto-discovered and explicitly advertised",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                            new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase));
                        TestNfsAcls acls = new TestNfsAcls();
                        TestNfsCopyClone copyClone = new TestNfsCopyClone();
                        TestNfsIdMapper idMapper = new TestNfsIdMapper();

                        OpenNfsServer server = new OpenNfsServerBuilder()
                            .UseFileSystem(fileSystem)
                            .UseAcls(acls)
                            .UseCopyClone(copyClone)
                            .UseIdMapper(idMapper)
                            .Build();

                        if (!ReferenceEquals(server.Capabilities, server.Settings.Capabilities))
                        {
                            throw new InvalidOperationException("Expected the server capability catalog to be surfaced directly from immutable server settings.");
                        }

                        if (!ReferenceEquals(server.Capabilities.Locking, fileSystem)
                            || !ReferenceEquals(server.Capabilities.Delegations, fileSystem)
                            || !ReferenceEquals(server.Capabilities.Sparse, fileSystem))
                        {
                            throw new InvalidOperationException("Expected optional capabilities implemented by the configured file system to be auto-discovered.");
                        }

                        if (!ReferenceEquals(server.Capabilities.Acls, acls)
                            || !ReferenceEquals(server.Capabilities.CopyClone, copyClone)
                            || !ReferenceEquals(server.Capabilities.IdMapper, idMapper))
                        {
                            throw new InvalidOperationException("Expected explicitly registered optional capability services to be preserved.");
                        }

                        NfsCapabilityKind[] expectedCapabilities = new[]
                        {
                            NfsCapabilityKind.Locking,
                            NfsCapabilityKind.Acls,
                            NfsCapabilityKind.Delegations,
                            NfsCapabilityKind.CopyClone,
                            NfsCapabilityKind.SparseFiles,
                            NfsCapabilityKind.IdMapping,
                        };

                        if (server.Capabilities.AdvertisedCapabilities.Count != expectedCapabilities.Length)
                        {
                            throw new InvalidOperationException("Expected every configured optional capability to appear in the advertised capability catalog.");
                        }

                        for (int index = 0; index < expectedCapabilities.Length; index++)
                        {
                            NfsCapabilityKind expectedCapability = expectedCapabilities[index];

                            if (server.Capabilities.AdvertisedCapabilities[index] != expectedCapability)
                            {
                                throw new InvalidOperationException("Expected advertised capability order to remain stable for later protocol advertisement decisions.");
                            }

                            if (!server.Capabilities.Supports(expectedCapability))
                            {
                                throw new InvalidOperationException("Expected configured capability '" + expectedCapability + "' to be discoverable.");
                            }
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
