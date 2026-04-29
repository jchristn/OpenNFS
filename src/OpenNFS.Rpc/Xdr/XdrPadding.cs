namespace OpenNFS.Rpc.Xdr
{
    using System;

    internal static class XdrPadding
    {
        public static int GetPaddingLength(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            int remainder = length & 3;
            if (remainder == 0)
            {
                return 0;
            }

            return 4 - remainder;
        }
    }
}
