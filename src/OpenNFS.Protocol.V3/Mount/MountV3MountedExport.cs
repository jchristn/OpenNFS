namespace OpenNFS.Protocol.V3.Mount
{
    using System;

    internal sealed class MountV3MountedExport
    {
        internal MountV3MountedExport(string hostName, string exportPath)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new ArgumentException("The mount host name must contain a non-empty value.", nameof(hostName));
            }

            if (string.IsNullOrWhiteSpace(exportPath))
            {
                throw new ArgumentException("The mount export path must contain a non-empty value.", nameof(exportPath));
            }

            HostName = hostName;
            ExportPath = exportPath;
        }

        internal string HostName { get; }

        internal string ExportPath { get; }
    }
}
