namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Client identity policy that normalizes common NFSv4 owner and owner-group strings into Linux-style local names.
    /// </summary>
    public sealed class OpenNfsLinuxStyleIdentityPolicy : IOpenNfsClientIdentityPolicy
    {
        /// <summary>
        /// Maps server-returned owner and owner-group strings into Linux-style local names.
        /// </summary>
        /// <param name="serverOwner">Server-returned owner string.</param>
        /// <param name="serverOwnerGroup">Server-returned owner-group string.</param>
        /// <returns>The mapped identity values.</returns>
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

            return new OpenNfsMappedIdentity(
                serverOwner,
                serverOwnerGroup,
                NormalizeLinuxName(serverOwner),
                NormalizeLinuxName(serverOwnerGroup));
        }

        private static string NormalizeLinuxName(string value)
        {
            int domainSeparatorIndex = value.IndexOf('@', StringComparison.Ordinal);
            if (domainSeparatorIndex > 0)
            {
                value = value.Substring(0, domainSeparatorIndex);
            }

            int windowsSeparatorIndex = value.LastIndexOf('\\');
            if (windowsSeparatorIndex >= 0 && windowsSeparatorIndex < value.Length - 1)
            {
                value = value.Substring(windowsSeparatorIndex + 1);
            }

            return string.IsNullOrWhiteSpace(value) ? "nobody" : value;
        }
    }
}
