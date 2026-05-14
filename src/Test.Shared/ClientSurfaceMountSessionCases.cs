namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Apis;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Raw;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.ClientSurfaceSuiteSupport;

    /// <summary>
    /// Mounted-session, dedicated-mount, denied-mount, and typed-credential client suites.
    /// </summary>
    internal static class ClientSurfaceMountSessionCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountSessionSupportsPathFirstReadWriteAndMetadata",
                        displayName: "Mount sessions support path-first browse, read, write, and metadata flows",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionSupportsPathFirstReadWriteAndMetadataAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountSessionMaintainsSameSessionMutationConsistency",
                        displayName: "Mount sessions keep same-session mutation paths consistent",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionMaintainsSameSessionMutationConsistencyAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigation",
                        displayName: "Mount sessions support concurrent path operations and reject relative navigation segments",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountSessionSupportsConcurrentPathOperationsAndRejectsRelativeNavigationAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountAsyncUsesDedicatedMountEndpoint",
                        displayName: "Client MountAsync uses a dedicated mount endpoint and returns a disposable mounted session",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountAsyncUsesDedicatedMountEndpointAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "RpcSecGssAuthOnlyMountExecutesAgainstKerberosProbe",
                        displayName: "The public client establishes an auth-only RPCSEC_GSS Kerberos context and executes EXPORT, MNT, and mounted-session NFSv3 reads against a live KDC-backed probe",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        skip: !KerberosProbeEnvironment.Current.IsAvailable,
                        skipReason: KerberosProbeEnvironment.Current.SkipReason,
                        executeAsync: ExecuteRpcSecGssAuthOnlyMountAgainstKerberosProbeAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "RawV42CompoundExecutesAgainstPublicServerApplication",
                        displayName: "The public raw client surface executes a real NFSv4.2 EXCHANGE_ID, CREATE_SESSION, SEQUENCE, PUTROOTFH, LOOKUP, and IO_ADVISE flow against OpenNfsServerApplication",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteRawV42CompoundAgainstPublicServerApplicationAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "GroupedV42FileApisExecuteAgainstPublicServerApplication",
                        displayName: "The grouped NFSv4.2 file helpers establish and reuse a client-scoped session while executing IO_ADVISE, READ_PLUS, SEEK, ALLOCATE, DEALLOCATE, COPY, and CLONE against OpenNfsServerApplication",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteGroupedV42FileApisAgainstPublicServerApplicationAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "GroupedV42FileApisReuseSessionAcrossCalls",
                        displayName: "The grouped NFSv4.2 file helpers reuse a healthy client-scoped session across consecutive sequenced calls",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteGroupedV42FileApisReuseSessionAcrossCallsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "GroupedV42FileApisReconnectAfterTransportBreak",
                        displayName: "The grouped NFSv4.2 file helpers reconnect, bind the existing session, and preserve SEQUENCE across a transport break",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteGroupedV42FileApisReconnectAfterTransportBreakAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "MountAsyncThrowsForDeniedMount",
                        displayName: "Client MountAsync throws a clear failure for denied mounts",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteMountAsyncThrowsForDeniedMountAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "TryLifecycleAndMountSurfacesTypedResults",
                        displayName: "Client Try lifecycle and mount surfaces return typed result envelopes",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteTryLifecycleAndMountSurfacesTypedResultsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "TypedCredentialMountAsyncSurfaceIsPresent",
                        displayName: "OpenNfsClient.MountAsync(string, OpenNfsClientCredential, CancellationToken) accepts typed credentials and validates flavor against the builder configuration",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            // Anonymous singleton must report AuthNone.
                            if (OpenNfsClientCredential.Anonymous.Flavor != OpenNfsAuthenticationFlavor.AuthNone
                                || OpenNfsClientCredential.Anonymous.AuthSys is not null)
                            {
                                throw new InvalidOperationException("OpenNfsClientCredential.Anonymous must carry AuthNone with no AuthSys payload.");
                            }

                            // FromAuthSys wraps and exposes the supplied identity.
                            OpenNfsAuthSysCredentials authSys = new OpenNfsAuthSysCredentials("typed-creds-test", 4242, 4243);
                            OpenNfsClientCredential authSysCredential = OpenNfsClientCredential.FromAuthSys(authSys);
                            if (authSysCredential.Flavor != OpenNfsAuthenticationFlavor.AuthSys
                                || !ReferenceEquals(authSysCredential.AuthSys, authSys))
                            {
                                throw new InvalidOperationException("OpenNfsClientCredential.FromAuthSys must carry AuthSys with the supplied identity values.");
                            }

                            // FromAuthSys(null) must throw.
                            bool nullGuardFired = false;
                            try
                            {
                                _ = OpenNfsClientCredential.FromAuthSys(null!);
                            }
                            catch (ArgumentNullException)
                            {
                                nullGuardFired = true;
                            }

                            if (!nullGuardFired)
                            {
                                throw new InvalidOperationException("OpenNfsClientCredential.FromAuthSys(null) must throw ArgumentNullException.");
                            }

                            // The MountAsync(string, OpenNfsClientCredential, CancellationToken) overload must be present.
                            System.Reflection.MethodInfo? typedMountAsync = typeof(OpenNfsClient).GetMethod(
                                nameof(OpenNfsClient.MountAsync),
                                new[] { typeof(string), typeof(OpenNfsClientCredential), typeof(System.Threading.CancellationToken) });
                            if (typedMountAsync is null)
                            {
                                throw new InvalidOperationException("OpenNfsClient must expose MountAsync(string, OpenNfsClientCredential, CancellationToken).");
                            }

                            // Same for TryMountAsync.
                            System.Reflection.MethodInfo? typedTryMountAsync = typeof(OpenNfsClient).GetMethod(
                                nameof(OpenNfsClient.TryMountAsync),
                                new[] { typeof(string), typeof(OpenNfsClientCredential), typeof(System.Threading.CancellationToken) });
                            if (typedTryMountAsync is null)
                            {
                                throw new InvalidOperationException("OpenNfsClient must expose TryMountAsync(string, OpenNfsClientCredential, CancellationToken).");
                            }

                            _ = cancellationToken;
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "TypedCredentialMountAsyncRejectsFlavorMismatch",
                        displayName: "OpenNfsClient.MountAsync(string, OpenNfsClientCredential, ...) rejects a credential whose flavor does not match the builder configuration with a typed Unsupported failure",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteTypedCredentialMountAsyncRejectsFlavorMismatchAsync),

            };
        }
    }
}
