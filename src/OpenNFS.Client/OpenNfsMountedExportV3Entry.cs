namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Decoded entry from a MOUNT v3 <c>DUMP</c> reply.
    /// </summary>
    public sealed class OpenNfsMountedExportV3Entry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsMountedExportV3Entry"/> class.
        /// </summary>
        /// <param name="hostName">Decoded remote host name.</param>
        /// <param name="exportPath">Decoded mounted export path.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="hostName"/> or <paramref name="exportPath"/> is empty or whitespace.
        /// </exception>
        public OpenNfsMountedExportV3Entry(string hostName, string exportPath)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new ArgumentException("The mounted-export host name must contain a host name.", nameof(hostName));
            }

            if (string.IsNullOrWhiteSpace(exportPath))
            {
                throw new ArgumentException("The mounted-export path must contain a directory path.", nameof(exportPath));
            }

            HostName = hostName;
            ExportPath = exportPath;
        }

        /// <summary>
        /// Gets the decoded remote host name.
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// Gets the decoded mounted export path.
        /// </summary>
        public string ExportPath { get; }
    }
}
