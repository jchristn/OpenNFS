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
    /// RPCSEC_GSS credential, init, wrapper, and sequence-window suites.
    /// </summary>
    internal static class SecurityRpcSecGssCodecCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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

            };
        }
    }
}
