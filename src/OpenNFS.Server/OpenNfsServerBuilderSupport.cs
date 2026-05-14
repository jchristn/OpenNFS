namespace OpenNFS.Server
{
    using System;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;
    using OpenNFS.Server.FileHandles;
    using OpenNFS.Server.Internal;

    internal static class OpenNfsServerBuilderSupport
    {
        internal static void ValidateServerName(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
            {
                throw new ArgumentException("The server name must contain a non-empty value.", nameof(serverName));
            }
        }

        internal static void ValidateListenerAddress(string listenerAddress)
        {
            if (string.IsNullOrWhiteSpace(listenerAddress))
            {
                throw new ArgumentException("The listener address must contain a non-empty value.", nameof(listenerAddress));
            }
        }

        internal static void ValidateListenerPort(int listenerPort)
        {
            if (listenerPort < 1 || listenerPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(listenerPort), listenerPort, "The listener port must be between 1 and 65535.");
            }
        }

        internal static void ValidateMaximumConnections(int maximumConnections)
        {
            if (maximumConnections < 1 || maximumConnections > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumConnections), maximumConnections, "The maximum connection count must be between 1 and 65535.");
            }
        }

        internal static OpenNfsServerSettings BuildSettings(OpenNfsServerBuilderState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            if (state.FileSystem is null)
            {
                throw new InvalidOperationException("A server file system must be configured via UseFileSystem before building server settings or a server.");
            }

            return new OpenNfsServerSettings(
                fileSystem: state.FileSystem,
                serverName: state.ServerName,
                listenerAddress: state.ListenerAddress,
                listenerPort: state.ListenerPort,
                enableUdpForNfsV3: state.EnableUdpForNfsV3,
                maximumConnections: state.MaximumConnections,
                exportProvider: ResolveExportProvider(state),
                mountAuthorization: state.MountAuthorization ?? AllowAllNfsMountAuthorization.Instance,
                fileHandleProvider: state.FileHandleProvider ?? IntrinsicHandleProvider.Default,
                locking: ResolveCapability(state.Locking, state.FileSystem),
                acls: ResolveCapability(state.Acls, state.FileSystem),
                delegations: ResolveCapability(state.Delegations, state.FileSystem),
                copyClone: ResolveCapability(state.CopyClone, state.FileSystem),
                sparse: ResolveCapability(state.Sparse, state.FileSystem),
                idMapper: ResolveCapability(state.IdMapper, state.FileSystem),
                rpcSecGssMechanism: state.RpcSecGssMechanism);
        }

        private static INfsExportProvider ResolveExportProvider(OpenNfsServerBuilderState state)
        {
            if (state.ConfiguredExports.Count < 1)
            {
                return state.ExportProvider ?? EmptyNfsExportProvider.Instance;
            }

            INfsExportProvider staticProvider = new StaticNfsExportProvider(state.ConfiguredExports);
            if (state.ExportProvider is null)
            {
                return staticProvider;
            }

            return new CompositeNfsExportProvider(new INfsExportProvider[]
            {
                staticProvider,
                state.ExportProvider,
            });
        }

        private static TCapability? ResolveCapability<TCapability>(
            TCapability? explicitCapability,
            INfsFileSystem fileSystem)
            where TCapability : class
        {
            return explicitCapability ?? fileSystem as TCapability;
        }
    }
}
