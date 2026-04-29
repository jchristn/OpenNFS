namespace OpenNFS.Server.Requests
{
    using System;
    using System.Threading;

    /// <summary>
    /// Request context for creating a symbolic link beneath a host-local directory path.
    /// </summary>
    public sealed class NfsCreateSymbolicLinkRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsCreateSymbolicLinkRequest"/> class.
        /// </summary>
        /// <param name="parentDirectorySourcePath">Host-local source path for the parent directory.</param>
        /// <param name="entryName">Single entry name to create beneath the parent directory.</param>
        /// <param name="targetPath">Target path string to store in the symbolic link.</param>
        /// <param name="failIfExists">True to fail instead of reusing an existing entry.</param>
        /// <param name="cancellationToken">Cancellation token for the symbolic-link-create operation.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="parentDirectorySourcePath"/>, <paramref name="entryName"/>, or <paramref name="targetPath"/> is empty or whitespace.
        /// </exception>
        public NfsCreateSymbolicLinkRequest(
            string parentDirectorySourcePath,
            string entryName,
            string targetPath,
            bool failIfExists,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(parentDirectorySourcePath))
            {
                throw new ArgumentException("The create-symbolic-link request must contain a non-empty parent directory source path.", nameof(parentDirectorySourcePath));
            }

            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("The create-symbolic-link request must contain a non-empty entry name.", nameof(entryName));
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("The create-symbolic-link request must contain a non-empty symbolic-link target path.", nameof(targetPath));
            }

            ParentDirectorySourcePath = parentDirectorySourcePath;
            EntryName = entryName;
            TargetPath = targetPath;
            FailIfExists = failIfExists;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets the host-local source path for the parent directory.
        /// </summary>
        public string ParentDirectorySourcePath { get; }

        /// <summary>
        /// Gets the single entry name to create beneath the parent directory.
        /// </summary>
        public string EntryName { get; }

        /// <summary>
        /// Gets the target path string to store in the symbolic link.
        /// </summary>
        public string TargetPath { get; }

        /// <summary>
        /// Gets a value indicating whether the create operation should fail instead of reusing an existing entry.
        /// </summary>
        public bool FailIfExists { get; }

        /// <summary>
        /// Gets the cancellation token for the symbolic-link-create operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}
