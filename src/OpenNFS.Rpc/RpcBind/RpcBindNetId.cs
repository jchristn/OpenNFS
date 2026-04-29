namespace OpenNFS.Rpc.RpcBind
{
    using System;

    internal static class RpcBindNetId
    {
        public static string GetNetId(RpcBindingProtocol protocol)
        {
            switch (protocol)
            {
                case RpcBindingProtocol.Tcp:
                    return "tcp";
                case RpcBindingProtocol.Udp:
                    return "udp";
                default:
                    throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported rpcbind transport protocol.");
            }
        }

        public static bool TryGetProtocol(string netId, out RpcBindingProtocol protocol)
        {
            ArgumentNullException.ThrowIfNull(netId);

            if (string.Equals(netId, "tcp", StringComparison.OrdinalIgnoreCase))
            {
                protocol = RpcBindingProtocol.Tcp;
                return true;
            }

            if (string.Equals(netId, "udp", StringComparison.OrdinalIgnoreCase))
            {
                protocol = RpcBindingProtocol.Udp;
                return true;
            }

            protocol = default;
            return false;
        }

        public static bool TryGetProtocol(uint protocolNumber, out RpcBindingProtocol protocol)
        {
            if (protocolNumber == (uint)RpcBindingProtocol.Tcp)
            {
                protocol = RpcBindingProtocol.Tcp;
                return true;
            }

            if (protocolNumber == (uint)RpcBindingProtocol.Udp)
            {
                protocol = RpcBindingProtocol.Udp;
                return true;
            }

            protocol = default;
            return false;
        }
    }
}
