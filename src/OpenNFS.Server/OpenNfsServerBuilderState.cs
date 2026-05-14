namespace OpenNFS.Server
{
    using System.Collections.Generic;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Server.Abstractions;
    using OpenNFS.Server.Abstractions.Capabilities;

    internal sealed class OpenNfsServerBuilderState
    {
        internal OpenNfsServerBuilderState()
        {
            ConfiguredExports = new List<OpenNfsExportDefinition>();
            ListenerAddress = "0.0.0.0";
            ListenerPort = 2049;
            MaximumConnections = 256;
            ServerName = "OpenNFS";
        }

        internal List<OpenNfsExportDefinition> ConfiguredExports { get; }

        internal INfsAcls? Acls { get; set; }

        internal INfsCopyClone? CopyClone { get; set; }

        internal INfsDelegations? Delegations { get; set; }

        internal INfsExportProvider? ExportProvider { get; set; }

        internal INfsMountAuthorization? MountAuthorization { get; set; }

        internal IFileHandleProvider? FileHandleProvider { get; set; }

        internal INfsFileSystem? FileSystem { get; set; }

        internal INfsIdMapper? IdMapper { get; set; }

        internal INfsLocking? Locking { get; set; }

        internal INfsSparse? Sparse { get; set; }

        internal IRpcSecGssMechanism? RpcSecGssMechanism { get; set; }

        internal bool EnableUdpForNfsV3 { get; set; }

        internal string ListenerAddress { get; set; }

        internal int ListenerPort { get; set; }

        internal int MaximumConnections { get; set; }

        internal string ServerName { get; set; }
    }
}
