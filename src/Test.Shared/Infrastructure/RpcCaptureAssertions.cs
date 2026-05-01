namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;

    internal static class RpcCaptureAssertions
    {
        public static void AssertAuthSysCredential(CapturedRpcEnvelope capture, TestPrincipalIdentity expectedPrincipal)
        {
            ArgumentNullException.ThrowIfNull(capture);
            ArgumentNullException.ThrowIfNull(expectedPrincipal);

            call_body callBody = capture.CallBody
                ?? throw new InvalidOperationException("Expected the captured RPC envelope to be a CALL with a call body.");

            authsys_parms actualCredential = RpcAuthenticationCodec.ReadSystem(
                callBody.cred ?? throw new InvalidOperationException("Expected the captured RPC call to carry a credential."));

            if (!string.Equals(actualCredential.machinename, expectedPrincipal.MachineName, StringComparison.Ordinal)
                || actualCredential.uid != expectedPrincipal.UserId
                || actualCredential.gid != expectedPrincipal.GroupId
                || actualCredential.gids is null
                || !actualCredential.gids.SequenceEqual(expectedPrincipal.SupplementaryGroupIds))
            {
                throw new InvalidOperationException(
                    "Expected the captured AUTH_SYS credential to match the expected principal. "
                    + "Observed machine='" + actualCredential.machinename
                    + "', uid=" + actualCredential.uid.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", gid=" + actualCredential.gid.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        public static void AssertCallRoutingSequence(
            IReadOnlyList<CapturedRpcEnvelope> captures,
            uint expectedProgramNumber,
            uint expectedVersionNumber,
            params uint[] expectedProcedureNumbers)
        {
            ArgumentNullException.ThrowIfNull(captures);
            ArgumentNullException.ThrowIfNull(expectedProcedureNumbers);

            if (captures.Count != expectedProcedureNumbers.Length)
            {
                throw new InvalidOperationException(
                    "Expected " + expectedProcedureNumbers.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " captured RPC calls, but observed "
                    + captures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ".");
            }

            for (int index = 0; index < captures.Count; index++)
            {
                call_body callBody = captures[index].CallBody
                    ?? throw new InvalidOperationException(
                        "Expected captured RPC envelope #" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " to be a CALL.");

                if (callBody.prog != expectedProgramNumber
                    || callBody.vers != expectedVersionNumber
                    || callBody.proc != expectedProcedureNumbers[index])
                {
                    throw new InvalidOperationException(
                        "Expected captured RPC call #" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " to target program="
                        + expectedProgramNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", version="
                        + expectedVersionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", procedure="
                        + expectedProcedureNumbers[index].ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", but observed program="
                        + callBody.prog.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", version="
                        + callBody.vers.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ", procedure="
                        + callBody.proc.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + ".");
                }
            }
        }
    }
}
