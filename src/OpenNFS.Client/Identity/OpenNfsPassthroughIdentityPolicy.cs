namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Client identity policy that preserves server-returned owner and owner-group strings verbatim.
    /// </summary>
    public sealed class OpenNfsPassthroughIdentityPolicy : IOpenNfsClientIdentityPolicy
    {
        private static readonly OpenNfsPassthroughIdentityPolicy _default = new OpenNfsPassthroughIdentityPolicy();

        /// <summary>
        /// Gets the shared passthrough identity policy instance.
        /// </summary>
        public static OpenNfsPassthroughIdentityPolicy Default
        {
            get
            {
                return _default;
            }
        }

        /// <summary>
        /// Maps server-returned owner and owner-group strings without modification.
        /// </summary>
        /// <param name="serverOwner">Server-returned owner string.</param>
        /// <param name="serverOwnerGroup">Server-returned owner-group string.</param>
        /// <returns>The unchanged owner and owner-group strings.</returns>
        public OpenNfsMappedIdentity Map(string serverOwner, string serverOwnerGroup)
        {
            if (string.IsNullOrWhiteSpace(serverOwner))
            {
                throw new ArgumentException("The server owner string must contain non-empty text.", nameof(serverOwner));
            }

            if (string.IsNullOrWhiteSpace(serverOwnerGroup))
            {
                throw new ArgumentException("The server owner-group string must contain non-empty text.", nameof(serverOwnerGroup));
            }

            return new OpenNfsMappedIdentity(serverOwner, serverOwnerGroup, serverOwner, serverOwnerGroup);
        }
    }
}
