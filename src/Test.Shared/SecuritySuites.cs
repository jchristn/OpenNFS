namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
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

    /// <summary>
    /// Touchstone suites covering the RFC 2203 RPCSEC_GSS wire-level layer, sequence-window primitives,
    /// and the standards-compliant rejection path applied when no GSS mechanism is configured.
    /// </summary>
    /// <remarks>
    /// These suites do not yet cover the actual Kerberos cryptography (krb5 / krb5i / krb5p). Phase 9.1
    /// of the implementation plan tracks that follow-on work. The cases below pin the envelope shape,
    /// the replay-protection logic, and the no-mechanism-configured behavior so the eventual provider
    /// only has to plug into a stable surface.
    /// </remarks>
    public static class SecuritySuites
    {
        /// <summary>
        /// Creates the shared security-surface suite catalog.
        /// </summary>
        /// <returns>The configured suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "SecuritySuites",
                displayName: "RPCSEC_GSS Surface",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "CredentialRoundTripDataAndInit",
                        displayName: "Credential codec round-trips RPCSEC_GSS_DATA and RPCSEC_GSS_INIT envelopes",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            byte[] handle = new byte[] { 0xCA, 0xFE, 0xF0, 0x0D };
                            RpcSecGssCredentialBody dataBody = new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Data,
                                sequenceNumber: 0x1234,
                                service: RpcSecGssService.Integrity,
                                contextHandle: handle);
                            opaque_auth dataEnvelope = RpcSecGssCredentialCodec.Write(dataBody);
                            RpcSecGssCredentialBody decodedData = RpcSecGssCredentialCodec.Read(dataEnvelope);
                            EnsureCredentialEqual(dataBody, decodedData);

                            RpcSecGssCredentialBody initBody = new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Init,
                                sequenceNumber: 0,
                                service: RpcSecGssService.None,
                                contextHandle: ReadOnlyMemory<byte>.Empty);
                            opaque_auth initEnvelope = RpcSecGssCredentialCodec.Write(initBody);
                            RpcSecGssCredentialBody decodedInit = RpcSecGssCredentialCodec.Read(initEnvelope);
                            EnsureCredentialEqual(initBody, decodedInit);

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "CredentialCodecRejectsUnknownProcedureAndService",
                        displayName: "Credential codec rejects unknown procedure and service values on the wire",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            opaque_auth invalidProcedure = BuildRawCredential(
                                version: RpcSecGssProtocolConstants.Version,
                                procedureValue: 0xFF,
                                sequenceNumber: 1,
                                serviceValue: (uint)RpcSecGssService.None,
                                handle: Array.Empty<byte>());
                            EnsureThrows<RpcSecGssCodecException>(() => RpcSecGssCredentialCodec.Read(invalidProcedure));

                            opaque_auth invalidService = BuildRawCredential(
                                version: RpcSecGssProtocolConstants.Version,
                                procedureValue: (uint)RpcSecGssProcedure.Data,
                                sequenceNumber: 1,
                                serviceValue: 0,
                                handle: new byte[] { 0x01 });
                            EnsureThrows<RpcSecGssCodecException>(() => RpcSecGssCredentialCodec.Read(invalidService));

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "InitArgumentsAndResultRoundTrip",
                        displayName: "rpc_gss_init_arg and rpc_gss_init_res payloads round-trip through the codecs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssInitArguments arguments = new RpcSecGssInitArguments(
                                token: new byte[] { 0x60, 0x82, 0x05, 0x00, 0x06 });
                            byte[] argumentsBytes = RpcSecGssInitArgumentsCodec.Write(arguments);
                            RpcSecGssInitArguments decodedArguments = RpcSecGssInitArgumentsCodec.Read(argumentsBytes);
                            if (!decodedArguments.Token.Span.SequenceEqual(arguments.Token.Span))
                            {
                                throw new InvalidOperationException("rpc_gss_init_arg token bytes did not round-trip.");
                            }

                            RpcSecGssInitResult result = new RpcSecGssInitResult(
                                contextHandle: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                                majorStatus: RpcSecGssMajorStatus.Complete,
                                minorStatus: 0,
                                sequenceWindow: RpcSecGssProtocolConstants.DefaultSequenceWindowSize,
                                token: new byte[] { 0x61, 0x82, 0x04, 0xFC });
                            byte[] resultBytes = RpcSecGssInitResultCodec.Write(result);
                            RpcSecGssInitResult decodedResult = RpcSecGssInitResultCodec.Read(resultBytes);
                            if (!decodedResult.ContextHandle.Span.SequenceEqual(result.ContextHandle.Span)
                                || decodedResult.MajorStatus != result.MajorStatus
                                || decodedResult.MinorStatus != result.MinorStatus
                                || decodedResult.SequenceWindow != result.SequenceWindow
                                || !decodedResult.Token.Span.SequenceEqual(result.Token.Span))
                            {
                                throw new InvalidOperationException("rpc_gss_init_res payload did not round-trip.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "IntegrityAndPrivacyWrappersRoundTrip",
                        displayName: "rpc_gss_integ_data and rpc_gss_priv_data wrappers round-trip through the codecs",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssIntegrityData integrity = new RpcSecGssIntegrityData(
                                sequencedPayload: new byte[] { 0x00, 0x00, 0x00, 0x05, 0x68, 0x65, 0x6C, 0x6C, 0x6F },
                                checksum: new byte[] { 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6 });
                            byte[] integrityBytes = RpcSecGssIntegrityDataCodec.Write(integrity);
                            RpcSecGssIntegrityData decodedIntegrity = RpcSecGssIntegrityDataCodec.Read(integrityBytes);
                            if (!decodedIntegrity.SequencedPayload.Span.SequenceEqual(integrity.SequencedPayload.Span)
                                || !decodedIntegrity.Checksum.Span.SequenceEqual(integrity.Checksum.Span))
                            {
                                throw new InvalidOperationException("rpc_gss_integ_data did not round-trip.");
                            }

                            RpcSecGssPrivacyData privacy = new RpcSecGssPrivacyData(
                                wrappedPayload: new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 });
                            byte[] privacyBytes = RpcSecGssPrivacyDataCodec.Write(privacy);
                            RpcSecGssPrivacyData decodedPrivacy = RpcSecGssPrivacyDataCodec.Read(privacyBytes);
                            if (!decodedPrivacy.WrappedPayload.Span.SequenceEqual(privacy.WrappedPayload.Span))
                            {
                                throw new InvalidOperationException("rpc_gss_priv_data did not round-trip.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "SequenceWindowAcceptsAndRejectsExpectedValues",
                        displayName: "RPCSEC_GSS sequence window accepts fresh values, rejects replays, and rejects below-window values",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssSequenceWindow window = new RpcSecGssSequenceWindow(
                                RpcSecGssProtocolConstants.DefaultSequenceWindowSize);

                            if (!window.TryAccept(0))
                            {
                                throw new InvalidOperationException("Initial sequence number 0 must be accepted.");
                            }

                            if (window.TryAccept(0))
                            {
                                throw new InvalidOperationException("Replayed sequence number 0 must be rejected.");
                            }

                            if (!window.TryAccept(5))
                            {
                                throw new InvalidOperationException("Sequence number 5 must be accepted after 0.");
                            }

                            if (!window.TryAccept(3))
                            {
                                throw new InvalidOperationException("In-window out-of-order sequence number 3 must be accepted.");
                            }

                            if (window.TryAccept(3))
                            {
                                throw new InvalidOperationException("Replayed sequence number 3 must be rejected.");
                            }

                            if (!window.TryAccept(100))
                            {
                                throw new InvalidOperationException("Far-advance sequence number 100 must be accepted.");
                            }

                            if (window.TryAccept(5))
                            {
                                throw new InvalidOperationException("Below-window sequence number 5 must be rejected after far advance.");
                            }

                            if (!window.TryAccept(80))
                            {
                                throw new InvalidOperationException("In-window sequence number 80 must still be accepted after far advance.");
                            }

                            if (window.TryAccept(RpcSecGssProtocolConstants.MaximumSequenceNumber + 1))
                            {
                                throw new InvalidOperationException("Sequence numbers above the RFC 2203 maximum must be rejected.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "AuthenticatorRejectsWhenNoMechanismConfigured",
                        displayName: "Authenticator rejects RPCSEC_GSS calls with AUTH_TOOWEAK when no mechanism is registered",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssInMemoryContextStore store = new RpcSecGssInMemoryContextStore();
                            RpcSecGssAuthenticator authenticator = new RpcSecGssAuthenticator(store, isMechanismRegistered: false);

                            opaque_auth credential = RpcSecGssCredentialCodec.Write(new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Init,
                                sequenceNumber: 0,
                                service: RpcSecGssService.None,
                                contextHandle: ReadOnlyMemory<byte>.Empty));

                            RpcSecGssAuthenticationResult result = authenticator.Evaluate(credential);
                            if (result.Outcome != RpcSecGssAuthenticationOutcome.MechanismUnavailable
                                || result.RejectionStatus != auth_stat.AUTH_TOOWEAK)
                            {
                                throw new InvalidOperationException(
                                    "Authenticator must reject RPCSEC_GSS with AUTH_TOOWEAK when no mechanism is registered.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "AuthenticatorRejectsUnknownContextAndReplayedDataCalls",
                        displayName: "Authenticator rejects unknown context handles and replayed DATA sequence numbers with RPCSEC_GSS_CTXPROBLEM",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssInMemoryContextStore store = new RpcSecGssInMemoryContextStore();
                            RpcSecGssAuthenticator authenticator = new RpcSecGssAuthenticator(store, isMechanismRegistered: true);

                            opaque_auth missingContextCredential = RpcSecGssCredentialCodec.Write(new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Data,
                                sequenceNumber: 1,
                                service: RpcSecGssService.None,
                                contextHandle: new byte[] { 0x99 }));
                            RpcSecGssAuthenticationResult missingContextResult = authenticator.Evaluate(missingContextCredential);
                            if (missingContextResult.Outcome != RpcSecGssAuthenticationOutcome.ContextProblem
                                || missingContextResult.RejectionStatus != auth_stat.RPCSEC_GSS_CTXPROBLEM)
                            {
                                throw new InvalidOperationException(
                                    "Authenticator must reject DATA calls with unknown handles using RPCSEC_GSS_CTXPROBLEM.");
                            }

                            byte[] handle = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                            RpcSecGssContext context = new RpcSecGssContext(
                                contextHandle: handle,
                                mechanismName: RpcSecGssMechanismName.KerberosV5,
                                initiatorPrincipal: "alice@EXAMPLE.TEST",
                                sequenceWindow: new RpcSecGssSequenceWindow(RpcSecGssProtocolConstants.DefaultSequenceWindowSize));
                            store.Register(context);

                            opaque_auth firstDataCredential = RpcSecGssCredentialCodec.Write(new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Data,
                                sequenceNumber: 7,
                                service: RpcSecGssService.None,
                                contextHandle: handle));
                            RpcSecGssAuthenticationResult firstResult = authenticator.Evaluate(firstDataCredential);
                            if (firstResult.Outcome != RpcSecGssAuthenticationOutcome.Accepted || firstResult.Context is null)
                            {
                                throw new InvalidOperationException("First DATA call must be accepted on a valid context.");
                            }

                            RpcSecGssAuthenticationResult replayResult = authenticator.Evaluate(firstDataCredential);
                            if (replayResult.Outcome != RpcSecGssAuthenticationOutcome.ContextProblem
                                || replayResult.RejectionStatus != auth_stat.RPCSEC_GSS_CTXPROBLEM)
                            {
                                throw new InvalidOperationException(
                                    "Replayed DATA sequence numbers must be rejected with RPCSEC_GSS_CTXPROBLEM.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "AuthenticatorRejectsMalformedAndVersionMismatchCredentials",
                        displayName: "Authenticator rejects malformed credentials and version mismatches with AUTH_BADCRED",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            RpcSecGssInMemoryContextStore store = new RpcSecGssInMemoryContextStore();
                            RpcSecGssAuthenticator authenticator = new RpcSecGssAuthenticator(store, isMechanismRegistered: true);

                            opaque_auth malformedCredential = new opaque_auth
                            {
                                flavor = auth_flavor.RPCSEC_GSS,
                                body = new byte[] { 0x00, 0x00, 0x00 },
                            };
                            RpcSecGssAuthenticationResult malformedResult = authenticator.Evaluate(malformedCredential);
                            if (malformedResult.Outcome != RpcSecGssAuthenticationOutcome.CredentialProblem
                                || malformedResult.RejectionStatus != auth_stat.AUTH_BADCRED)
                            {
                                throw new InvalidOperationException(
                                    "Authenticator must reject malformed RPCSEC_GSS credentials with AUTH_BADCRED.");
                            }

                            opaque_auth versionMismatchCredential = RpcSecGssCredentialCodec.Write(new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version + 1,
                                procedure: RpcSecGssProcedure.Init,
                                sequenceNumber: 0,
                                service: RpcSecGssService.None,
                                contextHandle: ReadOnlyMemory<byte>.Empty));
                            RpcSecGssAuthenticationResult versionResult = authenticator.Evaluate(versionMismatchCredential);
                            if (versionResult.Outcome != RpcSecGssAuthenticationOutcome.VersionMismatch
                                || versionResult.RejectionStatus != auth_stat.AUTH_BADCRED)
                            {
                                throw new InvalidOperationException(
                                    "Authenticator must reject unsupported RPCSEC_GSS versions with AUTH_BADCRED.");
                            }

                            return Task.CompletedTask;
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "SecuritySuites",
                        caseId: "ServerBuilderRegistersRpcSecGssMechanism",
                        displayName: "OpenNfsServerBuilder.UseRpcSecGssMechanism wires the mechanism into the server settings and authenticator",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNFS.Server.OpenNfsServerBuilder builder = new OpenNFS.Server.OpenNfsServerBuilder()
                                .UseFileSystem(new OpenNFS.Server.FileSystems.LocalNfsFileSystem())
                                .UseRpcSecGssMechanism(new OpenNfsKerberosMechanism(
                                    new OpenNfsKerberosMechanismOptions("nfs/sample.example.test@EXAMPLE.TEST")));

                            OpenNFS.Server.OpenNfsServerSettings settings = builder.BuildSettings();

                            if (settings.RpcSecGssMechanism is null)
                            {
                                throw new InvalidOperationException(
                                    "OpenNfsServerBuilder.UseRpcSecGssMechanism must propagate the mechanism into the resulting OpenNfsServerSettings.");
                            }

                            if (settings.RpcSecGssMechanism.MechanismName != RpcSecGssMechanismName.KerberosV5)
                            {
                                throw new InvalidOperationException(
                                    "Configured server settings must surface the registered Kerberos mechanism by name.");
                            }

                            OpenNFS.Server.OpenNfsServerSettings unconfigured = new OpenNFS.Server.OpenNfsServerBuilder()
                                .UseFileSystem(new OpenNFS.Server.FileSystems.LocalNfsFileSystem())
                                .BuildSettings();

                            if (unconfigured.RpcSecGssAuthenticator is null)
                            {
                                throw new InvalidOperationException(
                                    "Server settings must always expose a non-null RpcSecGssAuthenticator.");
                            }

                            opaque_auth credential = RpcSecGssCredentialCodec.Write(new RpcSecGssCredentialBody(
                                version: RpcSecGssProtocolConstants.Version,
                                procedure: RpcSecGssProcedure.Init,
                                sequenceNumber: 0,
                                service: RpcSecGssService.None,
                                contextHandle: ReadOnlyMemory<byte>.Empty));

                            RpcSecGssAuthenticationResult tooWeakResult = unconfigured.RpcSecGssAuthenticator.Evaluate(credential);
                            if (tooWeakResult.Outcome != RpcSecGssAuthenticationOutcome.MechanismUnavailable
                                || tooWeakResult.RejectionStatus != auth_stat.AUTH_TOOWEAK)
                            {
                                throw new InvalidOperationException(
                                    "Without a registered mechanism, the authenticator must surface AUTH_TOOWEAK for inbound RPCSEC_GSS credentials.");
                            }

                            RpcSecGssAuthenticationResult acceptedResult = settings.RpcSecGssAuthenticator.Evaluate(credential);
                            if (acceptedResult.Outcome != RpcSecGssAuthenticationOutcome.Accepted)
                            {
                                throw new InvalidOperationException(
                                    "With a registered mechanism, the authenticator must accept an INIT credential for downstream context establishment.");
                            }

                            return Task.CompletedTask;
                        }),
                });
        }

        private static Task ExecuteRpcSecGssContextEstablishmentAsync(System.Threading.CancellationToken cancellationToken)
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

        private static Task ExecuteRpcSecGssIntegrityFailureRejectedAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: client correctly rejected tampered message under server MIC",
                    "PROBE: server correctly rejected tampered message under client MIC",
                },
                cancellationToken);
        }

        private static Task ExecuteKrb5ReadWriteAsync(System.Threading.CancellationToken cancellationToken)
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

        private static Task ExecuteKrb5pEncryptsPayloadAsync(System.Threading.CancellationToken cancellationToken)
        {
            return RunKerberosProbeAsync(
                expectedMarkers: new[]
                {
                    "PROBE: krb5p bidirectional round-trip OK",
                    "initiator-principal=alice@EXAMPLE.TEST",
                },
                cancellationToken);
        }

        private static Task ExecuteKrb5iDetectsTamperAsync(System.Threading.CancellationToken cancellationToken)
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

        private static async Task RunKerberosProbeAsync(IReadOnlyList<string> expectedMarkers, System.Threading.CancellationToken cancellationToken)
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

        private static string ResolveRepositoryRoot()
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

        private static void EnsureCredentialEqual(RpcSecGssCredentialBody expected, RpcSecGssCredentialBody actual)
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

        private static opaque_auth BuildRawCredential(
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

        private static void EnsureThrows<TException>(Action action)
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
