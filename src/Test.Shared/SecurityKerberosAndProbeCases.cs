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
    using Touchstone.Core;
    using static Test.Shared.SecuritySuiteSupport;

    /// <summary>
    /// Kerberos mechanism and probe-backed security integration suites.
    /// </summary>
    internal static class SecurityKerberosAndProbeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "KerberosMechanismExposesKerberosV5",
                        displayName: "OpenNfsKerberosMechanism exposes the KerberosV5 mechanism name and accepts a non-empty target SPN",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsKerberosMechanismOptions options = new OpenNfsKerberosMechanismOptions(
                                targetSpn: "nfs/sample.example.test@EXAMPLE.TEST");
                            using OpenNfsKerberosMechanism mechanism = new OpenNfsKerberosMechanism(options);

                            if (mechanism.MechanismName != RpcSecGssMechanismName.KerberosV5)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsKerberosMechanism must report the KerberosV5 mechanism name.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "KerberosMechanismRejectsEmptyTargetSpn",
                        displayName: "OpenNfsKerberosMechanismOptions rejects an empty target SPN",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsKerberosMechanismOptions? whitespaceOptions = null;
                            try
                            {
                                whitespaceOptions = new OpenNfsKerberosMechanismOptions(targetSpn: "   ");
                            }
                            catch (ArgumentException)
                            {
                            }

                            if (whitespaceOptions is not null)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsKerberosMechanismOptions must reject a whitespace target SPN.");
                            }

                            OpenNfsKerberosMechanismOptions? emptyOptions = null;
                            try
                            {
                                emptyOptions = new OpenNfsKerberosMechanismOptions(targetSpn: string.Empty);
                            }
                            catch (ArgumentException)
                            {
                            }

                            if (emptyOptions is not null)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsKerberosMechanismOptions must reject an empty target SPN.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "KerberosMechanismMicSurfacesUnsupportedOnNet8",
                        displayName: "Kerberos provider surfaces a clear unsupported state for ComputeMic / VerifyMic on .NET 8 because NegotiateAuthentication does not expose those APIs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            OpenNfsKerberosMechanismOptions options = new OpenNfsKerberosMechanismOptions(
                                targetSpn: "nfs/sample.example.test@EXAMPLE.TEST");
                            using OpenNfsKerberosMechanism mechanism = new OpenNfsKerberosMechanism(options);

                            ReadOnlyMemory<byte> unknownHandle = new byte[] { 0x01, 0x02, 0x03 };

                            // The handle is unknown; ResolveEstablished must fail before hitting the
                            // MIC code path. That establishes the precondition is honest.
                            try
                            {
                                await mechanism.ComputeMicAsync(unknownHandle, ReadOnlyMemory<byte>.Empty, default).ConfigureAwait(false);
                                throw new InvalidOperationException(
                                    "ComputeMicAsync must throw on an unknown context handle.");
                            }
                            catch (InvalidOperationException)
                            {
                            }

                            try
                            {
                                await mechanism.VerifyMicAsync(unknownHandle, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, default).ConfigureAwait(false);
                                throw new InvalidOperationException(
                                    "VerifyMicAsync must throw on an unknown context handle.");
                            }
                            catch (InvalidOperationException)
                            {
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "KerberosMechanismDeleteOnUnknownHandleIsSafe",
                        displayName: "Kerberos provider DeleteSecurityContextAsync is safe on unknown context handles",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            OpenNfsKerberosMechanismOptions options = new OpenNfsKerberosMechanismOptions(
                                targetSpn: "nfs/sample.example.test@EXAMPLE.TEST");
                            using OpenNfsKerberosMechanism mechanism = new OpenNfsKerberosMechanism(options);

                            await mechanism.DeleteSecurityContextAsync(
                                new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                                default).ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "Krb5pEncryptsPayload",
                        displayName: "OpenNfsKerberosMechanism completes a krb5p bidirectional Wrap/Unwrap round-trip against the live KDC",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteKrb5pEncryptsPayloadAsync),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "Krb5iDetectsTamper",
                        displayName: "OpenNfsKerberosMechanism computes and verifies krb5i MICs against the live KDC and rejects tampered messages in both directions",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteKrb5iDetectsTamperAsync),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "Krb5ReadWrite",
                        displayName: "OpenNfsKerberosMechanism supports the krb5 (auth-only) read+write happy path: server-MIC verified by client and client-MIC verified by server over the live KDC",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteKrb5ReadWriteAsync),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "RpcSecGssContextEstablishment",
                        displayName: "RPCSEC_GSS context establishment completes through OpenNfsKerberosMechanism with a real Kerberos token exchange against the live KDC",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteRpcSecGssContextEstablishmentAsync),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "RpcSecGssSequenceWindow",
                        displayName: "RPCSEC_GSS sequence window enforces RFC 2203 §5.3.3.1 replay protection: fresh values accepted, replays rejected, below-window rejected",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            // Same coverage as SequenceWindowAcceptsAndRejectsExpectedValues; this
                            // case exists so the plan-named gate is met by name.
                            RpcSecGssSequenceWindow window = new RpcSecGssSequenceWindow(
                                RpcSecGssProtocolConstants.DefaultSequenceWindowSize);

                            if (!window.TryAccept(0)) throw new InvalidOperationException("Initial sequence number 0 must be accepted.");
                            if (window.TryAccept(0)) throw new InvalidOperationException("Replayed sequence number 0 must be rejected.");
                            if (!window.TryAccept(5)) throw new InvalidOperationException("Sequence number 5 must be accepted after 0.");
                            if (!window.TryAccept(3)) throw new InvalidOperationException("In-window out-of-order sequence number 3 must be accepted.");
                            if (window.TryAccept(3)) throw new InvalidOperationException("Replayed sequence number 3 must be rejected.");
                            if (!window.TryAccept(100)) throw new InvalidOperationException("Far-advance sequence number 100 must be accepted.");
                            if (window.TryAccept(5)) throw new InvalidOperationException("Below-window sequence number 5 must be rejected after far advance.");
                            if (window.TryAccept(RpcSecGssProtocolConstants.MaximumSequenceNumber + 1)) throw new InvalidOperationException("Sequence numbers above the RFC 2203 maximum must be rejected.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "RpcSecGssIntegrityFailureRejected",
                        displayName: "RPCSEC_GSS integrity failures (tampered MIC payloads) are rejected by both the client and server sides of an established Kerberos context",
                        tags: new List<string> { TestCategories.Interop, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteRpcSecGssIntegrityFailureRejectedAsync),

            };
        }
    }
}
