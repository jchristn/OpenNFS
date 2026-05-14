namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Replay;
    using OpenNFS.Rpc.RpcMessages;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Replay-cache primitive cases.
    /// </summary>
    internal static class ReplayCacheCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                new TestCaseDescriptor(
                    suiteId: "ReplaySuites",
                    caseId: "DuplicateCallReturnsStableReply",
                    displayName: "Replay cache returns a stable reply for duplicate calls",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        DateTimeOffset storedAt = new DateTimeOffset(2026, 4, 27, 12, 0, 0, TimeSpan.Zero);
                        RpcReplayCache<RpcRequestCorrelationKey, RpcMessageEnvelope> replayCache =
                            new RpcReplayCache<RpcRequestCorrelationKey, RpcMessageEnvelope>(TimeSpan.FromMinutes(5));

                        RpcRequestCorrelationKey originalKey = new RpcRequestCorrelationKey(
                            requesterIdentity: "127.0.0.1:1400",
                            transactionId: 0x10101010,
                            programNumber: (uint)NFS_PROGRAM_Program.Program,
                            versionNumber: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ);

                        RpcMessageEnvelope originalReply = RpcMessageFactory.CreateAcceptedReply(
                            xid: 0x10101010,
                            status: accept_stat.SUCCESS,
                            verifier: RpcAuthenticationCodec.CreateNone(),
                            procedurePayload: new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE });

                        replayCache.Store(originalKey, originalReply, storedAt);

                        RpcRequestCorrelationKey duplicateKey = new RpcRequestCorrelationKey(
                            requesterIdentity: "127.0.0.1:1400",
                            transactionId: 0x10101010,
                            programNumber: (uint)NFS_PROGRAM_Program.Program,
                            versionNumber: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                            procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_READ);

                        if (!replayCache.TryGet(duplicateKey, storedAt.AddMinutes(1), out RpcMessageEnvelope? cachedReply) || cachedReply is null)
                        {
                            throw new InvalidOperationException("Expected the replay cache to return a cached reply for a duplicate request correlation key.");
                        }

                        byte[] originalReplyBytes = RpcMessageCodec.Encode(originalReply);
                        byte[] cachedReplyBytes = RpcMessageCodec.Encode(cachedReply);
                        if (!originalReplyBytes.SequenceEqual(cachedReplyBytes))
                        {
                            throw new InvalidOperationException("Expected the replay cache to return a byte-stable reply for duplicate requests.");
                        }

                        if (replayCache.Count != 1)
                        {
                            throw new InvalidOperationException("Expected duplicate replay lookup to leave exactly one cache entry.");
                        }

                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor(
                    suiteId: "ReplaySuites",
                    caseId: "ExpiredEntryIsPurgedSafely",
                    displayName: "Replay cache safely purges expired entries",
                    tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                    executeAsync: _ =>
                    {
                        DateTimeOffset now = new DateTimeOffset(2026, 4, 27, 12, 30, 0, TimeSpan.Zero);
                        RpcReplayCache<RpcRequestCorrelationKey, string> replayCache =
                            new RpcReplayCache<RpcRequestCorrelationKey, string>(TimeSpan.FromSeconds(30));

                        RpcRequestCorrelationKey expiredKeyOne = new RpcRequestCorrelationKey("client-a", 1, 100003, 3, 6);
                        RpcRequestCorrelationKey expiredKeyTwo = new RpcRequestCorrelationKey("client-b", 2, 100003, 3, 7);
                        RpcRequestCorrelationKey freshKey = new RpcRequestCorrelationKey("client-c", 3, 100003, 3, 8);

                        replayCache.Store(expiredKeyOne, "expired-a", now.AddMinutes(-2));
                        replayCache.Store(expiredKeyTwo, "expired-b", now.AddMinutes(-1));
                        replayCache.Store(freshKey, "fresh", now);

                        int purgedEntryCount = replayCache.PurgeExpired(now);
                        if (purgedEntryCount != 2)
                        {
                            throw new InvalidOperationException("Expected replay-cache purge to remove both expired entries.");
                        }

                        if (replayCache.TryGet(expiredKeyOne, now, out string? expiredValue))
                        {
                            throw new InvalidOperationException("Expected the first expired replay entry to be unavailable after purge, but received '" + expiredValue + "'.");
                        }

                        if (replayCache.TryGet(expiredKeyTwo, now, out expiredValue))
                        {
                            throw new InvalidOperationException("Expected the second expired replay entry to be unavailable after purge, but received '" + expiredValue + "'.");
                        }

                        if (!replayCache.TryGet(freshKey, now.AddSeconds(10), out string? freshValue) || !string.Equals(freshValue, "fresh", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Expected the non-expired replay entry to survive purge and remain retrievable.");
                        }

                        if (replayCache.Count != 1)
                        {
                            throw new InvalidOperationException("Expected exactly one replay-cache entry to remain after purging expired entries.");
                        }

                        return Task.CompletedTask;
                    }),
            };
        }
    }
}
