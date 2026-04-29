#pragma warning disable CS1591
namespace OpenNFS.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// RPCSEC_GSS service levels surfaced by NFSv4.0 security-discovery replies.
    /// </summary>
    public enum OpenNfsRpcGssService
    {
        None = 1,
        Integrity = 2,
        Privacy = 3,
    }

    /// <summary>
    /// A single NFSv4.0 SECINFO flavor advertisement.
    /// </summary>
    public sealed class OpenNfsV40SecurityFlavorInfo
    {
        public OpenNfsV40SecurityFlavorInfo(
            OpenNfsRpcAuthenticationFlavor flavor,
            OpenNfsRpcGssService? rpcSecGssService = null,
            uint? rpcSecGssQualityOfProtection = null,
            ReadOnlyMemory<byte> rpcSecGssMechanismOid = default)
        {
            Flavor = flavor;
            RpcSecGssService = rpcSecGssService;
            RpcSecGssQualityOfProtection = rpcSecGssQualityOfProtection;
            RpcSecGssMechanismOid = rpcSecGssMechanismOid.Length == 0
                ? ReadOnlyMemory<byte>.Empty
                : new ReadOnlyMemory<byte>(rpcSecGssMechanismOid.ToArray());
        }

        public OpenNfsRpcAuthenticationFlavor Flavor { get; }

        public OpenNfsRpcGssService? RpcSecGssService { get; }

        public uint? RpcSecGssQualityOfProtection { get; }

        public ReadOnlyMemory<byte> RpcSecGssMechanismOid { get; }
    }

    /// <summary>
    /// Typed NFSv4.0 SECINFO result surfaced by the grouped client API.
    /// </summary>
    public sealed class OpenNfsV40SecurityInfoResult
    {
        public OpenNfsV40SecurityInfoResult(
            OpenNfsV40Status status,
            IReadOnlyList<OpenNfsV40SecurityFlavorInfo>? securityFlavors = null)
        {
            Status = status;
            SecurityFlavors = CopySecurityFlavors(securityFlavors);
        }

        public OpenNfsV40Status Status { get; }

        public bool IsSuccess => Status == OpenNfsV40Status.Ok;

        public IReadOnlyList<OpenNfsV40SecurityFlavorInfo> SecurityFlavors { get; }

        private static IReadOnlyList<OpenNfsV40SecurityFlavorInfo> CopySecurityFlavors(
            IReadOnlyList<OpenNfsV40SecurityFlavorInfo>? securityFlavors)
        {
            if (securityFlavors is null || securityFlavors.Count == 0)
            {
                return Array.Empty<OpenNfsV40SecurityFlavorInfo>();
            }

            OpenNfsV40SecurityFlavorInfo[] copy = new OpenNfsV40SecurityFlavorInfo[securityFlavors.Count];
            for (int index = 0; index < securityFlavors.Count; index++)
            {
                copy[index] = securityFlavors[index];
            }

            return copy;
        }
    }
}
#pragma warning restore CS1591
