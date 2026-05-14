namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for Linux kernel-server interop flows.
    /// </summary>
    internal static class InteropLinuxKnfsdSupport
    {
        internal static Task ExecuteClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdV3Support.ExecuteClientAgainstLinuxKnfsdServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdV3Support.ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdV40Support.ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdV40Support.ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async(cancellationToken);
        }
    }
}
