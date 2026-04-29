namespace OpenNFS.Rpc.RpcBind
{
    using System;

    internal static class RpcBindUniversalAddress
    {
        public static string Create(string host, uint port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("The universal address host must contain a non-empty value.", nameof(host));
            }

            if (port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "The universal address port must be between 0 and 65535.");
            }

            uint high = port / 256;
            uint low = port % 256;
            return host + "." + high + "." + low;
        }

        public static bool TryGetPort(string universalAddress, out uint port)
        {
            ArgumentNullException.ThrowIfNull(universalAddress);

            int lastSeparator = universalAddress.LastIndexOf('.');
            if (lastSeparator <= 0 || lastSeparator == universalAddress.Length - 1)
            {
                port = 0;
                return false;
            }

            int secondLastSeparator = universalAddress.LastIndexOf('.', lastSeparator - 1);
            if (secondLastSeparator <= 0 || secondLastSeparator == lastSeparator - 1)
            {
                port = 0;
                return false;
            }

            string highToken = universalAddress.Substring(secondLastSeparator + 1, lastSeparator - secondLastSeparator - 1);
            string lowToken = universalAddress.Substring(lastSeparator + 1);

            if (!byte.TryParse(highToken, out byte highByte) || !byte.TryParse(lowToken, out byte lowByte))
            {
                port = 0;
                return false;
            }

            port = (uint)((highByte << 8) | lowByte);
            return true;
        }
    }
}
