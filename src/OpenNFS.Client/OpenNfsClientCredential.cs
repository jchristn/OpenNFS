namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Strongly-typed credential carrier passed to per-call client surfaces such as
    /// <see cref="OpenNfsClient.MountAsync(string, OpenNfsClientCredential, System.Threading.CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// The current shipping scope honors the credential's <see cref="Flavor"/> at validation time
    /// (the supplied flavor must match the <see cref="OpenNfsClientSettings.AuthenticationFlavor"/>
    /// configured on the builder) and uses the builder-configured AUTH_SYS identity values for the
    /// underlying RPC. Per-mount routing of the supplied AUTH_SYS values through the RPC pipeline is
    /// tracked as a release follow-up; the typed surface lands now so callers can express intent and
    /// future versions can plumb the override without breaking the public shape.
    /// </remarks>
    public sealed class OpenNfsClientCredential
    {
        private OpenNfsClientCredential(OpenNfsAuthenticationFlavor flavor, OpenNfsAuthSysCredentials? authSys)
        {
            Flavor = flavor;
            AuthSys = authSys;
        }

        /// <summary>
        /// Gets a reusable anonymous credential carrying <see cref="OpenNfsAuthenticationFlavor.AuthNone"/>.
        /// </summary>
        public static OpenNfsClientCredential Anonymous { get; } =
            new OpenNfsClientCredential(OpenNfsAuthenticationFlavor.AuthNone, null);

        /// <summary>
        /// Gets the authentication flavor carried by this credential.
        /// </summary>
        public OpenNfsAuthenticationFlavor Flavor { get; }

        /// <summary>
        /// Gets the AUTH_SYS identity values when <see cref="Flavor"/> is
        /// <see cref="OpenNfsAuthenticationFlavor.AuthSys"/>; otherwise null.
        /// </summary>
        public OpenNfsAuthSysCredentials? AuthSys { get; }

        /// <summary>
        /// Wraps the supplied AUTH_SYS identity values in a typed credential carrying
        /// <see cref="OpenNfsAuthenticationFlavor.AuthSys"/>.
        /// </summary>
        /// <param name="credentials">The AUTH_SYS identity values.</param>
        /// <returns>The typed credential.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is null.</exception>
        public static OpenNfsClientCredential FromAuthSys(OpenNfsAuthSysCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            return new OpenNfsClientCredential(OpenNfsAuthenticationFlavor.AuthSys, credentials);
        }
    }
}
