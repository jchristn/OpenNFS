namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Defines a server export mapping exposed by the initial OpenNFS server configuration surface.
    /// </summary>
    public sealed class OpenNfsExportDefinition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsExportDefinition"/> class.
        /// </summary>
        /// <param name="exportPath">
        /// Client-visible export path.
        /// The value must begin with <c>/</c>.
        /// </param>
        /// <param name="sourcePath">Host-local source path that backs the export.</param>
        /// <param name="readOnly">
        /// True to advertise the export as read-only.
        /// Default value: <c>false</c>.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when a path value is empty, whitespace, or structurally invalid.</exception>
        public OpenNfsExportDefinition(string exportPath, string sourcePath, bool readOnly = false)
        {
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                throw new ArgumentException("The export path must contain a non-empty path value.", nameof(exportPath));
            }

            if (!exportPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("The export path must start with '/'.", nameof(exportPath));
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The source path must contain a non-empty filesystem path.", nameof(sourcePath));
            }

            ExportPath = exportPath;
            SourcePath = sourcePath;
            ReadOnly = readOnly;
        }

        /// <summary>
        /// Gets the client-visible export path.
        /// </summary>
        public string ExportPath { get; }

        /// <summary>
        /// Gets the host-local filesystem path that backs the export.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets a value indicating whether the export is read-only.
        /// </summary>
        public bool ReadOnly { get; }
    }
}

