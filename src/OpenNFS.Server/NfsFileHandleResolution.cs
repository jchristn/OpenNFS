namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Result of resolving a server-side filehandle.
    /// </summary>
    public sealed class NfsFileHandleResolution
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsFileHandleResolution"/> class.
        /// </summary>
        /// <param name="found">True when the filehandle was resolved to a target.</param>
        /// <param name="target">Resolved target, or null when the filehandle was not found.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="found"/> is true and <paramref name="target"/> is null.</exception>
        public NfsFileHandleResolution(bool found, NfsFileHandleTarget? target = null)
        {
            if (found && target is null)
            {
                throw new ArgumentException("A resolved filehandle result must include a target.", nameof(target));
            }

            Found = found;
            Target = target;
        }

        /// <summary>
        /// Gets a value indicating whether the filehandle was resolved.
        /// </summary>
        public bool Found { get; }

        /// <summary>
        /// Gets the resolved target when <see cref="Found"/> is true; otherwise null.
        /// </summary>
        public NfsFileHandleTarget? Target { get; }
    }
}
