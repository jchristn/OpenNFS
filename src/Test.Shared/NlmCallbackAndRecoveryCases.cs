namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.NlmSuiteSupport;

    /// <summary>
    /// Granted callback and reclaim-after-restart NLM v4 suites.
    /// </summary>
    internal static class NlmCallbackAndRecoveryCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GrantedCallbackFlowPositive",
                        displayName: "NLM v4 message procedures trigger granted callbacks and promote the waiter on success",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            RecordingNlmV4GrantedCallbackDispatcher dispatcher = new RecordingNlmV4GrantedCallbackDispatcher(
                                static (_, _) => Task.FromResult(NlmV4GrantedCallbackStatus.Granted));
                            NlmV4Service service = new NlmV4Service(server, dispatcher);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);
                            byte[] waiterOwnerHandle = new byte[] { 0x9C };

                            RpcMessageEnvelope holderLockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2301,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_MSG,
                                        CreateLockArguments(
                                            new byte[] { 0x60 },
                                            fileHandle,
                                            new byte[] { 0x8B },
                                            "holder",
                                            91,
                                            16,
                                            24,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(holderLockReply), "NLM v4 LOCK_MSG");

                            RpcMessageEnvelope waiterLockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2302,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_MSG,
                                        CreateLockArguments(
                                            new byte[] { 0x61 },
                                            fileHandle,
                                            waiterOwnerHandle,
                                            "waiter",
                                            92,
                                            16,
                                            24,
                                            block: true,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(waiterLockReply), "NLM v4 LOCK_MSG");

                            RpcMessageEnvelope unlockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2303,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK_MSG,
                                        CreateUnlockArguments(
                                            new byte[] { 0x60 },
                                            fileHandle,
                                            new byte[] { 0x8B },
                                            "holder",
                                            91,
                                            16,
                                            24)),
                                    cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(unlockReply), "NLM v4 UNLOCK_MSG");

                            if (dispatcher.Callbacks.Count != 1)
                            {
                                throw new InvalidOperationException("Expected one callback from the message-based waiter wakeup path.");
                            }

                            RpcMessageEnvelope testReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2304,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x62 },
                                            fileHandle,
                                            new byte[] { 0xAD },
                                            "third",
                                            93,
                                            16,
                                            24,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult testResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(testReply));
                            if (testResult.Status != OpenNfsNlmV4Status.Denied
                                || testResult.ConflictingHolder is null
                                || !testResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(waiterOwnerHandle))
                            {
                                throw new InvalidOperationException("Expected the message-path waiter to hold the lock after callback grant.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GrantedCallbackFlowNegative",
                        displayName: "NLM v4 drops queued waiters when the granted callback is denied",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            RecordingNlmV4GrantedCallbackDispatcher dispatcher = new RecordingNlmV4GrantedCallbackDispatcher(
                                static (_, _) => Task.FromResult(NlmV4GrantedCallbackStatus.Denied));
                            NlmV4Service service = new NlmV4Service(server, dispatcher);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);
                            byte[] waiterCookie = new byte[] { 0x71 };
                            byte[] waiterOwnerHandle = new byte[] { 0x7C };

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2401,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_MSG,
                                    CreateLockArguments(
                                        new byte[] { 0x70 },
                                        fileHandle,
                                        new byte[] { 0x6B },
                                        "holder",
                                        101,
                                        32,
                                        12,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2402,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK_MSG,
                                    CreateLockArguments(
                                        waiterCookie,
                                        fileHandle,
                                        waiterOwnerHandle,
                                        "waiter",
                                        102,
                                        32,
                                        12,
                                        block: true,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2403,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK_MSG,
                                    CreateUnlockArguments(
                                        new byte[] { 0x70 },
                                        fileHandle,
                                        new byte[] { 0x6B },
                                        "holder",
                                        101,
                                        32,
                                        12)),
                                cancellationToken).ConfigureAwait(false);

                            if (dispatcher.Callbacks.Count != 1)
                            {
                                throw new InvalidOperationException("Expected one denied callback attempt for the queued waiter.");
                            }

                            RpcMessageEnvelope testReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2404,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x72 },
                                            fileHandle,
                                            new byte[] { 0x8D },
                                            "third",
                                            103,
                                            32,
                                            12,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult testResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(testReply));
                            if (!testResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the denied callback to leave the range unlocked for the next caller.");
                            }

                            RpcMessageEnvelope cancelReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2405,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL,
                                        CreateCancelArguments(
                                            waiterCookie,
                                            fileHandle,
                                            waiterOwnerHandle,
                                            "waiter",
                                            102,
                                            32,
                                            12,
                                            block: true,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result cancelResult = new OpenNfsClientBuilder().Build().Locks.ReadCancelV4Result(RpcMessageCodec.Encode(cancelReply));
                            if (cancelResult.Status != OpenNfsNlmV4Status.Denied)
                            {
                                throw new InvalidOperationException("Expected the denied callback to drop the queued waiter before CANCEL.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "ReclaimAfterServerRestartPositive",
                        displayName: "NLM v4 reclaims locks during the restart grace period and blocks new non-reclaim callers",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero));
                            NsmRecoveryCoordinator coordinator = new NsmRecoveryCoordinator(TimeSpan.FromMinutes(5), clock.UtcNow);
                            OpenNfsServer originalServer = CreateLockingServer();
                            NlmV4Service originalService = new NlmV4Service(originalServer, recoveryCoordinator: coordinator);
                            byte[] fileHandle = await CreateFileHandleAsync(originalServer, cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope originalLockReply =
                                await originalService.DispatchAsync(
                                    CreateCall(
                                        0x2501,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x90 },
                                            fileHandle,
                                            new byte[] { 0xA0 },
                                            "owner-a",
                                            111,
                                            0,
                                            40,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result originalLockResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(originalLockReply));
                            if (!originalLockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the original pre-restart lock to succeed.");
                            }

                            NsmService nsmService = new NsmService(coordinator);
                            RpcMessageEnvelope crashReply =
                                await nsmService.DispatchAsync(
                                    RpcMessageFactory.CreateCall(
                                        xid: 0x2502,
                                        program: (uint)SM_PROG_Program.Program,
                                        version: (uint)SM_PROG_Program.Version_SM_VERS,
                                        procedure: (uint)SM_PROG_Program.Procedure_SM_VERS_SM_SIMU_CRASH,
                                        credential: RpcAuthenticationCodec.CreateNone(),
                                        verifier: RpcAuthenticationCodec.CreateNone(),
                                        procedurePayload: Array.Empty<byte>()),
                                    cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(crashReply), "SM_SIMU_CRASH");

                            OpenNfsServer restartedServer = CreateLockingServer();
                            NlmV4Service restartedService = new NlmV4Service(restartedServer, recoveryCoordinator: coordinator);

                            RpcMessageEnvelope graceDeniedReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2503,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x91 },
                                            fileHandle,
                                            new byte[] { 0xB0 },
                                            "owner-b",
                                            222,
                                            0,
                                            40,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result graceDeniedResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(graceDeniedReply));
                            if (graceDeniedResult.Status != OpenNfsNlmV4Status.DeniedGracePeriod)
                            {
                                throw new InvalidOperationException("Expected new non-reclaim locks to be denied during the restart grace period.");
                            }

                            RpcMessageEnvelope reclaimReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2504,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x92 },
                                            fileHandle,
                                            new byte[] { 0xA0 },
                                            "owner-a",
                                            111,
                                            0,
                                            40,
                                            block: false,
                                            exclusive: true,
                                            reclaim: true,
                                            state: 1)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result reclaimResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(reclaimReply));
                            if (!reclaimResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the original owner to reclaim the lock successfully during grace.");
                            }

                            clock.Advance(TimeSpan.FromMinutes(6));

                            RpcMessageEnvelope postGraceTestReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2505,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x93 },
                                            fileHandle,
                                            new byte[] { 0xC0 },
                                            "owner-c",
                                            333,
                                            0,
                                            40,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult postGraceTestResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(postGraceTestReply));
                            if (postGraceTestResult.Status != OpenNfsNlmV4Status.Denied
                                || postGraceTestResult.ConflictingHolder is null
                                || !postGraceTestResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(new byte[] { 0xA0 }))
                            {
                                throw new InvalidOperationException("Expected the reclaimed lock to remain held after the grace period ends.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "ReclaimAfterServerRestartNegative",
                        displayName: "NLM v4 rejects late reclaim after grace and surfaces grace-period TEST failures before expiry",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            MutableClock clock = new MutableClock(new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero));
                            NsmRecoveryCoordinator coordinator = new NsmRecoveryCoordinator(TimeSpan.FromMinutes(5), clock.UtcNow);
                            OpenNfsServer originalServer = CreateLockingServer();
                            byte[] fileHandle = await CreateFileHandleAsync(originalServer, cancellationToken).ConfigureAwait(false);
                            NsmService nsmService = new NsmService(coordinator);

                            RpcMessageEnvelope crashReply =
                                await nsmService.DispatchAsync(
                                    RpcMessageFactory.CreateCall(
                                        xid: 0x2601,
                                        program: (uint)SM_PROG_Program.Program,
                                        version: (uint)SM_PROG_Program.Version_SM_VERS,
                                        procedure: (uint)SM_PROG_Program.Procedure_SM_VERS_SM_SIMU_CRASH,
                                        credential: RpcAuthenticationCodec.CreateNone(),
                                        verifier: RpcAuthenticationCodec.CreateNone(),
                                        procedurePayload: Array.Empty<byte>()),
                                    cancellationToken).ConfigureAwait(false);
                            OpenNfsRpcReplyDecoder.EnsureAcceptedSuccessWithoutPayload(RpcMessageCodec.Encode(crashReply), "SM_SIMU_CRASH");

                            OpenNfsServer restartedServer = CreateLockingServer();
                            NlmV4Service restartedService = new NlmV4Service(restartedServer, recoveryCoordinator: coordinator);

                            RpcMessageEnvelope graceTestReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2602,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x94 },
                                            fileHandle,
                                            new byte[] { 0xD0 },
                                            "owner-d",
                                            444,
                                            8,
                                            16,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult graceTestResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(graceTestReply));
                            if (graceTestResult.Status != OpenNfsNlmV4Status.DeniedGracePeriod)
                            {
                                throw new InvalidOperationException("Expected NLM TEST to surface the restart grace period before expiry.");
                            }

                            clock.Advance(TimeSpan.FromMinutes(6));

                            RpcMessageEnvelope newOwnerReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2603,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x95 },
                                            fileHandle,
                                            new byte[] { 0xE0 },
                                            "owner-e",
                                            555,
                                            8,
                                            16,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result newOwnerResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(newOwnerReply));
                            if (!newOwnerResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected a new non-reclaim owner to lock successfully after grace expires.");
                            }

                            RpcMessageEnvelope lateReclaimReply =
                                await restartedService.DispatchAsync(
                                    CreateCall(
                                        0x2604,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x96 },
                                            fileHandle,
                                            new byte[] { 0xF0 },
                                            "owner-f",
                                            666,
                                            8,
                                            16,
                                            block: false,
                                            exclusive: true,
                                            reclaim: true,
                                            state: 9)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result lateReclaimResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(lateReclaimReply));
                            if (lateReclaimResult.Status != OpenNfsNlmV4Status.Denied)
                            {
                                throw new InvalidOperationException("Expected reclaim to lose its grace-period privilege once grace has expired.");
                            }
                        }),

            };
        }
    }
}
