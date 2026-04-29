namespace OpenNFS.Server.Responses
{
    using System;

    /// <summary>
    /// Response context containing owner and group identity strings for a host-local path.
    /// </summary>
    public sealed class NfsGetIdentityResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetIdentityResponse"/> class.
        /// </summary>
        /// <param name="owner">Mapped owner identity string.</param>
        /// <param name="ownerGroup">Mapped group identity string.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="owner"/> or <paramref name="ownerGroup"/> is empty or whitespace.
        /// </exception>
        public NfsGetIdentityResponse(string owner, string ownerGroup)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("The identity-mapping response must contain a non-empty owner string.", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(ownerGroup))
            {
                throw new ArgumentException("The identity-mapping response must contain a non-empty owner-group string.", nameof(ownerGroup));
            }

            Owner = owner;
            OwnerGroup = ownerGroup;
        }

        /// <summary>
        /// Gets the mapped owner identity string.
        /// </summary>
        public string Owner { get; }

        /// <summary>
        /// Gets the mapped group identity string.
        /// </summary>
        public string OwnerGroup { get; }
    }
}
