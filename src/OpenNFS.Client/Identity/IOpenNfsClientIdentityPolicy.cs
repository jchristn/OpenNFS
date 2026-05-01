namespace OpenNFS.Client
{
    /// <summary>
    /// Pluggable client-side identity policy for translating NFSv4 owner and owner-group strings into local consumer-facing names.
    /// </summary>
    public interface IOpenNfsClientIdentityPolicy
    {
        /// <summary>
        /// Maps the server-returned owner and owner-group strings into local consumer-facing identity values.
        /// </summary>
        /// <param name="serverOwner">Server-returned owner string.</param>
        /// <param name="serverOwnerGroup">Server-returned owner-group string.</param>
        /// <returns>The mapped identity values.</returns>
        OpenNfsMappedIdentity Map(string serverOwner, string serverOwnerGroup);
    }
}
