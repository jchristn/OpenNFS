namespace OpenNFS.Rpc.Transport
{
    using System;

    /// <summary>
    /// Applies transport-policy rules for protocol and version combinations.
    /// </summary>
    public static class RpcTransportPolicy
    {
        private const uint MountProgram = 100005;
        private const uint MountV3 = 3;
        private const uint NfsProgram = 100003;
        private const uint NfsV3 = 3;
        private const uint NlmProgram = 100021;
        private const uint NlmV4 = 4;
        private const uint NsmProgram = 100024;
        private const uint NsmV1 = 1;
        private const uint PortmapProgram = 100000;
        private const uint PortmapV2 = 2;
        private const uint RpcbindV3 = 3;
        private const uint RpcbindV4 = 4;

        /// <summary>
        /// Ensures that UDP is supported for the supplied RPC program binding.
        /// </summary>
        /// <param name="programBinding">The program and version pairing to validate.</param>
        public static void EnsureUdpSupported(RpcProgramBinding programBinding)
        {
            if (!IsUdpSupported(programBinding))
            {
                throw new NotSupportedException(
                    "UDP transport is only enabled for v3-era RPC flows. Program binding '" + programBinding.ToString() + "' must use TCP.");
            }
        }

        /// <summary>
        /// Determines whether UDP is supported for the supplied RPC program binding.
        /// </summary>
        /// <param name="programBinding">The program and version pairing to evaluate.</param>
        /// <returns><c>true</c> when UDP is supported; otherwise <c>false</c>.</returns>
        public static bool IsUdpSupported(RpcProgramBinding programBinding)
        {
            switch (programBinding.ProgramNumber)
            {
                case PortmapProgram:
                    return programBinding.VersionNumber == PortmapV2
                        || programBinding.VersionNumber == RpcbindV3
                        || programBinding.VersionNumber == RpcbindV4;
                case NfsProgram:
                    return programBinding.VersionNumber == NfsV3;
                case MountProgram:
                    return programBinding.VersionNumber == MountV3;
                case NlmProgram:
                    return programBinding.VersionNumber == NlmV4;
                case NsmProgram:
                    return programBinding.VersionNumber == NsmV1;
                default:
                    return false;
            }
        }
    }
}
