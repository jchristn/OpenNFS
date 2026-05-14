namespace OpenNFS.Protocol.V40.Compound
{
    using System;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Server;

    internal static class Nfs40CompoundIoSupport
    {
        internal static verifier4 CreateWriteVerifier(byte[] writeVerifierBytes)
        {
            ArgumentNullException.ThrowIfNull(writeVerifierBytes);

            return new verifier4
            {
                Value = writeVerifierBytes.AsSpan().ToArray(),
            };
        }

        internal static stable_how4 MapWriteStability(NfsWriteStability stability)
        {
            return stability switch
            {
                NfsWriteStability.Unstable => stable_how4.UNSTABLE4,
                NfsWriteStability.DataSync => stable_how4.DATA_SYNC4,
                _ => stable_how4.FILE_SYNC4,
            };
        }

        internal static bool TryMapWriteStability(stable_how4? value, out NfsWriteStability stability)
        {
            stability = NfsWriteStability.Unstable;
            if (!value.HasValue)
            {
                return false;
            }

            switch (value.Value)
            {
                case stable_how4.UNSTABLE4:
                    stability = NfsWriteStability.Unstable;
                    return true;
                case stable_how4.DATA_SYNC4:
                    stability = NfsWriteStability.DataSync;
                    return true;
                case stable_how4.FILE_SYNC4:
                    stability = NfsWriteStability.FileSync;
                    return true;
                default:
                    return false;
            }
        }
    }
}
