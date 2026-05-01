namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;

    /// <summary>
    /// Routes inbound NFSv4.1 <c>CB_COMPOUND</c> requests through a back-channel slot table and
    /// dispatches each operation to a configured <see cref="OpenNfsV41CallbackHandler"/>.
    /// </summary>
    /// <remarks>
    /// This dispatcher operates at the typed argument/result level. It enforces the RFC 8881 §20.10
    /// rule that <c>CB_SEQUENCE</c> must be the first operation in a CB_COMPOUND and tracks back-channel
    /// slot state through the standard <see cref="Nfs41SlotTable"/>. The wire-level transport that
    /// receives CB_COMPOUND requests over the back-channel side of the underlying TCP connection is a
    /// separate slice; this dispatcher is the typed processing engine that the eventual wire-level
    /// integration plugs into.
    /// </remarks>
    public sealed class OpenNfsV41CallbackDispatcher
    {
        /// <summary>
        /// Gets the supported NFSv4.1 minor version. RFC 8881 defines v4.1 as minor version 1.
        /// </summary>
        public const uint SupportedMinorVersion = 1;

        private readonly byte[] expectedSessionId;
        private readonly Nfs41SlotTable backChannelSlotTable;
        private readonly OpenNfsV41CallbackHandler handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenNfsV41CallbackDispatcher"/> class.
        /// </summary>
        /// <param name="expectedSessionId">The 16-byte session identifier this dispatcher is bound to.</param>
        /// <param name="backChannelSlotCount">The back-channel slot count negotiated during <c>CREATE_SESSION</c>.</param>
        /// <param name="handler">The callback handler.</param>
        public OpenNfsV41CallbackDispatcher(
            ReadOnlySpan<byte> expectedSessionId,
            uint backChannelSlotCount,
            OpenNfsV41CallbackHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (expectedSessionId.Length != Nfs41SessionId.Length)
            {
                throw new ArgumentException(
                    "Expected sessionid must be exactly " + Nfs41SessionId.Length + " bytes.",
                    nameof(expectedSessionId));
            }

            this.expectedSessionId = expectedSessionId.ToArray();
            this.handler = handler;
            backChannelSlotTable = new Nfs41SlotTable(backChannelSlotCount == 0 ? 1u : backChannelSlotCount);
        }

        /// <summary>
        /// Gets the configured back-channel slot count.
        /// </summary>
        public uint BackChannelSlotCount => backChannelSlotTable.Size;

        /// <summary>
        /// Processes an inbound CB_COMPOUND.
        /// </summary>
        /// <param name="arguments">The decoded CB_COMPOUND arguments.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The CB_COMPOUND result.</returns>
        public async Task<CB_COMPOUND4res> ProcessCompoundAsync(
            CB_COMPOUND4args arguments,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            if (arguments.minorversion != SupportedMinorVersion)
            {
                return new CB_COMPOUND4res
                {
                    status = nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH,
                    tag = arguments.tag ?? EmptyTag(),
                    resarray = Array.Empty<nfs_cb_resop4>(),
                };
            }

            nfs_cb_argop4[] operations = arguments.argarray ?? Array.Empty<nfs_cb_argop4>();
            if (operations.Length == 0 || operations[0].argop != (uint)nfs_cb_opnum4.OP_CB_SEQUENCE)
            {
                return new CB_COMPOUND4res
                {
                    status = nfsstat4.NFS4ERR_OP_NOT_IN_SESSION,
                    tag = arguments.tag ?? EmptyTag(),
                    resarray = Array.Empty<nfs_cb_resop4>(),
                };
            }

            CB_SEQUENCE4args? sequenceArgs = operations[0].opcbsequence;
            if (sequenceArgs?.csa_sessionid?.Value is null
                || sequenceArgs.csa_sessionid.Value.Length != Nfs41SessionId.Length
                || sequenceArgs.csa_sequenceid is null
                || sequenceArgs.csa_slotid is null)
            {
                return BuildBadSessionResult(arguments.tag);
            }

            if (!sequenceArgs.csa_sessionid.Value.AsSpan().SequenceEqual(expectedSessionId))
            {
                return BuildBadSessionResult(arguments.tag);
            }

            uint slotId = sequenceArgs.csa_slotid.Value;
            uint sequenceId = sequenceArgs.csa_sequenceid.Value;
            Nfs41SlotEvaluation evaluation = backChannelSlotTable.Evaluate(slotId, sequenceId);

            switch (evaluation.State)
            {
                case Nfs41SlotState.BadSlot:
                    return BuildSingleResult(arguments.tag, nfsstat4.NFS4ERR_BADSLOT, BuildSequenceResult(sequenceArgs, nfsstat4.NFS4ERR_BADSLOT));
                case Nfs41SlotState.Misordered:
                    return BuildSingleResult(arguments.tag, nfsstat4.NFS4ERR_BAD_SEQID, BuildSequenceResult(sequenceArgs, nfsstat4.NFS4ERR_BAD_SEQID));
                case Nfs41SlotState.RetryUncached:
                    return BuildSingleResult(arguments.tag, nfsstat4.NFS4ERR_RETRY_UNCACHED_REP, BuildSequenceResult(sequenceArgs, nfsstat4.NFS4ERR_RETRY_UNCACHED_REP));
            }

            // For Replay we don't currently keep cached CB replies; the dispatcher returns the same shape
            // as a Fresh request would produce, which preserves protocol correctness for the typed surface.
            // Wire-level integration can later cache the encoded reply bytes for byte-stable replay.

            List<nfs_cb_resop4> results = new List<nfs_cb_resop4>(operations.Length);
            results.Add(BuildSequenceResult(sequenceArgs, nfsstat4.NFS4_OK));

            nfsstat4 finalStatus = nfsstat4.NFS4_OK;
            for (int index = 1; index < operations.Length; index++)
            {
                nfs_cb_argop4 op = operations[index];
                nfs_cb_resop4 result = await DispatchOpAsync(op, cancellationToken).ConfigureAwait(false);
                results.Add(result);

                nfsstat4? opStatus = ExtractStatus(result);
                if (opStatus != null && opStatus.Value != nfsstat4.NFS4_OK)
                {
                    finalStatus = opStatus.Value;
                    break;
                }
            }

            if (evaluation.State == Nfs41SlotState.Fresh)
            {
                backChannelSlotTable.RecordFreshRequest(slotId, sequenceId, sequenceArgs.csa_cachethis, ReadOnlyMemory<byte>.Empty);
            }

            return new CB_COMPOUND4res
            {
                status = finalStatus,
                tag = arguments.tag ?? EmptyTag(),
                resarray = results.ToArray(),
            };
        }

        private async Task<nfs_cb_resop4> DispatchOpAsync(nfs_cb_argop4 op, CancellationToken cancellationToken)
        {
            switch ((nfs_cb_opnum4)op.argop)
            {
                case nfs_cb_opnum4.OP_CB_RECALL:
                    CB_RECALL4res recallResult = await handler
                        .OnRecallAsync(op.opcbrecall ?? new CB_RECALL4args(), cancellationToken)
                        .ConfigureAwait(false);
                    return new nfs_cb_resop4 { resop = (uint)nfs_cb_opnum4.OP_CB_RECALL, opcbrecall = recallResult };

                case nfs_cb_opnum4.OP_CB_GETATTR:
                    CB_GETATTR4res getAttrResult = await handler
                        .OnGetAttributesAsync(op.opcbgetattr ?? new CB_GETATTR4args(), cancellationToken)
                        .ConfigureAwait(false);
                    return new nfs_cb_resop4 { resop = (uint)nfs_cb_opnum4.OP_CB_GETATTR, opcbgetattr = getAttrResult };

                case nfs_cb_opnum4.OP_CB_RECALL_ANY:
                    CB_RECALL_ANY4res recallAnyResult = await handler
                        .OnRecallAnyAsync(op.opcbrecall_any ?? new CB_RECALL_ANY4args(), cancellationToken)
                        .ConfigureAwait(false);
                    return new nfs_cb_resop4 { resop = (uint)nfs_cb_opnum4.OP_CB_RECALL_ANY, opcbrecall_any = recallAnyResult };

                default:
                    return new nfs_cb_resop4
                    {
                        resop = (uint)nfs_cb_opnum4.OP_CB_ILLEGAL,
                        opcbillegal = new CB_ILLEGAL4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                    };
            }
        }

        private static nfs_cb_resop4 BuildSequenceResult(CB_SEQUENCE4args sequenceArgs, nfsstat4 status)
        {
            CB_SEQUENCE4res sequenceResult = new CB_SEQUENCE4res
            {
                csr_status = status,
            };

            if (status == nfsstat4.NFS4_OK)
            {
                sequenceResult.csr_resok4 = new CB_SEQUENCE4resok
                {
                    csr_sessionid = sequenceArgs.csa_sessionid,
                    csr_sequenceid = sequenceArgs.csa_sequenceid,
                    csr_slotid = sequenceArgs.csa_slotid,
                    csr_highest_slotid = sequenceArgs.csa_highest_slotid,
                    csr_target_highest_slotid = sequenceArgs.csa_highest_slotid,
                };
            }

            return new nfs_cb_resop4
            {
                resop = (uint)nfs_cb_opnum4.OP_CB_SEQUENCE,
                opcbsequence = sequenceResult,
            };
        }

        private static CB_COMPOUND4res BuildBadSessionResult(utf8str_cs? tag)
        {
            return new CB_COMPOUND4res
            {
                status = nfsstat4.NFS4ERR_BADSESSION,
                tag = tag ?? EmptyTag(),
                resarray = Array.Empty<nfs_cb_resop4>(),
            };
        }

        private static CB_COMPOUND4res BuildSingleResult(utf8str_cs? tag, nfsstat4 status, nfs_cb_resop4 result)
        {
            return new CB_COMPOUND4res
            {
                status = status,
                tag = tag ?? EmptyTag(),
                resarray = new[] { result },
            };
        }

        private static utf8str_cs EmptyTag()
        {
            return new utf8str_cs
            {
                Value = new utf8string { Value = Array.Empty<byte>() },
            };
        }

        private static nfsstat4? ExtractStatus(nfs_cb_resop4 result)
        {
            return (nfs_cb_opnum4)result.resop switch
            {
                nfs_cb_opnum4.OP_CB_RECALL => result.opcbrecall?.status,
                nfs_cb_opnum4.OP_CB_GETATTR => result.opcbgetattr?.status,
                nfs_cb_opnum4.OP_CB_RECALL_ANY => result.opcbrecall_any?.crar_status,
                nfs_cb_opnum4.OP_CB_SEQUENCE => result.opcbsequence?.csr_status,
                nfs_cb_opnum4.OP_CB_ILLEGAL => result.opcbillegal?.status,
                _ => null,
            };
        }
    }
}
