namespace OpenNFS.Protocol.V3.Mount
{
    using System;
    using System.Collections.Generic;

    internal sealed class MountV3MountedExportRegistry
    {
        private readonly List<MountV3MountedExport> mountedExports;
        private readonly object syncRoot;

        internal MountV3MountedExportRegistry()
        {
            mountedExports = new List<MountV3MountedExport>();
            syncRoot = new object();
        }

        internal void Add(string hostName, string exportPath)
        {
            lock (syncRoot)
            {
                for (int index = 0; index < mountedExports.Count; index++)
                {
                    MountV3MountedExport mountedExport = mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(mountedExport.ExportPath, exportPath, StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                mountedExports.Add(new MountV3MountedExport(hostName, exportPath));
            }
        }

        internal void Remove(string hostName, string exportPath)
        {
            lock (syncRoot)
            {
                for (int index = mountedExports.Count - 1; index >= 0; index--)
                {
                    MountV3MountedExport mountedExport = mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(mountedExport.ExportPath, exportPath, StringComparison.Ordinal))
                    {
                        mountedExports.RemoveAt(index);
                    }
                }
            }
        }

        internal void RemoveAll(string hostName)
        {
            lock (syncRoot)
            {
                for (int index = mountedExports.Count - 1; index >= 0; index--)
                {
                    MountV3MountedExport mountedExport = mountedExports[index];
                    if (string.Equals(mountedExport.HostName, hostName, StringComparison.OrdinalIgnoreCase))
                    {
                        mountedExports.RemoveAt(index);
                    }
                }
            }
        }

        internal List<MountV3MountedExport> CreateSnapshot()
        {
            lock (syncRoot)
            {
                return new List<MountV3MountedExport>(mountedExports);
            }
        }
    }
}
