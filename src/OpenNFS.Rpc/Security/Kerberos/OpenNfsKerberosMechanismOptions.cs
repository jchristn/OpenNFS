namespace OpenNFS.Rpc.Security.Kerberos
{
    using System;
    using System.Net;
    using System.Net.Security;

    /// <summary>
    /// Configures the server-side <see cref="OpenNfsKerberosMechanism"/>.
    /// </summary>
    public sealed class OpenNfsKerberosMechanismOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsKerberosMechanismOptions"/> class.
        /// </summary>
        /// <param name="targetSpn">
        /// The target service principal name the server expects clients to authenticate to, in the
        /// canonical Kerberos form <c>service/host@REALM</c> (e.g. <c>nfs/sample.example.test@EXAMPLE.TEST</c>).
        /// </param>
        public OpenNfsKerberosMechanismOptions(string targetSpn)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetSpn);

            TargetSpn = targetSpn;
            RequiredProtectionLevel = ProtectionLevel.EncryptAndSign;
            IdleContextLifetime = TimeSpan.FromMinutes(60);
        }

        /// <summary>
        /// Gets the target service principal name the server expects clients to authenticate to.
        /// </summary>
        public string TargetSpn { get; }

        /// <summary>
        /// Gets or sets the optional server credential. When unset, the server uses the current
        /// process credentials, which on Linux .NET typically resolves a keytab via the
        /// <c>KRB5_KTNAME</c> environment variable and on Windows uses the process logon session.
        /// </summary>
        public NetworkCredential? ServerCredential { get; set; }

        /// <summary>
        /// Gets or sets the protection level required for established contexts. Defaults to
        /// <see cref="ProtectionLevel.EncryptAndSign"/> so the negotiated context supports both
        /// integrity (<c>krb5i</c>) and privacy (<c>krb5p</c>) services.
        /// </summary>
        public ProtectionLevel RequiredProtectionLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum idle lifetime of an in-progress or established context before
        /// the mechanism evicts it.
        /// </summary>
        public TimeSpan IdleContextLifetime { get; set; }
    }
}
