namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases that drive a real in-process server and client over loopback TCP and assert the server RPC
    /// pipeline, backends, connections, lifecycle, and client spans and metrics they produce.
    /// </summary>
    internal static class TelemetryServerCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "ServerRoundTripEmitsRpcBackendAndConnectionTelemetry",
                    displayName: "A mounted-session round trip emits server RPC, stage, backend, auth, mount, connection, and lifecycle telemetry",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        byte[] payload = Encoding.UTF8.GetBytes("telemetry-round-trip-payload");

                        await using (EphemeralOpenNfsServer server = await EphemeralOpenNfsServer.StartAsync(cancellationToken).ConfigureAwait(false))
                        {
                            (OpenNfsClient client, OpenNfsMountSession session) = await server.ConnectAndMountAsync(cancellationToken).ConfigureAwait(false);
                            await using (client)
                            await using (session)
                            {
                                await session.Directories.CreateDirectoryAsync("/telemetry", createParents: true, cancellationToken).ConfigureAwait(false);
                                await session.Files.WriteAllBytesAsync("/telemetry/data.bin", payload, OpenNfsWriteStability.FileSync, cancellationToken).ConfigureAwait(false);
                                byte[] readBack = await session.Files.ReadAllBytesAsync("/telemetry/data.bin", cancellationToken).ConfigureAwait(false);
                                if (!readBack.SequenceEqual(payload))
                                {
                                    throw new InvalidOperationException("The instrumented round trip must return the written payload unchanged.");
                                }

                                try
                                {
                                    _ = await session.Files.ReadAllBytesAsync("/telemetry/missing.bin", cancellationToken).ConfigureAwait(false);
                                    throw new InvalidOperationException("Reading a missing file must fail.");
                                }
                                catch (OpenNfsV3StatusException)
                                {
                                }
                            }

                            capture.RecordObservableInstruments();
                            if (!capture.Require(OpenNfsTelemetryNames.ServerUp, "a running application reports itself up").Any(measurement => measurement.Value >= 1))
                            {
                                throw new InvalidOperationException("Expected opennfs.server.up to be at least 1 while the application runs.");
                            }

                            capture.Require(OpenNfsTelemetryNames.ServerConfigMaximumConnections, "the configured connection capacity is exported as a gauge");
                        }

                        string rpcDuration = OpenNfsTelemetryNames.ServerRpcDuration;
                        capture.Require(rpcDuration, "MNT succeeds", OpenNfsTelemetryNames.AttributeRpcService, "mount", OpenNfsTelemetryNames.AttributeRpcMethod, "MNT", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.AttributeStatus, "MNT3_OK");
                        capture.Require(rpcDuration, "WRITE succeeds", OpenNfsTelemetryNames.AttributeRpcService, "nfs", OpenNfsTelemetryNames.AttributeRpcMethod, "WRITE", OpenNfsTelemetryNames.AttributeRpcVersion, "3", OpenNfsTelemetryNames.AttributeStatus, "NFS3_OK");
                        capture.Require(rpcDuration, "READ succeeds", OpenNfsTelemetryNames.AttributeRpcMethod, "READ", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(rpcDuration, "a LOOKUP of a missing name is an NFS status error, not a failure", OpenNfsTelemetryNames.AttributeRpcMethod, "LOOKUP", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeNfsError, OpenNfsTelemetryNames.AttributeStatus, "NFS3ERR_NOENT", OpenNfsTelemetryNames.AttributeErrorType, "NFS3ERR_NOENT");

                        capture.Require(OpenNfsTelemetryNames.ServerRpcStageDuration, "the NFSv3 handler stage is timed", OpenNfsTelemetryNames.AttributeStage, OpenNfsTelemetryNames.StageExecute);
                        capture.Require(OpenNfsTelemetryNames.ServerRpcStageDuration, "the duplicate-request lookup is timed", OpenNfsTelemetryNames.AttributeStage, OpenNfsTelemetryNames.StageReplayCache);
                        capture.Require(OpenNfsTelemetryNames.ServerRpcStageDuration, "the reply write is timed", OpenNfsTelemetryNames.AttributeStage, OpenNfsTelemetryNames.StageSend);
                        capture.Require(OpenNfsTelemetryNames.ServerRpcRequestSize, "argument payload sizes are recorded", OpenNfsTelemetryNames.AttributeRpcMethod, "WRITE");
                        capture.Require(OpenNfsTelemetryNames.ServerRpcResponseSize, "result payload sizes are recorded", OpenNfsTelemetryNames.AttributeRpcMethod, "READ");
                        capture.Require(OpenNfsTelemetryNames.ServerRpcActive, "in-flight calls are tracked", OpenNfsTelemetryNames.AttributeRpcService, "nfs");
                        capture.Require(OpenNfsTelemetryNames.ServerAuthRequests, "credentials are counted by flavor", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultAccepted);
                        capture.Require(OpenNfsTelemetryNames.ServerMountRequests, "the mount decision is counted", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultGranted);
                        capture.Require(OpenNfsTelemetryNames.ServerReplayCacheLookups, "the NFSv3 duplicate-request cache records misses", OpenNfsTelemetryNames.AttributeCache, OpenNfsTelemetryNames.CacheNfs3DuplicateRequest, OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultMiss);
                        capture.Require(OpenNfsTelemetryNames.ServerReplayCacheEntries, "stored replies grow the cache", OpenNfsTelemetryNames.AttributeCache, OpenNfsTelemetryNames.CacheNfs3DuplicateRequest);

                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "file writes go through the backend", OpenNfsTelemetryNames.AttributeCapability, OpenNfsTelemetryNames.CapabilityFileSystem, OpenNfsTelemetryNames.AttributeBackendOperation, "write_file", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "file reads go through the backend", OpenNfsTelemetryNames.AttributeBackendOperation, "read_file");
                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "file handles are resolved through the provider", OpenNfsTelemetryNames.AttributeCapability, OpenNfsTelemetryNames.CapabilityFileHandles, OpenNfsTelemetryNames.AttributeBackendOperation, "resolve");
                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "mounts consult the authorization backend", OpenNfsTelemetryNames.AttributeCapability, OpenNfsTelemetryNames.CapabilityMountAuthorization);
                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "mounts enumerate exports", OpenNfsTelemetryNames.AttributeCapability, OpenNfsTelemetryNames.CapabilityExports);
                        if (capture.Require(OpenNfsTelemetryNames.ServerIoBytes, "written bytes are counted", OpenNfsTelemetryNames.AttributeDirection, OpenNfsTelemetryNames.DirectionWrite).Sum(measurement => measurement.Value) < payload.Length)
                        {
                            throw new InvalidOperationException("Expected the server write byte counter to include the full payload.");
                        }

                        capture.Require(OpenNfsTelemetryNames.ServerIoBytes, "read bytes are counted", OpenNfsTelemetryNames.AttributeDirection, OpenNfsTelemetryNames.DirectionRead);

                        capture.Require(OpenNfsTelemetryNames.ServerConnectionsOpened, "client connections are accepted on the NFS listener", OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerNfsV3);
                        capture.Require(OpenNfsTelemetryNames.ServerConnectionsOpened, "client connections are accepted on the MOUNT listener", OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerMount);
                        await capture.RequireEventuallyAsync(OpenNfsTelemetryNames.ServerConnectionsClosed, "closed connections are counted with a reason", cancellationToken, OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerNfsV3).ConfigureAwait(false);
                        await capture.RequireEventuallyAsync(OpenNfsTelemetryNames.ServerConnectionDuration, "connection lifetimes are recorded", cancellationToken, OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerNfsV3).ConfigureAwait(false);
                        capture.Require(OpenNfsTelemetryNames.ServerListenersActive, "bound listeners are tracked", OpenNfsTelemetryNames.AttributeListener, OpenNfsTelemetryNames.ListenerNfsV3);
                        capture.Require(OpenNfsTelemetryNames.ServerLifecycleEvents, "application start is recorded", OpenNfsTelemetryNames.AttributeEvent, OpenNfsTelemetryNames.EventStarted);
                        capture.Require(OpenNfsTelemetryNames.ServerLifecycleEvents, "application stop is recorded", OpenNfsTelemetryNames.AttributeEvent, OpenNfsTelemetryNames.EventStopped);

                        Activity writeSpan = capture.RequireActivity("nfs WRITE", ActivityKind.Server, "every inbound RPC gets a server span");
                        if (writeSpan.ParentSpanId != default
                            || !Equals(writeSpan.GetTagItem(OpenNfsTelemetryNames.AttributeRpcSystem), OpenNfsTelemetryNames.RpcSystemOncRpc)
                            || !Equals(writeSpan.GetTagItem(OpenNfsTelemetryNames.AttributeStatus), "NFS3_OK")
                            || writeSpan.GetTagItem(OpenNfsTelemetryNames.AttributeClientAddress) is null
                            || writeSpan.Status != ActivityStatusCode.Ok)
                        {
                            throw new InvalidOperationException("Expected the server WRITE span to be a root span with RPC, status, and peer attributes and an OK status.");
                        }

                        bool backendChild = capture.Activities.Any(activity =>
                            activity.DisplayName == "filesystem write_file"
                            && activity.TraceId == writeSpan.TraceId
                            && activity.ParentSpanId == writeSpan.SpanId);
                        if (!backendChild)
                        {
                            throw new InvalidOperationException("Expected the file-system backend span to nest under the server WRITE span.");
                        }

                        Activity lookupSpan = capture.Activities.First(activity => activity.DisplayName == "nfs LOOKUP" && Equals(activity.GetTagItem(OpenNfsTelemetryNames.AttributeStatus), "NFS3ERR_NOENT"));
                        if (lookupSpan.Status == ActivityStatusCode.Error)
                        {
                            throw new InvalidOperationException("An ordinary NFS status error (NOENT) must not mark the server span as failed.");
                        }
                    }),
            };
        }
    }
}
