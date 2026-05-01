namespace OpenNFS.KerberosProbe
{
    using System;
    using System.Net.Security;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Security.Kerberos;
    using OpenNFS.Rpc.Security.RpcSecGss;

    /// <summary>
    /// Standalone probe that authenticates against the OpenNFS test KDC, drives token exchange
    /// through <see cref="OpenNfsKerberosMechanism"/>, and exercises a krb5p wrap/unwrap round-trip.
    /// </summary>
    /// <remarks>
    /// The probe expects the host environment to have working Kerberos client setup: <c>KRB5_CONFIG</c>
    /// pointing at the test <c>krb5.conf</c>, a TGT in the credential cache (typically obtained via
    /// <c>kinit -k -t alice.keytab alice@EXAMPLE.TEST</c>), and the KDC reachable on the network.
    /// </remarks>
    public static class Program
    {
        // The mechanism stores the realm-qualified principal because that is what RPCSEC_GSS
        // exposes downstream, but NegotiateAuthentication's client TargetName expects the
        // service/host portion only (GSSAPI's gss_import_name with GSS_C_NT_HOSTBASED_SERVICE).
        private const string MechanismTargetSpn = "nfs/sample.example.test@EXAMPLE.TEST";
        private const string ClientTargetName = "nfs/sample.example.test";
        private const int MaxTokenExchangeRounds = 8;

        /// <summary>
        /// Probe entry point.
        /// </summary>
        /// <param name="args">Command-line arguments. Currently unused.</param>
        /// <returns>0 on success; non-zero on each distinct failure mode.</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("PROBE: starting OpenNFS Kerberos probe");
                Console.WriteLine("PROBE: mechanism target SPN=" + MechanismTargetSpn);
                Console.WriteLine("PROBE: client target name=" + ClientTargetName);

                using OpenNfsKerberosMechanism mechanism = new OpenNfsKerberosMechanism(
                    new OpenNfsKerberosMechanismOptions(MechanismTargetSpn));

                NegotiateAuthenticationClientOptions clientOptions = new NegotiateAuthenticationClientOptions
                {
                    Package = "Kerberos",
                    TargetName = ClientTargetName,
                    RequiredProtectionLevel = ProtectionLevel.EncryptAndSign,
                };

                using NegotiateAuthentication client = new NegotiateAuthentication(clientOptions);

                ReadOnlyMemory<byte> serverContextHandle = ReadOnlyMemory<byte>.Empty;
                byte[]? clientToServerToken = null;
                bool clientAuthenticated = false;
                string? initiatorPrincipal = null;
                CancellationToken cancellationToken = CancellationToken.None;

                for (int round = 0; round < MaxTokenExchangeRounds; round++)
                {
                    NegotiateAuthenticationStatusCode clientStatus;
                    byte[]? newClientToken = client.GetOutgoingBlob(clientToServerToken, out clientStatus);
                    Console.WriteLine($"PROBE: client round={round} status={clientStatus} token-len={newClientToken?.Length ?? 0}");

                    if (clientStatus == NegotiateAuthenticationStatusCode.Completed)
                    {
                        clientAuthenticated = true;
                    }
                    else if (clientStatus != NegotiateAuthenticationStatusCode.ContinueNeeded)
                    {
                        Console.Error.WriteLine($"PROBE: client failed with status {clientStatus}");
                        return 1;
                    }

                    if (newClientToken is null || newClientToken.Length == 0)
                    {
                        if (clientAuthenticated)
                        {
                            Console.WriteLine("PROBE: client side authenticated; no further tokens to send");
                            break;
                        }

                        Console.Error.WriteLine("PROBE: client produced an empty token before authentication completed");
                        return 1;
                    }

                    RpcSecGssAcceptResult acceptResult = await mechanism.AcceptSecurityContextAsync(
                        serverContextHandle,
                        newClientToken,
                        cancellationToken).ConfigureAwait(false);
                    Console.WriteLine(
                        $"PROBE: server round={round} major={acceptResult.MajorStatus} established={acceptResult.IsContextEstablished}"
                        + $" handle-len={acceptResult.ContextHandle.Length} reply-len={acceptResult.OutboundToken.Length}"
                        + $" principal={acceptResult.InitiatorPrincipal ?? "<null>"}");

                    serverContextHandle = acceptResult.ContextHandle;
                    clientToServerToken = acceptResult.OutboundToken.IsEmpty ? null : acceptResult.OutboundToken.ToArray();

                    if (acceptResult.MajorStatus == RpcSecGssMajorStatus.Complete)
                    {
                        initiatorPrincipal = acceptResult.InitiatorPrincipal;
                        if (clientAuthenticated)
                        {
                            break;
                        }
                    }
                    else if (acceptResult.MajorStatus != RpcSecGssMajorStatus.ContinueNeeded)
                    {
                        Console.Error.WriteLine($"PROBE: server failed with major status {acceptResult.MajorStatus}");
                        return 2;
                    }
                }

                if (!clientAuthenticated)
                {
                    Console.Error.WriteLine("PROBE: client never reached Completed");
                    return 3;
                }

                Console.WriteLine("PROBE: context established");
                Console.WriteLine("PROBE: initiator-principal=" + (initiatorPrincipal ?? "<unknown>"));

                byte[] plaintext = System.Text.Encoding.UTF8.GetBytes("OpenNFS-krb5p-payload-2026-05-01");

                // Server-side wrap: produces bytes the client peer is expected to unwrap.
                byte[] serverWrapped = await mechanism.WrapAsync(serverContextHandle, plaintext, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"PROBE: server wrapped {plaintext.Length} bytes -> {serverWrapped.Length} bytes");

                System.Buffers.ArrayBufferWriter<byte> clientUnwrapWriter = new System.Buffers.ArrayBufferWriter<byte>();
                NegotiateAuthenticationStatusCode clientUnwrapStatus = client.Unwrap(serverWrapped, clientUnwrapWriter, out bool clientUnwrapEncrypted);
                if (clientUnwrapStatus != NegotiateAuthenticationStatusCode.Completed)
                {
                    Console.Error.WriteLine($"PROBE: client unwrap of server-wrapped payload failed with {clientUnwrapStatus}");
                    return 4;
                }

                if (!plaintext.AsSpan().SequenceEqual(clientUnwrapWriter.WrittenSpan))
                {
                    Console.Error.WriteLine("PROBE: client-side unwrap of server-wrapped payload mismatched plaintext");
                    return 5;
                }

                Console.WriteLine($"PROBE: client unwrapped server payload OK encrypted={clientUnwrapEncrypted}");

                // Client-side wrap: produces bytes the server peer is expected to unwrap.
                System.Buffers.ArrayBufferWriter<byte> clientWrappedWriter = new System.Buffers.ArrayBufferWriter<byte>();
                NegotiateAuthenticationStatusCode clientWrapStatus = client.Wrap(plaintext, clientWrappedWriter, requestEncryption: true, out bool clientWrapEncrypted);
                if (clientWrapStatus != NegotiateAuthenticationStatusCode.Completed)
                {
                    Console.Error.WriteLine($"PROBE: client wrap failed with {clientWrapStatus}");
                    return 6;
                }

                Console.WriteLine($"PROBE: client wrapped {plaintext.Length} bytes -> {clientWrappedWriter.WrittenCount} bytes encrypted={clientWrapEncrypted}");

                byte[] serverUnwrapped = await mechanism.UnwrapAsync(serverContextHandle, clientWrappedWriter.WrittenSpan.ToArray(), cancellationToken).ConfigureAwait(false);
                if (!plaintext.AsSpan().SequenceEqual(serverUnwrapped))
                {
                    Console.Error.WriteLine("PROBE: server-side unwrap of client-wrapped payload mismatched plaintext");
                    return 7;
                }

                Console.WriteLine($"PROBE: server unwrapped client payload OK len={serverUnwrapped.Length}");
                Console.WriteLine("PROBE: krb5p bidirectional round-trip OK");

#if NET10_0_OR_GREATER
                // krb5i: server computes MIC, client verifies; tamper detection.
                byte[] micMessage = System.Text.Encoding.UTF8.GetBytes("OpenNFS-krb5i-message-2026-05-01");
                byte[] serverMic = await mechanism.ComputeMicAsync(serverContextHandle, micMessage, cancellationToken).ConfigureAwait(false);
                Console.WriteLine($"PROBE: server computed MIC len={serverMic.Length} for {micMessage.Length}-byte message");

                if (!client.VerifyIntegrityCheck(micMessage, serverMic))
                {
                    Console.Error.WriteLine("PROBE: client failed to verify a clean server MIC");
                    return 8;
                }

                Console.WriteLine("PROBE: client verified clean server MIC");

                byte[] tamperedMessage = (byte[])micMessage.Clone();
                tamperedMessage[0] ^= 0x01;
                if (client.VerifyIntegrityCheck(tamperedMessage, serverMic))
                {
                    Console.Error.WriteLine("PROBE: client incorrectly accepted tampered message under server MIC");
                    return 9;
                }

                Console.WriteLine("PROBE: client correctly rejected tampered message under server MIC");

                // Reverse: client computes MIC, server (mechanism) verifies.
                System.Buffers.ArrayBufferWriter<byte> clientMicWriter = new System.Buffers.ArrayBufferWriter<byte>();
                client.ComputeIntegrityCheck(micMessage, clientMicWriter);
                byte[] clientMic = clientMicWriter.WrittenSpan.ToArray();
                Console.WriteLine($"PROBE: client computed MIC len={clientMic.Length}");

                bool serverVerifyClean = await mechanism.VerifyMicAsync(serverContextHandle, micMessage, clientMic, cancellationToken).ConfigureAwait(false);
                if (!serverVerifyClean)
                {
                    Console.Error.WriteLine("PROBE: server failed to verify a clean client MIC");
                    return 10;
                }

                Console.WriteLine("PROBE: server verified clean client MIC");

                bool serverVerifyTampered = await mechanism.VerifyMicAsync(serverContextHandle, tamperedMessage, clientMic, cancellationToken).ConfigureAwait(false);
                if (serverVerifyTampered)
                {
                    Console.Error.WriteLine("PROBE: server incorrectly accepted tampered message under client MIC");
                    return 11;
                }

                Console.WriteLine("PROBE: server correctly rejected tampered message under client MIC");
                Console.WriteLine("PROBE: krb5i tamper-detection round-trip OK");
#else
                Console.WriteLine("PROBE: krb5i tamper-detection skipped (requires net10.0 NegotiateAuthentication MIC APIs)");
#endif

                await mechanism.DeleteSecurityContextAsync(serverContextHandle, cancellationToken).ConfigureAwait(false);
                Console.WriteLine("PROBE: SUCCESS");
                return 0;
            }
            catch (Exception failure)
            {
                Console.Error.WriteLine("PROBE: failed with " + failure.GetType().FullName + ": " + failure.Message);
                Console.Error.WriteLine(failure.StackTrace);
                return 99;
            }
        }
    }
}
