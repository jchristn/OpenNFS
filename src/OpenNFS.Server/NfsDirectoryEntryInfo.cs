namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Describes a single child entry discovered beneath a host-local directory path.
    /// </summary>
    public sealed class NfsDirectoryEntryInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsDirectoryEntryInfo"/> class.
        /// </summary>
        /// <param name="name">Single entry name beneath the parent directory.</param>
        /// <param name="pathInfo">Resolved information about the child entry path.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pathInfo"/> is null.</exception>
        public NfsDirectoryEntryInfo(string name, NfsPathInfo pathInfo)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The directory entry name must contain a non-empty child name.", nameof(name));
            }

            ArgumentNullException.ThrowIfNull(pathInfo);
            Name = name;
            PathInfo = pathInfo;
        }

        /// <summary>
        /// Gets the single entry name beneath the parent directory.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the resolved information about the child entry path.
        /// </summary>
        public NfsPathInfo PathInfo { get; }
    }
}
