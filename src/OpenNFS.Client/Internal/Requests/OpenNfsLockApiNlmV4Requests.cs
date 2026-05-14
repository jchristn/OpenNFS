namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Apis;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Encodes the grouped NLM v4 locking requests exposed by <see cref="OpenNFS.Client.Apis.LockApis"/>.
    /// </summary>
    internal static class OpenNfsLockApiNlmV4Requests
    {
        internal static OpenNfsV3ProcedureRequest CreateTestRequest(
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
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmTestProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateLockRequest(
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
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                reclaim = reclaim,
                state = new int32
                {
                    Value = state,
                },
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmLockProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateCancelRequest(
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
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmCancelProcedure, arguments.WriteTo);
        }

        internal static OpenNfsV3ProcedureRequest CreateUnlockRequest(
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
                cookie = CreateNetObject(cookie, nameof(cookie), allowEmpty: true),
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return CreateEncodedRequest(OpenNfsV3RpcConstants.NlmUnlockProcedure, arguments.WriteTo);
        }

        private static nlm4_lock CreateLock(
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            string safeCallerName = OpenNfsClientArgument.RequireText(callerName, nameof(callerName));
            return new nlm4_lock
            {
                caller_name = safeCallerName,
                fh = CreateNetObject(fileHandle, nameof(fileHandle), allowEmpty: false),
                oh = CreateNetObject(ownerHandle, nameof(ownerHandle), allowEmpty: false),
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

        private static netobj CreateNetObject(byte[] value, string parameterName, bool allowEmpty)
        {
            byte[] safeValue = OpenNfsClientArgument.RequireBytes(value, parameterName, allowEmpty);
            return new netobj
            {
                Value = safeValue,
            };
        }

        private static OpenNfsV3ProcedureRequest CreateEncodedRequest(uint procedureNumber, Action<XdrWriter> writePayload)
        {
            return OpenNfsApiEncoding.CreateEncodedRequest(
                procedureNumber,
                writePayload,
                programNumber: OpenNfsV3RpcConstants.NlmProgram,
                versionNumber: OpenNfsV3RpcConstants.NlmVersion);
        }
    }
}
