#pragma warning disable CS1591
namespace OpenNFS.Server.Delegations
{
    /// <summary>
    /// Describes the protocol-neutral delegation type a host is willing to grant.
    /// </summary>
    public enum NfsDelegationKind
    {
        None = 0,
        Read = 1,
        Write = 2,
    }
}
#pragma warning restore CS1591
