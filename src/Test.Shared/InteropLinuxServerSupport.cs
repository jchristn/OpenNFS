namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for Linux userspace and kernel-server client interop suites.
    /// </summary>
    internal static class InteropLinuxServerSupport
    {
        internal static Task ExecuteClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceSupport.ExecuteClientAgainstLinuxServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceSupport.ExecuteClientAgainstLinuxServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceSupport.ExecuteNegativeClientAgainstLinuxServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceSupport.ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdSupport.ExecuteClientAgainstLinuxKnfsdServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdSupport.ExecuteNegativeClientAgainstLinuxKnfsdServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdSupport.ExecuteClientAgainstLinuxKnfsdServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxKnfsdSupport.ExecuteNegativeClientAgainstLinuxKnfsdServerOverNfs40Async(cancellationToken);
        }
    }
}
