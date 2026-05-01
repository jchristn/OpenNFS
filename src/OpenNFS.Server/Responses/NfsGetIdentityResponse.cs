namespace OpenNFS.Server.Responses
{
    using OpenNFS.Server.Identity;

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
        public NfsGetIdentityResponse(string owner, string ownerGroup)
            : this(new NfsIdentityMapping(owner, ownerGroup))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsGetIdentityResponse"/> class.
        /// </summary>
        /// <param name="identity">Mapped owner and owner-group identity strings.</param>
        public NfsGetIdentityResponse(NfsIdentityMapping identity)
        {
            Identity = identity;
        }

        /// <summary>
        /// Gets the mapped owner and owner-group identity strings.
        /// </summary>
        public NfsIdentityMapping Identity { get; }

        /// <summary>
        /// Gets the mapped owner identity string.
        /// </summary>
        public string Owner
        {
            get
            {
                return Identity.Owner;
            }
        }

        /// <summary>
        /// Gets the mapped group identity string.
        /// </summary>
        public string OwnerGroup
        {
            get
            {
                return Identity.OwnerGroup;
            }
        }
    }
}
