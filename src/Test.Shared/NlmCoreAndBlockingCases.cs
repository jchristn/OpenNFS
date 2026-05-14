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
    /// Core lock, conflict, cancel, and blocking NLM v4 suites.
    /// </summary>
    internal static class NlmCoreAndBlockingCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
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

            };
        }
    }
}
