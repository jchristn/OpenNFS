namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Host-visible target that a server-side filehandle refers to.
    /// </summary>
    public sealed class NfsFileHandleTarget
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsFileHandleTarget"/> class.
        /// </summary>
        /// <param name="exportPath">
        /// Client-visible export path.
        /// The value must begin with <c>/</c>.
        /// </param>
        /// <param name="sourcePath">Host-local source path for the target.</param>
        /// <param name="stableIdentity">Optional stable identity that can outlive path changes.</param>
        /// <exception cref="ArgumentException">Thrown when a path value is empty, whitespace, or structurally invalid.</exception>
        public NfsFileHandleTarget(string exportPath, string sourcePath, NfsFileHandleIdentity? stableIdentity = null)
        {
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                throw new ArgumentException("The filehandle export path must contain a non-empty path value.", nameof(exportPath));
            }

            if (!exportPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("The filehandle export path must start with '/'.", nameof(exportPath));
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("The filehandle source path must contain a non-empty filesystem path.", nameof(sourcePath));
            }

            ExportPath = exportPath;
            SourcePath = sourcePath;
            StableIdentity = stableIdentity;
        }

        /// <summary>
        /// Gets the client-visible export path.
        /// </summary>
        public string ExportPath { get; }

        /// <summary>
        /// Gets the host-local source path for the target.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the optional stable identity for the target.
        /// </summary>
        public NfsFileHandleIdentity? StableIdentity { get; }
    }
}
