namespace OpenNFS.Protocol.V3.Nlm.LockAdapters
{
    using System;
    using System.IO;
    using System.Threading;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Server;
    using OpenNFS.Server.Requests;
    using OpenNFS.Server.Responses;

    internal static class NlmLockingAdapter
    {
        internal static NfsLockRequest CreateRequest(
            NfsLockOperation operation,
            NfsFileHandleTarget target,
            nlm4_lock protocolLock,
            byte[] ownerHandle,
            bool exclusive,
            bool block,
            bool reclaim,
            int state,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(protocolLock);

            string callerName = protocolLock.caller_name
                ?? throw new InvalidDataException("The decoded NLM v4 lock payload omitted caller_name.");
            int32 ownerProcessId = protocolLock.svid
                ?? throw new InvalidDataException("The decoded NLM v4 lock payload omitted svid.");
            uint64 offset = protocolLock.l_offset
                ?? throw new InvalidDataException("The decoded NLM v4 lock payload omitted l_offset.");
            uint64 length = protocolLock.l_len
                ?? throw new InvalidDataException("The decoded NLM v4 lock payload omitted l_len.");
            long ownerProcessIdValue = ownerProcessId.Value
                ?? throw new InvalidDataException("The decoded NLM v4 lock payload omitted the nested svid value.");

            return new NfsLockRequest(
                operation,
                target,
                new NfsLockOwner(callerName, ownerHandle, checked((int)ownerProcessIdValue)),
                new NfsLockRange(offset.Value, length.Value),
                exclusive,
                block,
                reclaim,
                state,
                cancellationToken);
        }

        internal static nlm4_res CreateResult(byte[] cookie, NfsLockResponse response)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            ArgumentNullException.ThrowIfNull(response);

            return new nlm4_res
            {
                cookie = CreateNetObject(cookie),
                stat = new nlm4_stat
                {
                    stat = MapDisposition(response.Disposition),
                },
            };
        }

        internal static nlm4_testres CreateTestResult(byte[] cookie, NfsLockResponse response)
        {
            ArgumentNullException.ThrowIfNull(cookie);
            ArgumentNullException.ThrowIfNull(response);

            nlm4_stats status = MapDisposition(response.Disposition);
            nlm4_testrply reply = new nlm4_testrply
            {
                stat = status,
            };

            if (status == nlm4_stats.NLM4_DENIED)
            {
                NfsLockConflict conflict = response.Conflict
                    ?? throw new InvalidDataException("A denied NLM v4 TEST response must include conflicting-lock details.");
                reply.holder = new nlm4_holder
                {
                    exclusive = conflict.Exclusive,
                    svid = new int32
                    {
                        Value = conflict.Owner.ProcessId,
                    },
                    oh = CreateNetObject(conflict.Owner.ToArray()),
                    l_offset = new uint64
                    {
                        Value = conflict.Range.Offset,
                    },
                    l_len = new uint64
                    {
                        Value = conflict.Range.Length,
                    },
                };
            }

            return new nlm4_testres
            {
                cookie = CreateNetObject(cookie),
                test_stat = reply,
            };
        }

        internal static byte[] ReadRequiredNetObject(netobj? value, string fieldName, bool allowEmpty = false)
        {
            byte[]? decodedValue = value?.Value;
            if (decodedValue is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            if (!allowEmpty && decodedValue.Length < 1)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field must not be empty.");
            }

            return decodedValue.AsSpan().ToArray();
        }

        internal static nlm4_stats MapDisposition(NfsLockDisposition disposition)
        {
            return disposition switch
            {
                NfsLockDisposition.Granted => nlm4_stats.NLM4_GRANTED,
                NfsLockDisposition.Denied => nlm4_stats.NLM4_DENIED,
                NfsLockDisposition.DeniedNoLocks => nlm4_stats.NLM4_DENIED_NOLOCKS,
                NfsLockDisposition.Blocked => nlm4_stats.NLM4_BLOCKED,
                NfsLockDisposition.DeniedGracePeriod => nlm4_stats.NLM4_DENIED_GRACE_PERIOD,
                NfsLockDisposition.Deadlock => nlm4_stats.NLM4_DEADLCK,
                NfsLockDisposition.ReadOnlyFileSystem => nlm4_stats.NLM4_ROFS,
                NfsLockDisposition.StaleFileHandle => nlm4_stats.NLM4_STALE_FH,
                NfsLockDisposition.FileTooLarge => nlm4_stats.NLM4_FBIG,
                _ => nlm4_stats.NLM4_FAILED,
            };
        }

        private static netobj CreateNetObject(byte[] value)
        {
            return new netobj
            {
                Value = value.AsSpan().ToArray(),
            };
        }
    }
}
