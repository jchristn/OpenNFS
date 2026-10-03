namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Security.Cryptography;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Security.Kerberos;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the RPCSEC_GSS and Kerberos security suite catalog.
    /// </summary>
    internal static class SecuritySuiteSupport
    {
        internal static Task ExecuteRpcSecGssContextEstablishmentAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: server round=0 major=Complete established=True",
                    "PROBE: context established",
                    "initiator-principal=alice@EXAMPLE.TEST",
                },
                cancellationToken);
        }

        internal static Task ExecuteRpcSecGssIntegrityFailureRejectedAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: client correctly rejected tampered message under server MIC",
                    "PROBE: server correctly rejected tampered message under client MIC",
                },
                cancellationToken);
        }

        internal static Task ExecuteKrb5ReadWriteAsync(System.Threading.CancellationToken cancellationToken)
        {
            // RFC 2203 service=NONE (krb5 auth-only) puts a MIC over the credential body in the
            // verifier on every call and a MIC over the sequence number in the reply verifier; the
            // arguments and results travel in the clear. The probe's bidirectional clean-MIC flow
            // exercises the same compute+verify primitives that an actual NFS read/write under
            // auth-only mode would use on every RPC. This case asserts on the success of that flow
            // in both directions; tamper detection is a separate, stricter assertion covered by
            // Krb5iDetectsTamper.
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: context established",
                    "initiator-principal=alice@EXAMPLE.TEST",
                    "PROBE: client verified clean server MIC",
                    "PROBE: server verified clean client MIC",
                },
                cancellationToken);
        }

        internal static Task ExecuteKrb5pEncryptsPayloadAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: krb5p bidirectional round-trip OK",
                    "initiator-principal=alice@EXAMPLE.TEST",
                },
                cancellationToken);
        }

        internal static Task ExecuteKrb5iDetectsTamperAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: krb5i tamper-detection round-trip OK",
                    "PROBE: client correctly rejected tampered message under server MIC",
                    "PROBE: server correctly rejected tampered message under client MIC",
                },
                cancellationToken);
        }

        internal static async Task RunKerberosProbeAsync(IReadOnlyList<string> expectedMarkers, System.Threading.CancellationToken cancellationToken)
        {
            string repositoryRoot = ResolveRepositoryRoot();
            string runProbeScript = Path.Combine(repositoryRoot, "scripts", "interop", "kerberos", "probe", "Run-Probe.ps1");
            if (!File.Exists(runProbeScript))
            {
                throw new InvalidOperationException("Run-Probe.ps1 not found at " + runProbeScript);
            }

            // PowerShellCli resolves pwsh (or Windows PowerShell on Windows) from PATH, so the probe also runs on Linux and macOS.
            PowerShellCommandResult probeResult = await PowerShellCli.RunScriptAsync(
                runProbeScript,
                Array.Empty<string>(),
                repositoryRoot,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(10)).ConfigureAwait(false);
            string stdout = probeResult.StandardOutput;
            string stderr = probeResult.StandardError;

            string combined = stdout + Environment.NewLine + stderr;
            if (probeResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Kerberos probe failed (exit " + probeResult.ExitCode + "). Combined output:" + Environment.NewLine + combined);
            }

            for (int index = 0; index < expectedMarkers.Count; index++)
            {
                if (!combined.Contains(expectedMarkers[index], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Kerberos probe did not surface expected marker '" + expectedMarkers[index] + "'."
                        + Environment.NewLine + "Combined output:" + Environment.NewLine + combined);
                }
            }
        }

        internal static string ResolveRepositoryRoot()
        {
            string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyDir is null)
            {
                throw new InvalidOperationException("Unable to resolve the test assembly directory.");
            }

            DirectoryInfo? directory = new DirectoryInfo(assemblyDir);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                    && File.Exists(Path.Combine(directory.FullName, "src", "OpenNFS.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to locate the OpenNFS repository root from " + assemblyDir + ".");
        }

        internal static void EnsureCredentialEqual(RpcSecGssCredentialBody expected, RpcSecGssCredentialBody actual)
        {
            if (expected.Version != actual.Version
                || expected.Procedure != actual.Procedure
                || expected.SequenceNumber != actual.SequenceNumber
                || expected.Service != actual.Service
                || !expected.ContextHandle.Span.SequenceEqual(actual.ContextHandle.Span))
            {
                throw new InvalidOperationException("Credential round-trip mismatch.");
            }
        }

        internal static opaque_auth BuildRawCredential(
            uint version,
            uint procedureValue,
            uint sequenceNumber,
            uint serviceValue,
            byte[] handle)
        {
            OpenNFS.Rpc.Xdr.XdrWriter writer = new OpenNFS.Rpc.Xdr.XdrWriter();
            writer.WriteUInt32(version);
            writer.WriteUInt32(procedureValue);
            writer.WriteUInt32(sequenceNumber);
            writer.WriteUInt32(serviceValue);
            writer.WriteVariableOpaque(handle, RpcSecGssProtocolConstants.MaximumAuthenticationBodyLength);

            return new opaque_auth
            {
                flavor = auth_flavor.RPCSEC_GSS,
                body = writer.ToArray(),
            };
        }

        internal static void EnsureThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(TException).Name + " was not thrown.");
        }
    }
}
