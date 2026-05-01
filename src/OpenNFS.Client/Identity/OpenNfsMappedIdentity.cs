namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Server-returned and client-mapped owner and owner-group identity values.
    /// </summary>
    public sealed class OpenNfsMappedIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsMappedIdentity"/> class.
        /// </summary>
        /// <param name="serverOwner">Server-returned owner string.</param>
        /// <param name="serverOwnerGroup">Server-returned owner-group string.</param>
        /// <param name="clientOwner">Client-mapped owner string.</param>
        /// <param name="clientOwnerGroup">Client-mapped owner-group string.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when any identity value is empty or whitespace.
        /// </exception>
        public OpenNfsMappedIdentity(
            string serverOwner,
            string serverOwnerGroup,
            string clientOwner,
            string clientOwnerGroup)
        {
            ServerOwner = RequireText(serverOwner, nameof(serverOwner));
            ServerOwnerGroup = RequireText(serverOwnerGroup, nameof(serverOwnerGroup));
            ClientOwner = RequireText(clientOwner, nameof(clientOwner));
            ClientOwnerGroup = RequireText(clientOwnerGroup, nameof(clientOwnerGroup));
        }

        /// <summary>
        /// Gets the server-returned owner string.
        /// </summary>
        public string ServerOwner { get; }

        /// <summary>
        /// Gets the server-returned owner-group string.
        /// </summary>
        public string ServerOwnerGroup { get; }

        /// <summary>
        /// Gets the client-mapped owner string.
        /// </summary>
        public string ClientOwner { get; }

        /// <summary>
        /// Gets the client-mapped owner-group string.
        /// </summary>
        public string ClientOwnerGroup { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identity values must contain non-empty text.", parameterName);
            }

            return value;
        }
    }
}
