namespace OpenNFS.Client.Sessions
{
    using System;

    /// <summary>
    /// Non-throwing result envelope for NFSv4.1 <c>SendCompoundAsync</c>, distinguishing full success,
    /// partial success (some operations completed but the COMPOUND ended with a protocol-level error),
    /// and transport failure.
    /// </summary>
    /// <remarks>
    /// RFC 8881 §15.2 specifies that COMPOUND processing stops at the first operation that returns a
    /// non-OK status, and the resulting <c>resarray</c> contains the results of operations that did
    /// run. Callers that want to inspect those partial results without writing per-op success checks
    /// can use this envelope: <see cref="HasPartialResults"/> is <c>true</c> when the COMPOUND status
    /// is non-OK but at least one operation completed successfully before the failure.
    /// </remarks>
    public sealed class OpenNfsV41CompoundResult
    {
        private OpenNfsV41CompoundResult(
            OpenNfsV41CompoundOutcome? outcome,
            bool hasPartialResults,
            int operationsObservedSuccessfully,
            Exception? failure)
        {
            Outcome = outcome;
            HasPartialResults = hasPartialResults;
            OperationsObservedSuccessfully = operationsObservedSuccessfully;
            Failure = failure;
        }

        /// <summary>
        /// Gets the COMPOUND outcome when the call reached the server and produced a typed reply,
        /// regardless of whether the COMPOUND status was OK.
        /// </summary>
        public OpenNfsV41CompoundOutcome? Outcome { get; }

        /// <summary>
        /// Gets a value indicating whether the COMPOUND ended with a non-OK status while at least one
        /// operation completed successfully. When <c>true</c>, <see cref="Outcome"/> exposes the
        /// per-operation results that did run.
        /// </summary>
        public bool HasPartialResults { get; }

        /// <summary>
        /// Gets the number of operations that observed <c>NFS4_OK</c> in the result array.
        /// </summary>
        public int OperationsObservedSuccessfully { get; }

        /// <summary>
        /// Gets the transport-level failure when the call did not reach the server or could not be
        /// decoded. Null when the call produced a typed reply.
        /// </summary>
        public Exception? Failure { get; }

        /// <summary>
        /// Gets a value indicating whether the COMPOUND completed without any per-operation failure.
        /// </summary>
        public bool IsFullSuccess => Failure is null && Outcome is not null && !HasPartialResults && IsOutcomeOk;

        /// <summary>
        /// Gets a value indicating whether the call reached the server.
        /// </summary>
        public bool ReachedServer => Failure is null && Outcome is not null;

        private bool IsOutcomeOk
        {
            get
            {
                if (Outcome is null)
                {
                    return false;
                }

                return Outcome.Response.status == OpenNFS.Protocol.V41.Generated.nfsstat4.NFS4_OK;
            }
        }

        /// <summary>
        /// Returns the COMPOUND outcome when the result is a full success, or throws a typed
        /// <see cref="OpenNfsV41StatusException"/> on a partial / non-OK result, or rethrows the
        /// transport-level <see cref="Failure"/> exception when the call did not reach the server.
        /// </summary>
        /// <param name="operationName">A human-readable operation name embedded in any thrown exception.</param>
        /// <returns>The COMPOUND outcome.</returns>
        public OpenNfsV41CompoundOutcome GetOutcomeOrThrow(string operationName)
        {
            if (Failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Failure).Throw();
            }

            if (Outcome is null)
            {
                throw new InvalidOperationException(
                    "OpenNfsV41CompoundResult is in an invalid state: no outcome and no failure.");
            }

            OpenNFS.Protocol.V41.Generated.nfsstat4? overallStatus = Outcome.Response.status;
            if (overallStatus == OpenNFS.Protocol.V41.Generated.nfsstat4.NFS4_OK)
            {
                return Outcome;
            }

            int failedIndex = -1;
            OpenNFS.Protocol.V41.Generated.nfs_resop4[] results =
                Outcome.Response.resarray ?? Array.Empty<OpenNFS.Protocol.V41.Generated.nfs_resop4>();
            for (int index = 0; index < results.Length; index++)
            {
                OpenNFS.Protocol.V41.Generated.nfsstat4? status = ExtractOpStatus(results[index]);
                if (status != null && status.Value != OpenNFS.Protocol.V41.Generated.nfsstat4.NFS4_OK)
                {
                    failedIndex = index;
                    break;
                }
            }

            throw new OpenNfsV41StatusException(
                operationName ?? "NFSv4.1 COMPOUND",
                overallStatus ?? OpenNFS.Protocol.V41.Generated.nfsstat4.NFS4ERR_INVAL,
                failedIndex);
        }

        private static OpenNFS.Protocol.V41.Generated.nfsstat4? ExtractOpStatus(
            OpenNFS.Protocol.V41.Generated.nfs_resop4 result)
        {
            return result.resop switch
            {
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_SEQUENCE => result.opsequence?.sr_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_EXCHANGE_ID => result.opexchange_id?.eir_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_CREATE_SESSION => result.opcreate_session?.csr_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_DESTROY_SESSION => result.opdestroy_session?.dsr_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_DESTROY_CLIENTID => result.opdestroy_clientid?.dcr_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_BIND_CONN_TO_SESSION => result.opbind_conn_to_session?.bctsr_status,
                OpenNFS.Protocol.V41.Generated.nfs_opnum4.OP_ILLEGAL => result.opillegal?.status,
                _ => null,
            };
        }

        /// <summary>
        /// Builds a full-success result.
        /// </summary>
        /// <param name="outcome">The COMPOUND outcome.</param>
        /// <param name="operationsObservedSuccessfully">The number of OK results observed.</param>
        /// <returns>The result.</returns>
        public static OpenNfsV41CompoundResult FullSuccess(
            OpenNfsV41CompoundOutcome outcome,
            int operationsObservedSuccessfully)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            return new OpenNfsV41CompoundResult(
                outcome,
                hasPartialResults: false,
                operationsObservedSuccessfully,
                failure: null);
        }

        /// <summary>
        /// Builds a partial-success result.
        /// </summary>
        /// <param name="outcome">The COMPOUND outcome carrying the partial result array.</param>
        /// <param name="operationsObservedSuccessfully">The number of OK results observed before the failing op.</param>
        /// <returns>The result.</returns>
        public static OpenNfsV41CompoundResult Partial(
            OpenNfsV41CompoundOutcome outcome,
            int operationsObservedSuccessfully)
        {
            ArgumentNullException.ThrowIfNull(outcome);

            return new OpenNfsV41CompoundResult(
                outcome,
                hasPartialResults: true,
                operationsObservedSuccessfully,
                failure: null);
        }

        /// <summary>
        /// Builds a transport-failure result.
        /// </summary>
        /// <param name="failure">The transport-level failure.</param>
        /// <returns>The result.</returns>
        public static OpenNfsV41CompoundResult TransportFailure(Exception failure)
        {
            ArgumentNullException.ThrowIfNull(failure);

            return new OpenNfsV41CompoundResult(
                outcome: null,
                hasPartialResults: false,
                operationsObservedSuccessfully: 0,
                failure: failure);
        }
    }
}
