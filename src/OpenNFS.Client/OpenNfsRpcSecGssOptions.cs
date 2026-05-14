namespace OpenNFS.Client
{
    using System;

    /// <summary>
    /// Immutable RPCSEC_GSS client options for Kerberos-backed public-client execution.
    /// </summary>
    public sealed class OpenNfsRpcSecGssOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsRpcSecGssOptions"/> class.
        /// </summary>
        /// <param name="targetSpn">
        /// Kerberos service principal name in canonical form, for example
        /// <c>nfs/sample.example.test@EXAMPLE.TEST</c>.
        /// </param>
        /// <param name="targetName">
        /// Optional NegotiateAuthentication target name. When omitted, the client derives the
        /// host-based service name by stripping any realm suffix from <paramref name="targetSpn"/>.
        /// </param>
        /// <param name="service">
        /// Requested RPCSEC_GSS service level. The initial public-client runtime slice currently
        /// supports <see cref="OpenNfsRpcGssService.None"/> end to end.
        /// </param>
        public OpenNfsRpcSecGssOptions(
            string targetSpn,
            string? targetName = null,
            OpenNfsRpcGssService service = OpenNfsRpcGssService.None)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetSpn);

            if (!Enum.IsDefined(service))
            {
                throw new ArgumentOutOfRangeException(nameof(service), service, "The RPCSEC_GSS service must be a defined OpenNfsRpcGssService value.");
            }

            TargetSpn = targetSpn;
            TargetName = string.IsNullOrWhiteSpace(targetName)
                ? DeriveTargetName(targetSpn)
                : targetName;
            Service = service;
        }

        /// <summary>
        /// Gets the Kerberos service principal name in canonical form.
        /// </summary>
        public string TargetSpn { get; }

        /// <summary>
        /// Gets the NegotiateAuthentication target name used by the initiating client context.
        /// </summary>
        public string TargetName { get; }

        /// <summary>
        /// Gets the requested RPCSEC_GSS service level.
        /// </summary>
        public OpenNfsRpcGssService Service { get; }

        private static string DeriveTargetName(string targetSpn)
        {
            int realmSeparatorIndex = targetSpn.IndexOf('@', StringComparison.Ordinal);
            if (realmSeparatorIndex <= 0)
            {
                return targetSpn;
            }

            return targetSpn.Substring(0, realmSeparatorIndex);
        }
    }
}
