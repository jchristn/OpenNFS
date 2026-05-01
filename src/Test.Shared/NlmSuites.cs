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

    /// <summary>
    /// Touchstone suites covering the first NLM v4 server and grouped-client lock surface.
    /// </summary>
    public static class NlmSuites
    {
        /// <summary>
        /// Creates the shared NLM v4 suite catalog.
        /// </summary>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: "NlmSuites",
                displayName: "NLM v4 Foundation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "LockUnlockRoundTripPositive",
                        displayName: "NLM v4 lock, test, unlock, and relock flows succeed for distinct owners",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);
                            byte[] firstCookie = new byte[] { 0x01, 0x02 };
                            byte[] secondCookie = new byte[] { 0x03, 0x04 };

                            RpcMessageEnvelope lockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x1001,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            firstCookie,
                                            fileHandle,
                                            new byte[] { 0xAA },
                                            "owner-a",
                                            101,
                                            0,
                                            32,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result lockResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(lockReply));
                            if (!lockResult.IsSuccess || !lockResult.Cookie.Span.SequenceEqual(firstCookie))
                            {
                                throw new InvalidOperationException("Expected the first NLM v4 lock request to succeed and echo the cookie.");
                            }

                            RpcMessageEnvelope unlockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x1002,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK,
                                        CreateUnlockArguments(
                                            firstCookie,
                                            fileHandle,
                                            new byte[] { 0xAA },
                                            "owner-a",
                                            101,
                                            0,
                                            32)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result unlockResult = new OpenNfsClientBuilder().Build().Locks.ReadUnlockV4Result(RpcMessageCodec.Encode(unlockReply));
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the NLM v4 unlock request to succeed.");
                            }

                            RpcMessageEnvelope secondLockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x1003,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            secondCookie,
                                            fileHandle,
                                            new byte[] { 0xBB },
                                            "owner-b",
                                            202,
                                            0,
                                            32,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result secondLockResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(secondLockReply));
                            if (!secondLockResult.IsSuccess || !secondLockResult.Cookie.Span.SequenceEqual(secondCookie))
                            {
                                throw new InvalidOperationException("Expected the second NLM v4 lock request to succeed after unlock.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "LockConflictAndCancelNegative",
                        displayName: "NLM v4 surfaces denied, blocked, cancel, and stale-handle negative variants",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2001,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        new byte[] { 0x10 },
                                        fileHandle,
                                        new byte[] { 0xAA },
                                        "holder",
                                        11,
                                        8,
                                        16,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope testReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2002,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x11 },
                                            fileHandle,
                                            new byte[] { 0xBB },
                                            "other",
                                            22,
                                            8,
                                            16,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult testResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(testReply));
                            if (testResult.Status != OpenNfsNlmV4Status.Denied
                                || testResult.ConflictingHolder is null
                                || !testResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(new byte[] { 0xAA }))
                            {
                                throw new InvalidOperationException("Expected NLM v4 TEST to surface a denied conflict with holder details.");
                            }

                            RpcMessageEnvelope blockedReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2003,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x12 },
                                            fileHandle,
                                            new byte[] { 0xCC },
                                            "blocked",
                                            33,
                                            8,
                                            16,
                                            block: true,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result blockedResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(blockedReply));
                            if (blockedResult.Status != OpenNfsNlmV4Status.Blocked)
                            {
                                throw new InvalidOperationException("Expected a conflicting blocking NLM v4 LOCK request to report BLOCKED.");
                            }

                            RpcMessageEnvelope cancelReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2004,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL,
                                        CreateCancelArguments(
                                            new byte[] { 0x12 },
                                            fileHandle,
                                            new byte[] { 0xCC },
                                            "blocked",
                                            33,
                                            8,
                                            16,
                                            block: true,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result cancelResult = new OpenNfsClientBuilder().Build().Locks.ReadCancelV4Result(RpcMessageCodec.Encode(cancelReply));
                            if (!cancelResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected NLM v4 CANCEL to succeed for a queued blocking request.");
                            }

                            RpcMessageEnvelope staleReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2005,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            new byte[] { 0x13 },
                                            new byte[] { 0x00, 0x01, 0x02 },
                                            new byte[] { 0xDD },
                                            "stale",
                                            44,
                                            0,
                                            8,
                                            block: false,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result staleResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(staleReply));
                            if (staleResult.Status != OpenNfsNlmV4Status.StaleFileHandle)
                            {
                                throw new InvalidOperationException("Expected NLM v4 LOCK to report STALE_FH for an unknown filehandle.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "BlockingLockWakeupPositive",
                        displayName: "NLM v4 blocking locks wake on unlock and promote the waiter after a granted callback",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            RecordingNlmV4GrantedCallbackDispatcher dispatcher = new RecordingNlmV4GrantedCallbackDispatcher(
                                static (_, _) => Task.FromResult(NlmV4GrantedCallbackStatus.Granted));
                            NlmV4Service service = new NlmV4Service(server, dispatcher);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);
                            byte[] waiterCookie = new byte[] { 0x41, 0x42 };
                            byte[] waiterOwnerHandle = new byte[] { 0xBC };

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2101,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        new byte[] { 0x40 },
                                        fileHandle,
                                        new byte[] { 0xAB },
                                        "holder",
                                        71,
                                        0,
                                        32,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            RpcMessageEnvelope blockedReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2102,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                        CreateLockArguments(
                                            waiterCookie,
                                            fileHandle,
                                            waiterOwnerHandle,
                                            "waiter",
                                            72,
                                            0,
                                            32,
                                            block: true,
                                            exclusive: true,
                                            reclaim: false,
                                            state: 0)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result blockedResult = new OpenNfsClientBuilder().Build().Locks.ReadLockV4Result(RpcMessageCodec.Encode(blockedReply));
                            if (blockedResult.Status != OpenNfsNlmV4Status.Blocked)
                            {
                                throw new InvalidOperationException("Expected the blocking waiter to remain queued until the holder unlocks.");
                            }

                            RpcMessageEnvelope unlockReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2103,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK,
                                        CreateUnlockArguments(
                                            new byte[] { 0x40 },
                                            fileHandle,
                                            new byte[] { 0xAB },
                                            "holder",
                                            71,
                                            0,
                                            32)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result unlockResult = new OpenNfsClientBuilder().Build().Locks.ReadUnlockV4Result(RpcMessageCodec.Encode(unlockReply));
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the unlock that releases the waiter to succeed.");
                            }

                            if (dispatcher.Callbacks.Count != 1)
                            {
                                throw new InvalidOperationException("Expected exactly one granted callback during waiter wakeup.");
                            }

                            NlmV4GrantedCallback callback = dispatcher.Callbacks[0];
                            if (!callback.Cookie.Span.SequenceEqual(waiterCookie)
                                || !callback.FileHandle.Span.SequenceEqual(fileHandle)
                                || !callback.OwnerHandle.Span.SequenceEqual(waiterOwnerHandle)
                                || !string.Equals(callback.CallerName, "waiter", StringComparison.Ordinal)
                                || callback.OwnerProcessId != 72
                                || callback.Offset != 0
                                || callback.Length != 32
                                || !callback.Exclusive)
                            {
                                throw new InvalidOperationException("Expected the granted callback payload to match the queued waiter.");
                            }

                            RpcMessageEnvelope testReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2104,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x43 },
                                            fileHandle,
                                            new byte[] { 0xCD },
                                            "third",
                                            73,
                                            0,
                                            32,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult testResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(testReply));
                            if (testResult.Status != OpenNfsNlmV4Status.Denied
                                || testResult.ConflictingHolder is null
                                || !testResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(waiterOwnerHandle))
                            {
                                throw new InvalidOperationException("Expected the woken waiter to become the conflicting holder.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "BlockingLockWakeupNegative",
                        displayName: "NLM v4 leaves blocked waiters queued when the granted callback fails",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            RecordingNlmV4GrantedCallbackDispatcher dispatcher = new RecordingNlmV4GrantedCallbackDispatcher(
                                static (_, _) => Task.FromResult(NlmV4GrantedCallbackStatus.Failed));
                            NlmV4Service service = new NlmV4Service(server, dispatcher);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);
                            byte[] waiterCookie = new byte[] { 0x51, 0x52 };
                            byte[] waiterOwnerHandle = new byte[] { 0xDC };

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2201,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        new byte[] { 0x50 },
                                        fileHandle,
                                        new byte[] { 0xCB },
                                        "holder",
                                        81,
                                        8,
                                        16,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2202,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        waiterCookie,
                                        fileHandle,
                                        waiterOwnerHandle,
                                        "waiter",
                                        82,
                                        8,
                                        16,
                                        block: true,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x2203,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_UNLOCK,
                                    CreateUnlockArguments(
                                        new byte[] { 0x50 },
                                        fileHandle,
                                        new byte[] { 0xCB },
                                        "holder",
                                        81,
                                        8,
                                        16)),
                                cancellationToken).ConfigureAwait(false);

                            if (dispatcher.Callbacks.Count != 1)
                            {
                                throw new InvalidOperationException("Expected exactly one failed granted callback attempt.");
                            }

                            RpcMessageEnvelope testReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2204,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_TEST,
                                        CreateTestArguments(
                                            new byte[] { 0x53 },
                                            fileHandle,
                                            new byte[] { 0xEF },
                                            "third",
                                            83,
                                            8,
                                            16,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult testResult = new OpenNfsClientBuilder().Build().Locks.ReadTestV4Result(RpcMessageCodec.Encode(testReply));
                            if (!testResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the waiter to remain ungranted after a failed callback.");
                            }

                            RpcMessageEnvelope cancelReply =
                                await service.DispatchAsync(
                                    CreateCall(
                                        0x2205,
                                        (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_CANCEL,
                                        CreateCancelArguments(
                                            waiterCookie,
                                            fileHandle,
                                            waiterOwnerHandle,
                                            "waiter",
                                            82,
                                            8,
                                            16,
                                            block: true,
                                            exclusive: true)),
                                    cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result cancelResult = new OpenNfsClientBuilder().Build().Locks.ReadCancelV4Result(RpcMessageCodec.Encode(cancelReply));
                            if (!cancelResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected CANCEL to remove the waiter that could not be granted.");
                            }
                        }),

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

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisExecutePositive",
                        displayName: "Grouped client lock APIs execute and decode typed NLM v4 replies",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            ScriptedRpcExecutor executor = new ScriptedRpcExecutor(
                                (request, _, token) => service.DispatchAsync(request.CallEnvelope, token));
                            OpenNfsClient client = CreateClient(executor);
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsV3ProcedurePlan testPlan = await client.Locks.PrepareTestV4Async(
                                new byte[] { 0x21 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                exclusive: false,
                                cancellationToken).ConfigureAwait(false);
                            if (testPlan.ProgramNumber != 100021
                                || testPlan.VersionNumber != 4
                                || testPlan.ProcedureNumber != 1)
                            {
                                throw new InvalidOperationException("Expected the grouped lock API to plan an NLM v4 TEST call.");
                            }

                            OpenNfsNlmV4Result lockResult = await client.Locks.LockV4Async(
                                new byte[] { 0x22 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);
                            if (!lockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 LOCK execution to succeed.");
                            }

                            OpenNfsNlmV4Result unlockResult = await client.Locks.UnlockV4Async(
                                new byte[] { 0x22 },
                                fileHandle,
                                new byte[] { 0xA1 },
                                "client-a",
                                501,
                                0,
                                64,
                                cancellationToken).ConfigureAwait(false);
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 UNLOCK execution to succeed.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisNegative",
                        displayName: "Grouped client lock APIs surface denied replies and malformed payload failures",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            NlmV4Service service = new NlmV4Service(server);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            _ = await service.DispatchAsync(
                                CreateCall(
                                    0x3001,
                                    (uint)NLM_PROG_Program.Procedure_NLM4_VERS_NLMPROC4_LOCK,
                                    CreateLockArguments(
                                        new byte[] { 0x31 },
                                        fileHandle,
                                        new byte[] { 0xE1 },
                                        "holder",
                                        601,
                                        4,
                                        12,
                                        block: false,
                                        exclusive: true,
                                        reclaim: false,
                                        state: 0)),
                                cancellationToken).ConfigureAwait(false);

                            ScriptedRpcExecutor executor = new ScriptedRpcExecutor(
                                async (request, _, token) =>
                                {
                                    if (request.OperationName == "NLM v4 TEST")
                                    {
                                        return await service.DispatchAsync(request.CallEnvelope, token).ConfigureAwait(false);
                                    }

                                    return RpcMessageFactory.CreateAcceptedReply(
                                        xid: request.CallEnvelope.Header.xid,
                                        status: accept_stat.SUCCESS,
                                        verifier: RpcAuthenticationCodec.CreateNone(),
                                        procedurePayload: new byte[] { 0x00, 0x00, 0x00, 0x01 });
                                });

                            OpenNfsClient client = CreateClient(executor);
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult deniedResult = await client.Locks.TestV4Async(
                                new byte[] { 0x32 },
                                fileHandle,
                                new byte[] { 0xE2 },
                                "other",
                                602,
                                4,
                                12,
                                exclusive: true,
                                cancellationToken).ConfigureAwait(false);

                            if (deniedResult.Status != OpenNfsNlmV4Status.Denied
                                || deniedResult.ConflictingHolder is null)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 TEST execution to surface a denied conflict.");
                            }

                            bool sawProtocolFailure = false;
                            try
                            {
                                _ = await client.Locks.LockV4Async(
                                    new byte[] { 0x33 },
                                    fileHandle,
                                    new byte[] { 0xE3 },
                                    "malformed",
                                    603,
                                    0,
                                    8,
                                    block: false,
                                    exclusive: true,
                                    reclaim: false,
                                    state: 0,
                                    cancellationToken).ConfigureAwait(false);
                            }
                            catch (OpenNfsClientProtocolException exception)
                            {
                                if (exception.Category != OpenNfsErrorCategory.ProtocolError
                                    || !exception.Message.Contains("malformed", StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("Expected grouped NLM v4 malformed-payload failures to surface a typed protocol exception.");
                                }

                                sawProtocolFailure = true;
                            }

                            if (!sawProtocolFailure)
                            {
                                throw new InvalidOperationException("Expected grouped NLM v4 LOCK execution to fail on a malformed payload with a typed protocol exception.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisOverTcpPositive",
                        displayName: "Grouped client lock APIs execute over the real TCP NLM listener",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                            await using OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("127.0.0.1", host.NlmPort)
                                .Build();
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4Result lockResult = await client.Locks.LockV4Async(
                                new byte[] { 0x81 },
                                fileHandle,
                                new byte[] { 0x91 },
                                "tcp-owner",
                                701,
                                0,
                                48,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);
                            if (!lockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM lock to succeed.");
                            }

                            OpenNfsNlmV4Result unlockResult = await client.Locks.UnlockV4Async(
                                new byte[] { 0x81 },
                                fileHandle,
                                new byte[] { 0x91 },
                                "tcp-owner",
                                701,
                                0,
                                48,
                                cancellationToken).ConfigureAwait(false);
                            if (!unlockResult.IsSuccess)
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM unlock to succeed.");
                            }
                        }),

                    new TestCaseDescriptor(
                        suiteId: "NlmSuites",
                        caseId: "GroupedClientLockApisOverTcpNegative",
                        displayName: "Grouped client lock APIs surface denied conflicts over the real TCP NLM listener",
                        tags: new List<string> { TestCategories.Integration, TestCategories.Automated },
                        executeAsync: async cancellationToken =>
                        {
                            OpenNfsServer server = CreateLockingServer();
                            await using OpenNfsTcpInteropHost host = OpenNfsTcpInteropHost.Start(server);
                            await using OpenNfsClient client = new OpenNfsClientBuilder()
                                .WithPrimaryEndpoint("127.0.0.1", host.NlmPort)
                                .Build();
                            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
                            byte[] fileHandle = await CreateFileHandleAsync(server, cancellationToken).ConfigureAwait(false);

                            _ = await client.Locks.LockV4Async(
                                new byte[] { 0x82 },
                                fileHandle,
                                new byte[] { 0x92 },
                                "holder",
                                702,
                                4,
                                20,
                                block: false,
                                exclusive: true,
                                reclaim: false,
                                state: 0,
                                cancellationToken).ConfigureAwait(false);

                            OpenNfsNlmV4TestResult deniedResult = await client.Locks.TestV4Async(
                                new byte[] { 0x83 },
                                fileHandle,
                                new byte[] { 0x93 },
                                "other",
                                703,
                                4,
                                20,
                                exclusive: true,
                                cancellationToken).ConfigureAwait(false);

                            if (deniedResult.Status != OpenNfsNlmV4Status.Denied
                                || deniedResult.ConflictingHolder is null
                                || !deniedResult.ConflictingHolder.OwnerHandle.Span.SequenceEqual(new byte[] { 0x92 }))
                            {
                                throw new InvalidOperationException("Expected the TCP-hosted NLM test to surface the held conflicting lock.");
                            }
                        }),
                });
        }

        private static OpenNfsClient CreateClient(ScriptedRpcExecutor executor)
        {
            return new OpenNfsClient(
                new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("nlm.example", 4045)
                    .Build()
                    .Settings,
                executor,
                transportPipeline: null);
        }

        private static OpenNfsServer CreateLockingServer()
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\locks"] = NfsPathKind.Directory,
                    ["C:\\locks\\data.bin"] = NfsPathKind.File,
                });

            return new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .Build();
        }

        private static async Task<byte[]> CreateFileHandleAsync(OpenNfsServer server, System.Threading.CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/data.bin", "C:\\locks\\data.bin"),
                cancellationToken).ConfigureAwait(false);
            return fileHandle.ToArray();
        }

        private static RpcMessageEnvelope CreateCall(uint xid, uint procedureNumber, byte[] procedurePayload)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NLM_PROG_Program.Program,
                version: (uint)NLM_PROG_Program.Version_NLM4_VERS,
                procedure: procedureNumber,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        private static byte[] CreateTestArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive)
        {
            nlm4_testargs arguments = new nlm4_testargs
            {
                cookie = CreateNetObject(cookie),
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateLockArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state)
        {
            nlm4_lockargs arguments = new nlm4_lockargs
            {
                cookie = CreateNetObject(cookie),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                reclaim = reclaim,
                state = new int32
                {
                    Value = state,
                },
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateCancelArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive)
        {
            nlm4_cancargs arguments = new nlm4_cancargs
            {
                cookie = CreateNetObject(cookie),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        private static byte[] CreateUnlockArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            nlm4_unlockargs arguments = new nlm4_unlockargs
            {
                cookie = CreateNetObject(cookie),
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        private static nlm4_lock CreateLock(
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            return new nlm4_lock
            {
                caller_name = callerName,
                fh = CreateNetObject(fileHandle),
                oh = CreateNetObject(ownerHandle),
                svid = new int32
                {
                    Value = ownerProcessId,
                },
                l_offset = new uint64
                {
                    Value = offset,
                },
                l_len = new uint64
                {
                    Value = length,
                },
            };
        }

        private static netobj CreateNetObject(byte[] value)
        {
            return new netobj
            {
                Value = value,
            };
        }

        private static byte[] Encode(Action<XdrWriter> writeAction)
        {
            XdrWriter writer = new XdrWriter();
            writeAction(writer);
            return writer.ToArray();
        }
    }
}
