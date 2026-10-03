namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nsm.Callbacks;
    using OpenNFS.Protocol.V3.Telemetry;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Security.RpcSecGss;
    using OpenNFS.Rpc.Telemetry;
    using OpenNFS.Telemetry;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Telemetry cases for failure paths: exceptions, RPC-level errors, authentication rejections, server faults,
    /// denied mounts, RPCSEC_GSS rejections, backend failures, and server-originated callback failures.
    /// </summary>
    internal static class TelemetryFailureCases
    {
        private const uint NfsProgram = 100003U;
        private const uint MountProgram = 100005U;

        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "RpcFailuresAreClassifiedByOutcomeAndErrorType",
                    displayName: "Server RPC exceptions, RPC errors, auth rejections, server faults, and denied mounts are classified",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();

                        Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> throwing =
                            OpenNfsServerInstrumentation.InstrumentHandler((request, token) => throw new InvalidOperationException("simulated handler failure"));
                        try
                        {
                            _ = await throwing(CreateCall(0x7E300001, NfsProgram, 3U, 6U), cancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException("The instrumented handler must rethrow the original exception.");
                        }
                        catch (InvalidOperationException exception) when (exception.Message == "simulated handler failure")
                        {
                        }

                        await InvokeAsync(
                            CreateCall(0x7E300002, NfsProgram, 3U, 99U),
                            request => RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.PROC_UNAVAIL),
                            cancellationToken).ConfigureAwait(false);
                        await InvokeAsync(
                            CreateCall(0x7E300003, NfsProgram, 3U, 1U),
                            request => RpcMessageFactory.CreateRejectedReply(request.Header.xid, reject_stat.AUTH_ERROR, authenticationStatus: auth_stat.AUTH_BADCRED),
                            cancellationToken).ConfigureAwait(false);
                        await InvokeAsync(
                            CreateCall(0x7E300004, NfsProgram, 3U, 6U),
                            request => RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.SUCCESS, procedurePayload: new byte[] { 0, 0, 0, 5, 0, 0, 0, 0 }),
                            cancellationToken).ConfigureAwait(false);
                        await InvokeAsync(
                            CreateCall(0x7E300005, MountProgram, 3U, 1U),
                            request => RpcMessageFactory.CreateAcceptedReply(request.Header.xid, accept_stat.SUCCESS, procedurePayload: new byte[] { 0, 0, 0, 13 }),
                            cancellationToken).ConfigureAwait(false);

                        string rpcDuration = OpenNfsTelemetryNames.ServerRpcDuration;
                        capture.Require(rpcDuration, "a handler exception is an exception outcome", OpenNfsTelemetryNames.AttributeRpcMethod, "READ", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException, OpenNfsTelemetryNames.AttributeErrorType, "System.InvalidOperationException");
                        capture.Require(rpcDuration, "an unknown procedure maps to the bounded unknown method", OpenNfsTelemetryNames.AttributeRpcMethod, OpenNfsTelemetryNames.ValueUnknown, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeRpcError, OpenNfsTelemetryNames.AttributeErrorType, "PROC_UNAVAIL");
                        capture.Require(rpcDuration, "an AUTH_ERROR rejection is an RPC error", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeRpcError, OpenNfsTelemetryNames.AttributeErrorType, "AUTH_ERROR", OpenNfsTelemetryNames.AttributeStatus, "AUTH_BADCRED");
                        capture.Require(rpcDuration, "NFS3ERR_IO is a server fault", OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeServerError, OpenNfsTelemetryNames.AttributeStatus, "NFS3ERR_IO");
                        capture.Require(OpenNfsTelemetryNames.ServerAuthRequests, "rejected credentials are counted", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultRejected);
                        capture.Require(OpenNfsTelemetryNames.ServerMountRequests, "MNT3ERR_ACCES is a denied mount", OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultDenied, OpenNfsTelemetryNames.AttributeStatus, "MNT3ERR_ACCES");

                        Activity failed = capture.Activities.First(activity => activity.DisplayName == "nfs READ" && Equals(activity.GetTagItem(OpenNfsTelemetryNames.AttributeErrorType), "System.InvalidOperationException"));
                        if (failed.Status != ActivityStatusCode.Error || !failed.Events.Any(activityEvent => activityEvent.Name == "exception"))
                        {
                            throw new InvalidOperationException("Expected the failed server span to have an Error status and an exception event.");
                        }

                        if (failed.Events.SelectMany(activityEvent => activityEvent.Tags).Any(tag => Equals(tag.Value, "simulated handler failure")))
                        {
                            throw new InvalidOperationException("Exception messages must never be copied onto spans.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "RpcSecGssRejectionsAreCounted",
                    displayName: "RPCSEC_GSS credential rejections are counted and traced as an auth stage",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();
                        RpcSecGssAuthenticator authenticator = new RpcSecGssAuthenticator(
                            contextStore: new RpcSecGssInMemoryContextStore(),
                            isMechanismRegistered: false);
                        RpcMessageEnvelope call = RpcMessageFactory.CreateCall(
                            0x7E310001,
                            NfsProgram,
                            3U,
                            1U,
                            credential: new opaque_auth { flavor = auth_flavor.RPCSEC_GSS, body = new byte[] { 1, 2, 3 } });

                        RpcSecGssCallDisposition disposition = await RpcSecGssCallProcessor.ProcessAsync(call, authenticator, mechanism: null, cancellationToken).ConfigureAwait(false);
                        if (disposition.ContinueProcessing)
                        {
                            throw new InvalidOperationException("A malformed RPCSEC_GSS credential must be rejected.");
                        }

                        capture.Require(OpenNfsTelemetryNames.ServerRpcSecGssCalls, "malformed GSS credentials are rejected", OpenNfsTelemetryNames.AttributeGssProcedure, OpenNfsTelemetryNames.GssProcedureControl, OpenNfsTelemetryNames.AttributeResult, OpenNfsTelemetryNames.ResultRejected);
                        Activity stage = capture.RequireActivity("stage:" + OpenNfsTelemetryNames.StageAuth, ActivityKind.Internal, "RPCSEC_GSS evaluation is a traced stage");
                        if (stage.Status != ActivityStatusCode.Error)
                        {
                            throw new InvalidOperationException("Expected the rejected auth stage span to carry an Error status.");
                        }
                    }),

                new TestCaseDescriptor(
                    suiteId: "TelemetrySuites",
                    caseId: "BackendAndCallbackFailuresAreRecorded",
                    displayName: "Failed backend calls and server-originated callbacks record their outcome and error type",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: async cancellationToken =>
                    {
                        using TelemetryCapture capture = new TelemetryCapture();

                        try
                        {
                            _ = await OpenNfsServerInstrumentation.TrackBackendAsync<string, string, string>(
                                OpenNfsTelemetryNames.CapabilityFileSystem,
                                "read_file",
                                "host",
                                "request",
                                (host, request) => Task.FromException<string>(new IOException("disk gone"))).ConfigureAwait(false);
                            throw new InvalidOperationException("The tracked backend call must rethrow the backend failure.");
                        }
                        catch (IOException)
                        {
                        }

                        capture.Require(OpenNfsTelemetryNames.ServerBackendDuration, "backend failures are measured", OpenNfsTelemetryNames.AttributeCapability, OpenNfsTelemetryNames.CapabilityFileSystem, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException, OpenNfsTelemetryNames.AttributeErrorType, "System.IO.IOException");
                        if (capture.RequireActivity("filesystem read_file", ActivityKind.Internal, "backend calls are traced").Status != ActivityStatusCode.Error)
                        {
                            throw new InvalidOperationException("Expected the failed backend span to carry an Error status.");
                        }

                        NlmV4GrantedCallback grantedCallback = new NlmV4GrantedCallback("client", new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }, 42, 0UL, 10UL, exclusive: true);
                        TelemetryNlmV4GrantedCallbackDispatcher granted = new TelemetryNlmV4GrantedCallbackDispatcher(
                            new RecordingNlmV4GrantedCallbackDispatcher((callback, token) => Task.FromResult(NlmV4GrantedCallbackStatus.Granted)));
                        _ = await granted.DispatchGrantedAsync(grantedCallback, cancellationToken).ConfigureAwait(false);
                        TelemetryNlmV4GrantedCallbackDispatcher failing = new TelemetryNlmV4GrantedCallbackDispatcher(
                            new RecordingNlmV4GrantedCallbackDispatcher((callback, token) => Task.FromException<NlmV4GrantedCallbackStatus>(new IOException("client unreachable"))));
                        try
                        {
                            _ = await failing.DispatchGrantedAsync(grantedCallback, cancellationToken).ConfigureAwait(false);
                            throw new InvalidOperationException("A failing GRANTED callback must propagate.");
                        }
                        catch (IOException)
                        {
                        }

                        TelemetryNsmNotificationDispatcher notify = new TelemetryNsmNotificationDispatcher(new RecordingNsmNotificationDispatcher());
                        await notify.DispatchAsync(new NsmNotificationCallback("127.0.0.1", 100021, 4, 16, "client", 3, new byte[16]), cancellationToken).ConfigureAwait(false);

                        capture.Require(OpenNfsTelemetryNames.ServerCallbackDuration, "successful GRANTED callbacks are measured", OpenNfsTelemetryNames.AttributeCallback, OpenNfsTelemetryNames.CallbackNlmGranted, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.Require(OpenNfsTelemetryNames.ServerCallbackDuration, "failed GRANTED callbacks are measured", OpenNfsTelemetryNames.AttributeCallback, OpenNfsTelemetryNames.CallbackNlmGranted, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeException, OpenNfsTelemetryNames.AttributeErrorType, "System.IO.IOException");
                        capture.Require(OpenNfsTelemetryNames.ServerCallbackDuration, "NSM notifications are measured", OpenNfsTelemetryNames.AttributeCallback, OpenNfsTelemetryNames.CallbackNsmNotify, OpenNfsTelemetryNames.AttributeOutcome, OpenNfsTelemetryNames.OutcomeSuccess);
                        capture.RequireActivity("callback " + OpenNfsTelemetryNames.CallbackNlmGranted, ActivityKind.Client, "server-originated callbacks are client spans");
                    }),
            };
        }

        private static RpcMessageEnvelope CreateCall(uint xid, uint program, uint version, uint procedure)
        {
            return RpcMessageFactory.CreateCall(xid, program, version, procedure, procedurePayload: new byte[] { 0, 0, 0, 0 });
        }

        private static async Task InvokeAsync(
            RpcMessageEnvelope request,
            Func<RpcMessageEnvelope, RpcMessageEnvelope> buildReply,
            CancellationToken cancellationToken)
        {
            Func<RpcMessageEnvelope, CancellationToken, Task<RpcMessageEnvelope>> handler =
                OpenNfsServerInstrumentation.InstrumentHandler((message, token) => Task.FromResult(buildReply(message)), "127.0.0.1:700");
            _ = await handler(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
