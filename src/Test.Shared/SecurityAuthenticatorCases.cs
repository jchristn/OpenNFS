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
    /// Authenticator rejection and builder-registration RPCSEC_GSS suites.
    /// </summary>
    internal static class SecurityAuthenticatorCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
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
            };
        }
    }
}
