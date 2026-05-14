namespace Test.Shared
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Shared execution helpers for Linux userspace-server interop flows.
    /// </summary>
    internal static class InteropLinuxUserspaceSupport
    {
        internal static Task ExecuteClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceV3Support.ExecuteClientAgainstLinuxServerAsync(cancellationToken);
        }

        internal static Task ExecuteClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceV40Support.ExecuteClientAgainstLinuxServerOverNfs40Async(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxServerAsync(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceV3Support.ExecuteNegativeClientAgainstLinuxServerAsync(cancellationToken);
        }

        internal static Task ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(CancellationToken cancellationToken)
        {
            return InteropLinuxUserspaceV40Support.ExecuteNegativeClientAgainstLinuxServerOverNfs40Async(cancellationToken);
        }
    }
}
