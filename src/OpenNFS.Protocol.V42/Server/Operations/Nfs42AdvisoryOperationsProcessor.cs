namespace OpenNFS.Protocol.V42.Server.Operations
{
    using System;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V42.Generated;

    /// <summary>
    /// Implements the remaining RFC 7862 v4.2 operations that are not directly tied to sparse-file
    /// support or server-side copy/clone: <c>IO_ADVISE</c>, <c>OFFLOAD_CANCEL</c>, <c>OFFLOAD_STATUS</c>,
    /// <c>WRITE_SAME</c>, and <c>COPY_NOTIFY</c>.
    /// </summary>
    /// <remarks>
    /// Of these, <c>IO_ADVISE</c> is always processable: per RFC 7862 §15.5 the server is free to
    /// honor zero or more of the supplied hints, and returning an empty acknowledged-hints bitmap is a
    /// standards-compliant "no hints accepted" response. The remaining operations target features the
    /// current server does not advertise (asynchronous copy and inter-server copy in the OFFLOAD and
    /// COPY_NOTIFY cases; pre-allocated WRITE_SAME without explicit host opt-in), so they surface
    /// <c>NFS4ERR_NOTSUPP</c> rather than fabricating success — RFC 7862 §15.7 / §15.8 / §15.9 / §15.11
    /// permit that response when the corresponding feature is not advertised.
    /// </remarks>
    public sealed class Nfs42AdvisoryOperationsProcessor
    {
        /// <summary>
        /// Processes an IO_ADVISE request.
        /// </summary>
        /// <param name="arguments">The IO_ADVISE arguments.</param>
        /// <returns>The result.</returns>
        public ValueTask<IO_ADVISE4res> ProcessIoAdviseAsync(IO_ADVISE4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            if (arguments.iaa_offset is null || arguments.iaa_count is null || arguments.iaa_hints is null)
            {
                return ValueTask.FromResult(new IO_ADVISE4res { ior_status = nfsstat4.NFS4ERR_INVAL });
            }

            return ValueTask.FromResult(new IO_ADVISE4res
            {
                ior_status = nfsstat4.NFS4_OK,
                resok4 = new IO_ADVISE4resok
                {
                    ior_hints = new bitmap4 { Value = Array.Empty<uint>() },
                },
            });
        }

        /// <summary>
        /// Processes an OFFLOAD_CANCEL request.
        /// </summary>
        /// <param name="arguments">The OFFLOAD_CANCEL arguments.</param>
        /// <returns>The result.</returns>
        public ValueTask<OFFLOAD_CANCEL4res> ProcessOffloadCancelAsync(OFFLOAD_CANCEL4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            return ValueTask.FromResult(new OFFLOAD_CANCEL4res { ocr_status = nfsstat4.NFS4ERR_NOTSUPP });
        }

        /// <summary>
        /// Processes an OFFLOAD_STATUS request.
        /// </summary>
        /// <param name="arguments">The OFFLOAD_STATUS arguments.</param>
        /// <returns>The result.</returns>
        public ValueTask<OFFLOAD_STATUS4res> ProcessOffloadStatusAsync(OFFLOAD_STATUS4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            return ValueTask.FromResult(new OFFLOAD_STATUS4res { osr_status = nfsstat4.NFS4ERR_NOTSUPP });
        }

        /// <summary>
        /// Processes a WRITE_SAME request.
        /// </summary>
        /// <param name="arguments">The WRITE_SAME arguments.</param>
        /// <returns>The result.</returns>
        public ValueTask<WRITE_SAME4res> ProcessWriteSameAsync(WRITE_SAME4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            return ValueTask.FromResult(new WRITE_SAME4res { wsr_status = nfsstat4.NFS4ERR_NOTSUPP });
        }

        /// <summary>
        /// Processes a COPY_NOTIFY request.
        /// </summary>
        /// <param name="arguments">The COPY_NOTIFY arguments.</param>
        /// <returns>The result.</returns>
        public ValueTask<COPY_NOTIFY4res> ProcessCopyNotifyAsync(COPY_NOTIFY4args arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            return ValueTask.FromResult(new COPY_NOTIFY4res { cnr_status = nfsstat4.NFS4ERR_NOTSUPP });
        }
    }
}
