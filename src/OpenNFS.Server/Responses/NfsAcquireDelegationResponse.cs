#pragma warning disable CS1591
namespace OpenNFS.Server.Responses
{
    using OpenNFS.Server.Delegations;

    /// <summary>
    /// Describes the host decision for an optional NFSv4 delegation grant.
    /// </summary>
    public sealed class NfsAcquireDelegationResponse
    {
        public static readonly NfsAcquireDelegationResponse None = new NfsAcquireDelegationResponse(NfsDelegationKind.None);

        public NfsAcquireDelegationResponse(NfsDelegationKind delegationKind)
        {
            DelegationKind = delegationKind;
        }

        public NfsDelegationKind DelegationKind { get; }
    }
}
#pragma warning restore CS1591
