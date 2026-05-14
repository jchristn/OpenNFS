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
    /// Client builder, compatibility, authsys, README, duplicate-endpoint, and lifetime suites.
    /// </summary>
    internal static class ClientSurfaceBuilderAndShapeCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "BuilderCapturesTransportEndpointsAndRetry",
                        displayName: "Client builder captures transport, endpoint, timeout, and retry settings",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            OpenNfsRetryPolicy retryPolicy = new OpenNfsRetryPolicy(
                                maximumAttempts: 5,
                                initialDelay: TimeSpan.FromMilliseconds(200),
                                maximumDelay: TimeSpan.FromSeconds(3),
                                useExponentialBackoff: true);

                            OpenNfsClientSettings settings = new OpenNfsClientBuilder()
                                .WithServer("primary.example", 3049)
                                .WithMountEndpoint("mount.example", 20048)
                                .AddAlternateEndpoint("failover-a.example", 4049)
                                .AddAlternateEndpoint("failover-b.example", 5049)
                                .WithEndpointSelectionMode(OpenNfsEndpointSelectionMode.SequentialFailover)
                                .WithTransportPolicy(OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3)
                                .WithConnectionTimeout(TimeSpan.FromSeconds(12))
                                .WithResponseTimeout(TimeSpan.FromSeconds(45))
                                .WithAuthenticationFlavor(OpenNfsAuthenticationFlavor.RpcSecGss)
                                .WithRetryPolicy(retryPolicy)
                                .BuildSettings();

                            if (!string.Equals(settings.ServerHost, "primary.example", StringComparison.Ordinal)
                                || settings.ServerPort != 3049
                                || !string.Equals(settings.PrimaryEndpoint.Host, "primary.example", StringComparison.Ordinal)
                                || settings.PrimaryEndpoint.Port != 3049
                                || !string.Equals(settings.MountEndpoint.Host, "mount.example", StringComparison.Ordinal)
                                || settings.MountEndpoint.Port != 20048
                                || !settings.HasExplicitMountEndpoint
                                || settings.TransportPolicy != OpenNfsClientTransportPolicy.TcpWithUdpFallbackForNfsV3
                                || !settings.EnableUdpForNfsV3
                                || settings.ConnectionTimeout != TimeSpan.FromSeconds(12)
                                || settings.ResponseTimeout != TimeSpan.FromSeconds(45)
                                || settings.AuthenticationFlavor != OpenNfsAuthenticationFlavor.RpcSecGss
                                || !ReferenceEquals(settings.RetryPolicy, retryPolicy)
                                || settings.EndpointSelectionMode != OpenNfsEndpointSelectionMode.SequentialFailover)
                            {
                                throw new InvalidOperationException("Expected the client builder to round-trip the configured transport, timeout, endpoint, authentication, and retry settings.");
                            }

                            if (settings.AlternateEndpoints.Count != 2
                                || settings.CandidateEndpoints.Count != 3
                                || !string.Equals(settings.CandidateEndpoints[1].Host, "failover-a.example", StringComparison.Ordinal)
                                || settings.CandidateEndpoints[1].Port != 4049
                                || !string.Equals(settings.CandidateEndpoints[2].Host, "failover-b.example", StringComparison.Ordinal)
                                || settings.CandidateEndpoints[2].Port != 5049)
                            {
                                throw new InvalidOperationException("Expected the client settings to preserve alternate endpoints and resolved candidate ordering.");
                            }

                            if (settings.RetryPolicy.GetDelayForRetry(1) != TimeSpan.FromMilliseconds(200)
                                || settings.RetryPolicy.GetDelayForRetry(2) != TimeSpan.FromMilliseconds(400)
                                || settings.RetryPolicy.GetDelayForRetry(4) != TimeSpan.FromSeconds(1.6))
                            {
                                throw new InvalidOperationException("Expected the client retry policy to expose stable exponential backoff timing.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "CompatibilityPrimarySurfaceShapeIsPresent",
                        displayName: "Client compatibility primary surface is present and advanced members are marked secondary",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            MethodInfo? connectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ConnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? tryConnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryConnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? disconnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.DisconnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? tryDisconnectAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryDisconnectAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? openAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.OpenAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? closeAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CloseAsync), new[] { typeof(CancellationToken) });
                            MethodInfo? mountAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.MountAsync), new[] { typeof(string), typeof(CancellationToken) });
                            MethodInfo? tryMountAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.TryMountAsync), new[] { typeof(string), typeof(CancellationToken) });
                            MethodInfo? createMountSessionFromHandle = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CreateMountSession), new[] { typeof(string), typeof(byte[]) });
                            MethodInfo? createMountSessionFromResult = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.CreateMountSession), new[] { typeof(string), typeof(OpenNfsMountV3Result) });
                            MethodInfo? prepareV3ProcedureAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.PrepareV3ProcedureAsync), new[] { typeof(OpenNfsV3ProcedureRequest), typeof(CancellationToken) });
                            MethodInfo? executeV3ProcedureAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ExecuteV3ProcedureAsync), new[] { typeof(OpenNfsV3ProcedureRequest), typeof(OpenNfsOperationIdempotency), typeof(CancellationToken) });
                            MethodInfo? prepareCompoundAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.PrepareCompoundAsync), new[] { typeof(OpenNfsCompoundRequest), typeof(CancellationToken) });
                            MethodInfo? executeCompoundAsync = typeof(OpenNfsClient).GetMethod(nameof(OpenNfsClient.ExecuteCompoundAsync), new[] { typeof(OpenNfsCompoundRequest), typeof(OpenNfsOperationIdempotency), typeof(CancellationToken) });
                            MethodInfo? tryListExportsV3Async = typeof(ExportApis).GetMethod(nameof(ExportApis.TryListExportsV3Async), new[] { typeof(CancellationToken) });

                            if (connectAsync is null
                                || tryConnectAsync is null
                                || disconnectAsync is null
                                || tryDisconnectAsync is null
                                || mountAsync is null
                                || tryMountAsync is null
                                || createMountSessionFromHandle is null
                                || createMountSessionFromResult is null
                                || prepareV3ProcedureAsync is null
                                || executeV3ProcedureAsync is null
                                || prepareCompoundAsync is null
                                || executeCompoundAsync is null
                                || tryListExportsV3Async is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Files)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Directories)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Metadata)) is null
                                || typeof(OpenNfsMountSession).GetProperty(nameof(OpenNfsMountSession.Locks)) is null)
                            {
                                throw new InvalidOperationException("Expected the aligned compatibility primary surface to expose ConnectAsync, DisconnectAsync, TryConnectAsync, TryDisconnectAsync, MountAsync, TryMountAsync, export bootstrap Try APIs, OpenNfsMountSession, and its grouped members.");
                            }

                            AssertEditorBrowsableState(openAsync, EditorBrowsableState.Never, nameof(OpenNfsClient.OpenAsync));
                            AssertEditorBrowsableState(closeAsync, EditorBrowsableState.Never, nameof(OpenNfsClient.CloseAsync));
                            AssertEditorBrowsableState(createMountSessionFromHandle, EditorBrowsableState.Advanced, "CreateMountSession(string, byte[])");
                            AssertEditorBrowsableState(createMountSessionFromResult, EditorBrowsableState.Advanced, "CreateMountSession(string, OpenNfsMountV3Result)");
                            AssertEditorBrowsableState(prepareV3ProcedureAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.PrepareV3ProcedureAsync));
                            AssertEditorBrowsableState(executeV3ProcedureAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.ExecuteV3ProcedureAsync));
                            AssertEditorBrowsableState(prepareCompoundAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.PrepareCompoundAsync));
                            AssertEditorBrowsableState(executeCompoundAsync, EditorBrowsableState.Advanced, nameof(OpenNfsClient.ExecuteCompoundAsync));

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "BuilderCapturesAuthSysCredentialsAndMountTrafficUsesThem",
                        displayName: "Client builder captures AUTH_SYS credentials and mount traffic uses the configured machine name",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteBuilderCapturesAuthSysCredentialsAndMountTrafficUsesThemAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "ReadmeClientSnippetCompilesFromCleanConsumerApp",
                        displayName: "The canonical README client snippet compiles from a clean packaged consumer app",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: ExecuteReadmeClientSnippetCompilesFromCleanConsumerAppAsync),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "DuplicateAlternateEndpointsAreRejected",
                        displayName: "Client settings reject duplicate alternate endpoints",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: cancellationToken =>
                        {
                            try
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                OpenNfsClientSettings settings = new OpenNfsClientBuilder()
                                    .WithPrimaryEndpoint("dup.example", 2049)
                                    .AddAlternateEndpoint("dup.example", 2049)
                                    .BuildSettings();

                                if (settings.ServerPort == 0)
                                {
                                    throw new InvalidOperationException("The duplicate-endpoint validation path unexpectedly returned a settings object.");
                                }

                                throw new InvalidOperationException("Expected duplicate alternate endpoints to be rejected.");
                            }
                            catch (ArgumentException exception)
                            {
                                if (!exception.Message.Contains("alternate endpoints", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected duplicate endpoint validation to explain the alternate-endpoint constraint.");
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "ClientSurfaceSuites",
                        caseId: "LifetimeOpenCloseAndDisposeBehavePredictably",
                        displayName: "Client lifetime honors cancellation, close, and disposal",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async _ =>
                        {
                            OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithServer("lifetime.example", 2049)
                                .Build();

                            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                            cancellationTokenSource.Cancel();

                            try
                            {
                                await client.ConnectAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected client connect to honor cancellation before state changes.");
                            }
                            catch (OperationCanceledException)
                            {
                                if (client.State != OpenNfsClientState.Created)
                                {
                                    throw new InvalidOperationException("Expected canceled client connect attempts to leave the client in the created state.");
                                }
                            }

                            await client.ConnectAsync(CancellationToken.None).ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Open || client.LifetimeCancellationToken.IsCancellationRequested)
                            {
                                throw new InvalidOperationException("Expected a successfully connected client to enter the open state without canceling its lifetime token.");
                            }

                            await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Closed || !client.LifetimeCancellationToken.IsCancellationRequested)
                            {
                                throw new InvalidOperationException("Expected client disconnect to transition to the closed state and cancel the lifetime token.");
                            }

                            try
                            {
                                await client.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                                throw new InvalidOperationException("Expected a closed client not to reopen.");
                            }
                            catch (InvalidOperationException exception)
                            {
                                if (!exception.Message.Contains("cannot be reopened", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected the closed-client reopen failure to explain the lifetime constraint.");
                                }
                            }

                            await client.DisposeAsync().ConfigureAwait(false);

                            if (client.State != OpenNfsClientState.Disposed)
                            {
                                throw new InvalidOperationException("Expected asynchronous disposal to transition the client to the disposed state.");
                            }
                        }),

            };
        }
    }
}
