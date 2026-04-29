namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Decoded MOUNT v3 <c>MNT</c> result surfaced by the grouped client decode APIs.
    /// </summary>
    public sealed class OpenNfsMountV3Result
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsMountV3Result"/> class.
        /// </summary>
        /// <param name="status">Decoded MOUNT v3 status code.</param>
        /// <param name="rootFileHandle">
        /// Decoded root filehandle bytes for successful mounts.
        /// Empty when <paramref name="status"/> is not <see cref="OpenNfsMountV3Status.Ok"/>.
        /// </param>
        /// <param name="supportedAuthenticationFlavors">
        /// Advertised ONC RPC authentication flavors for successful mounts.
        /// Empty when <paramref name="status"/> is not <see cref="OpenNfsMountV3Status.Ok"/>.
        /// </param>
        public OpenNfsMountV3Result(
            OpenNfsMountV3Status status,
            ReadOnlyMemory<byte> rootFileHandle = default,
            IReadOnlyList<OpenNfsRpcAuthenticationFlavor>? supportedAuthenticationFlavors = null)
        {
            Status = status;
            RootFileHandle = rootFileHandle.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(rootFileHandle.ToArray());
            SupportedAuthenticationFlavors = CopyAuthenticationFlavors(supportedAuthenticationFlavors);
        }

        /// <summary>
        /// Gets the decoded MOUNT v3 status code.
        /// </summary>
        public OpenNfsMountV3Status Status { get; }

        /// <summary>
        /// Gets a value indicating whether the mount succeeded.
        /// </summary>
        public bool IsSuccess
        {
            get
            {
                return Status == OpenNfsMountV3Status.Ok;
            }
        }

        /// <summary>
        /// Gets the decoded root filehandle bytes for successful mounts.
        /// </summary>
        public ReadOnlyMemory<byte> RootFileHandle { get; }

        /// <summary>
        /// Gets the advertised ONC RPC authentication flavors for successful mounts.
        /// </summary>
        public IReadOnlyList<OpenNfsRpcAuthenticationFlavor> SupportedAuthenticationFlavors { get; }

        private static IReadOnlyList<OpenNfsRpcAuthenticationFlavor> CopyAuthenticationFlavors(
            IReadOnlyList<OpenNfsRpcAuthenticationFlavor>? supportedAuthenticationFlavors)
        {
            if (supportedAuthenticationFlavors is null || supportedAuthenticationFlavors.Count == 0)
            {
                return Array.Empty<OpenNfsRpcAuthenticationFlavor>();
            }

            OpenNfsRpcAuthenticationFlavor[] copy = new OpenNfsRpcAuthenticationFlavor[supportedAuthenticationFlavors.Count];
            for (int index = 0; index < supportedAuthenticationFlavors.Count; index++)
            {
                copy[index] = supportedAuthenticationFlavors[index];
            }

            return copy;
        }
    }
}
