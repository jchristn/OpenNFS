namespace OpenNFS.Server.Identity
{
    using System;

    /// <summary>
    /// Immutable owner and owner-group identity mapping for a host-local path.
    /// </summary>
    public sealed class NfsIdentityMapping
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsIdentityMapping"/> class.
        /// </summary>
        /// <param name="owner">Mapped owner identity string.</param>
        /// <param name="ownerGroup">Mapped owner-group identity string.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="owner"/> or <paramref name="ownerGroup"/> is empty or whitespace.
        /// </exception>
        public NfsIdentityMapping(string owner, string ownerGroup)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("The identity mapping must contain a non-empty owner string.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(ownerGroup))
            {
                throw new ArgumentException("The identity mapping must contain a non-empty owner-group string.", nameof(ownerGroup));
            }

            Owner = owner;
            OwnerGroup = ownerGroup;
        }

        /// <summary>
        /// Gets the mapped owner identity string.
        /// </summary>
        public string Owner { get; }

        /// <summary>
        /// Gets the mapped owner-group identity string.
        /// </summary>
        public string OwnerGroup { get; }
    }
}
