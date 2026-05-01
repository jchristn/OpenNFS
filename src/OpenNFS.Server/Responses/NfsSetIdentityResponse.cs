namespace OpenNFS.Server.Responses
{
    using OpenNFS.Server.Identity;

    /// <summary>
    /// Response context containing the effective owner and owner-group identity strings after an identity update.
    /// </summary>
    public sealed class NfsSetIdentityResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetIdentityResponse"/> class.
        /// </summary>
        /// <param name="owner">Effective owner identity string after the update.</param>
        /// <param name="ownerGroup">Effective owner-group identity string after the update.</param>
        public NfsSetIdentityResponse(string owner, string ownerGroup)
            : this(new NfsIdentityMapping(owner, ownerGroup))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsSetIdentityResponse"/> class.
        /// </summary>
        /// <param name="identity">Effective owner and owner-group mapping after the update.</param>
        public NfsSetIdentityResponse(NfsIdentityMapping identity)
        {
            Identity = identity;
        }

        /// <summary>
        /// Gets the effective identity mapping after the update.
        /// </summary>
        public NfsIdentityMapping Identity { get; }

        /// <summary>
        /// Gets the effective owner identity string after the update.
        /// </summary>
        public string Owner
        {
            get
            {
                return Identity.Owner;
            }
        }

        /// <summary>
        /// Gets the effective owner-group identity string after the update.
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
