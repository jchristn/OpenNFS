namespace OpenNFS.Server
{
    using System;

    /// <summary>
    /// Stable host-defined identity that can survive path changes for a filehandle target.
    /// </summary>
    public sealed class NfsFileHandleIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsFileHandleIdentity"/> class.
        /// </summary>
        /// <param name="scheme">Identity scheme name, such as an inode, object ID, or provider-specific key family.</param>
        /// <param name="value">Stable identity value within the chosen scheme.</param>
        /// <exception cref="ArgumentException">Thrown when a text input is empty or whitespace.</exception>
        public NfsFileHandleIdentity(string scheme, string value)
        {
            if (string.IsNullOrWhiteSpace(scheme))
            {
                throw new ArgumentException("The filehandle identity scheme must contain a non-empty value.", nameof(scheme));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The filehandle identity value must contain a non-empty value.", nameof(value));
            }

            Scheme = scheme;
            Value = value;
        }

        /// <summary>
        /// Gets the host-defined identity scheme name.
        /// </summary>
        public string Scheme { get; }

        /// <summary>
        /// Gets the stable identity value within the chosen scheme.
        /// </summary>
        public string Value { get; }
    }
}
