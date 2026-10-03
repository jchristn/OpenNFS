namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases for OpenNFS.Client: mounted-session operations, logical RPC calls, transport attempts, the
    /// pooled connection lifecycle, retries, and connect failures.
    /// </summary>
    internal static class TelemetryClientCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "ClientSessionPoolAndRpcTelemetry",
                    displayName: "Client mounted-session operations nest RPC spans and emit pool, attempt, and byte metrics",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        byte[] payload = Encoding.UTF8.GetBytes("client-telemetry-payload");

                        await using (EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false))
                        {
                            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
                            await using (client)
                            await using (session)
                            {
                                await session.Files.WriteAllBytesAsync("/client.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                                _ = await session.Files.ReadAllBytesAsync("/client.bin", cancellationToken).ConfigureAwait(false);
                                try
                                {
                                    _ = await session.Files.ReadAllBytesAsync("/absent.bin", cancellationToken).ConfigureAwait(false);
                                }
                                catch (OpenNfsV3StatusException)
                                {
                                }

                                capture.RecordObservableInstruments();
                                capture.Require(OpenNfsTelemetryNames.ClientPoolMaxConnectionsPerEndpoint, "pool capacity is exported as a gauge");
                            }

                            await capture.RequireEventuallyAsync(OpenNfsTelemetryNames.ClientPoolConnectionsClosed, "disposing the client closes its pooled connections", cancellationToken, OpenNfsTelemetryNames.AttributeReason, OpenNfsTelemetryNames.ReasonDisposed).ConfigureAwait(false);
                        }

                        string sessionDuration = OpenNfsTelemetryNames.ClientSessionOperationDuration;
                        capture.Require(sessionDuration, "ConnectAsync is a session operation", OpenNfsTelemetryNames.AttributeOperation, "ConnectAsync", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(sessionDuration, "MountAsync is a session operation", OpenNfsTelemetryNames.AttributeOperation, "MountAsync", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(sessionDuration, "whole-file writes are measured", OpenNfsTelemetryNames.AttributeOperation, "WriteAllBytesAsync", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(sessionDuration, "a failed read records the exception type", OpenNfsTelemetryNames.AttributeOperation, "ReadAllBytesAsync", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException, OpenNfsTelemetryNames.AttributeErrorType, typeof(OpenNfsV3StatusException).FullName!);
                        if (capture.Require(OpenNfsTelemetryNames.ClientIoBytes, "written bytes are counted", OpenNfsTelemetryNames.AttributeDirection, OpenNfsTelemetryNames.DirectionWrite).Sum(measurement => measurement.Value) < payload.Length)
                        {
                            throw new InvalidOperationException("Expected the client write byte counter to include the payload.");
                        }

                        capture.Require(OpenNfsTelemetryNames.ClientIoBytes, "read bytes are counted", OpenNfsTelemetryNames.AttributeDirection, OpenNfsTelemetryNames.DirectionRead);
                        capture.Require(OpenNfsTelemetryNames.ClientRpcDuration, "logical WRITE calls are measured", OpenNfsTelemetryNames.AttributeOperation, "NFSv3 WRITE", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(OpenNfsTelemetryNames.ClientRpcDuration, "logical MNT calls are measured", OpenNfsTelemetryNames.AttributeOperation, "MOUNT v3 MNT");
                        capture.Require(OpenNfsTelemetryNames.ClientRpcAttemptDuration, "TCP attempts are measured", OpenNfsTelemetryNames.AttributeNetworkTransport, OpenNfsTelemetryNames.TransportTcp, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolConnectionsOpened, "pooled connections open", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultSuccess);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolConnectDuration, "TCP connect time is measured", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultSuccess);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolAcquireDuration, "the first call creates a connection", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultCreated);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolAcquireDuration, "later calls reuse pooled connections", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultReused);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolPendingCalls, "in-flight calls are tracked");
                        capture.Require(OpenNfsTelemetryNames.ClientPoolConnections, "open connections are tracked");

                        Activity writeSession = capture.RequireActivity("session WriteAllBytesAsync", ActivityKind.Internal, "mounted-session operations are spans");
                        Activity? writeRpc = capture.Activities.FirstOrDefault(activity =>
                            activity.DisplayName == "NFSv3 WRITE"
                            && activity.Kind == ActivityKind.Client
                            && activity.ParentSpanId == writeSession.SpanId);
                        if (writeRpc is null)
                        {
                            throw new InvalidOperationException("Expected the NFSv3 WRITE client span to nest under the WriteAllBytesAsync session span.");
                        }

                        if (writeRpc.GetTagItem(OpenNfsTelemetryNames.AttributeServerAddress) is null
                            || writeRpc.GetTagItem(OpenNfsTelemetryNames.AttributeServerPort) is null
                            || writeRpc.Status != ActivityStatusCode.Ok)
                        {
                            throw new InvalidOperationException("Expected the client RPC span to carry server.address and server.port and an OK status.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "ClientRetriesAreCountedAndTraced",
                    displayName: "Idempotent client retries are counted and recorded as span events; exhausted calls record failure",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        OpenNfsTransportPipeline pipeline = new OpenNfsTransportPipeline((delay, token) => Task.CompletedTask);
                        int attempts = 0;
                        string reply = await pipeline.ExecuteAsync(
                            CreatePipelineRequest("Telemetry READ", OpenNfsTransportPipelineIdempotency.Idempotent),
                            (attempt, token) =>
                            {
                                attempts++;
                                return attempt.AttemptNumber == 1
                                    ? Task.FromException<string>(new IOException("transient"))
                                    : Task.FromResult("ok");
                            },
                            validateReply: null,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (reply != "ok" || attempts != 2)
                        {
                            throw new InvalidOperationException("Expected one retry before success.");
                        }

                        try
                        {
                            _ = await pipeline.ExecuteAsync<string>(
                                CreatePipelineRequest("Telemetry WRITE", OpenNfsTransportPipelineIdempotency.NonIdempotent),
                                (attempt, token) => Task.FromException<string>(new IOException("connection reset")),
                                validateReply: null,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException("A non-idempotent failure must not be retried or swallowed.");
                        }
                        catch (IOException)
                        {
                        }

                        capture.Require(OpenNfsTelemetryNames.ClientRpcRetries, "the transient failure was retried", OpenNfsTelemetryNames.AttributeOperation, "Telemetry READ", OpenNfsTelemetryNames.AttributeErrorType, "System.IO.IOException");
                        capture.Require(OpenNfsTelemetryNames.ClientRpcDuration, "the retried call still succeeded", OpenNfsTelemetryNames.AttributeOperation, "Telemetry READ", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(OpenNfsTelemetryNames.ClientRpcDuration, "the non-idempotent call failed", OpenNfsTelemetryNames.AttributeOperation, "Telemetry WRITE", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException, OpenNfsTelemetryNames.AttributeErrorType, "System.IO.IOException");
                        if (capture.Measurements(OpenNfsTelemetryNames.ClientRpcRetries).Any(measurement => measurement.Matches(new[] { OpenNfsTelemetryNames.AttributeOperation, "Telemetry WRITE" })))
                        {
                            throw new InvalidOperationException("Non-idempotent calls must not report retries.");
                        }

                        Activity retried = capture.RequireActivity("Telemetry READ", ActivityKind.Client, "logical calls are client spans");
                        if (!retried.Events.Any(activityEvent => activityEvent.Name == "retry") || retried.Status != ActivityStatusCode.Ok)
                        {
                            throw new InvalidOperationException("Expected the retried call span to carry a retry event and end OK.");
                        }

                        if (capture.RequireActivity("Telemetry WRITE", ActivityKind.Client, "failed calls are client spans").Status != ActivityStatusCode.Error)
                        {
                            throw new InvalidOperationException("Expected the failed call span to carry an Error status.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "ClientConnectFailuresAreRecorded",
                    displayName: "A refused TCP connect is recorded as a failed pooled connection, a failed attempt, and a failed mount",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        int closedPort = ReserveClosedPort();
                        await using OpenNfsClient client = new OpenNfsClientBuilder()
                            .WithPrimaryEndpoint("127.0.0.1", closedPort)
                            .WithMountEndpoint("127.0.0.1", closedPort)
                            .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                            .WithResponseTimeout(TimeSpan.FromSeconds(2))
                            .WithRetryPolicy(new OpenNfsRetryPolicy(maximumAttempts: 1))
                            .Build();
                        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                        try
                        {
                            await using OpenNfsMountSession session = await client.MountAsync("/export", cancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException("Mounting against a closed port must fail.");
                        }
                        catch (OpenNfsClientException)
                        {
                        }

                        capture.Require(OpenNfsTelemetryNames.ClientPoolConnectionsOpened, "the refused connect is counted", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultFailure);
                        capture.Require(OpenNfsTelemetryNames.ClientPoolAcquireDuration, "no connection could be acquired", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultFailed);
                        capture.Require(OpenNfsTelemetryNames.ClientRpcAttemptDuration, "the attempt failed", OpenNfsTelemetryNames.AttributeNetworkTransport, OpenNfsTelemetryNames.TransportTcp, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException);
                        capture.Require(OpenNfsTelemetryNames.ClientSessionOperationDuration, "the mount failed", OpenNfsTelemetryNames.AttributeOperation, "MountAsync", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException);
                    }),
            };
        }

        private static OpenNfsTransportPipelineRequest CreatePipelineRequest(string operationName, OpenNfsTransportPipelineIdempotency idempotency)
        {
            return new OpenNfsTransportPipelineRequest(
                operationName: operationName,
                candidateEndpoints: new[] { new OpenNfsEndpoint("telemetry.example", 2049) },
                connectionTimeout: TimeSpan.FromSeconds(5),
                responseTimeout: TimeSpan.FromSeconds(5),
                retryPolicy: new OpenNfsRetryPolicy(maximumAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1), maximumDelay: TimeSpan.FromMilliseconds(1), useExponentialBackoff: false),
                idempotency: idempotency);
        }

        private static int ReserveClosedPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
