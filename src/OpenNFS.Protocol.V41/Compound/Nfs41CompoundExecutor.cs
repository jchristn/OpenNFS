namespace OpenNFS.Protocol.V41.Compound
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;

    /// <summary>
    /// Executes a decoded NFSv4.1 <c>COMPOUND</c> against the session-management processor and surfaces
    /// the typed result. Operations outside the session-management surface return
    /// <see cref="nfsstat4.NFS4ERR_NOTSUPP"/> for now; subsequent Phase 10 milestones add real handlers.
    /// </summary>
    internal sealed class Nfs41CompoundExecutor
    {
        /// <summary>
        /// Gets the supported NFSv4 minor version. RFC 8881 defines v4.1 as minor version 1.
        /// </summary>
        internal const uint SupportedMinorVersion = 1;

        private readonly Nfs41SessionOperationProcessor processor;

        internal Nfs41CompoundExecutor(Nfs41SessionOperationProcessor processor)
        {
            ArgumentNullException.ThrowIfNull(processor);

            this.processor = processor;
        }

        internal COMPOUND4res Execute(COMPOUND4args arguments, Nfs41OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(context);

            if (arguments.minorversion != SupportedMinorVersion)
            {
                return new COMPOUND4res
                {
                    status = nfsstat4.NFS4ERR_MINOR_VERS_MISMATCH,
                    tag = arguments.tag ?? new utf8str_cs { Value = new utf8string { Value = Array.Empty<byte>() } },
                    resarray = Array.Empty<nfs_resop4>(),
                };
            }

            nfs_argop4[] operations = arguments.argarray ?? Array.Empty<nfs_argop4>();
            List<nfs_resop4> results = new List<nfs_resop4>(operations.Length);
            nfsstat4 finalStatus = nfsstat4.NFS4_OK;
            Nfs41SequenceOutcome? freshSequenceOutcome = null;
            bool hasObservedSequence = false;
            bool replayDetected = false;

            for (int index = 0; index < operations.Length; index++)
            {
                nfs_argop4 op = operations[index];
                nfs_opnum4? opnum = op.argop;
                nfs_resop4 result;

                if (!hasObservedSequence
                    && opnum != nfs_opnum4.OP_SEQUENCE
                    && opnum != nfs_opnum4.OP_EXCHANGE_ID
                    && opnum != nfs_opnum4.OP_CREATE_SESSION
                    && opnum != nfs_opnum4.OP_BIND_CONN_TO_SESSION
                    && opnum != nfs_opnum4.OP_DESTROY_SESSION
                    && opnum != nfs_opnum4.OP_DESTROY_CLIENTID)
                {
                    finalStatus = nfsstat4.NFS4ERR_OP_NOT_IN_SESSION;
                    results.Add(BuildOpNotInSessionResult(opnum));
                    break;
                }

                switch (opnum)
                {
                    case nfs_opnum4.OP_EXCHANGE_ID:
                        result = WrapExchangeId(processor.ProcessExchangeId(op.opexchange_id ?? new EXCHANGE_ID4args()));
                        break;
                    case nfs_opnum4.OP_CREATE_SESSION:
                        result = WrapCreateSession(processor.ProcessCreateSession(op.opcreate_session ?? new CREATE_SESSION4args(), context));
                        break;
                    case nfs_opnum4.OP_DESTROY_SESSION:
                        result = WrapDestroySession(processor.ProcessDestroySession(op.opdestroy_session ?? new DESTROY_SESSION4args()));
                        break;
                    case nfs_opnum4.OP_DESTROY_CLIENTID:
                        result = WrapDestroyClientId(processor.ProcessDestroyClientId(op.opdestroy_clientid ?? new DESTROY_CLIENTID4args()));
                        break;
                    case nfs_opnum4.OP_BIND_CONN_TO_SESSION:
                        result = WrapBindConnToSession(processor.ProcessBindConnToSession(op.opbind_conn_to_session ?? new BIND_CONN_TO_SESSION4args(), context));
                        break;
                    case nfs_opnum4.OP_SEQUENCE:
                        Nfs41SequenceOutcome outcome = processor.ProcessSequence(op.opsequence ?? new SEQUENCE4args(), context);
                        hasObservedSequence = true;
                        result = WrapSequence(outcome.Result);
                        if (outcome.State == Nfs41SlotState.Replay)
                        {
                            replayDetected = true;
                        }
                        else if (outcome.State == Nfs41SlotState.Fresh)
                        {
                            freshSequenceOutcome = outcome;
                        }
                        else
                        {
                            finalStatus = outcome.Result.sr_status ?? nfsstat4.NFS4ERR_INVAL;
                            results.Add(result);
                            return new COMPOUND4res
                            {
                                status = finalStatus,
                                tag = arguments.tag ?? new utf8str_cs { Value = new utf8string { Value = Array.Empty<byte>() } },
                                resarray = results.ToArray(),
                            };
                        }

                        break;
                    default:
                        result = new nfs_resop4
                        {
                            resop = nfs_opnum4.OP_ILLEGAL,
                            opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_NOTSUPP },
                        };
                        finalStatus = nfsstat4.NFS4ERR_NOTSUPP;
                        results.Add(result);
                        return new COMPOUND4res
                        {
                            status = finalStatus,
                            tag = arguments.tag ?? new utf8str_cs { Value = new utf8string { Value = Array.Empty<byte>() } },
                            resarray = results.ToArray(),
                        };
                }

                results.Add(result);

                nfsstat4? opStatus = ExtractStatus(result);
                if (opStatus != null && opStatus.Value != nfsstat4.NFS4_OK)
                {
                    finalStatus = opStatus.Value;
                    break;
                }
            }

            COMPOUND4res response = new COMPOUND4res
            {
                status = finalStatus,
                tag = arguments.tag ?? new utf8str_cs { Value = new utf8string { Value = Array.Empty<byte>() } },
                resarray = results.ToArray(),
            };

            if (replayDetected)
            {
                return response;
            }

            if (freshSequenceOutcome is not null && freshSequenceOutcome.CacheRequested)
            {
                byte[] replyBytes = Nfs41CompoundPayloadCodec.EncodeCompoundResult(response);
                processor.RecordSequenceReply(freshSequenceOutcome, replyBytes);
            }
            else if (freshSequenceOutcome is not null)
            {
                processor.RecordSequenceReply(freshSequenceOutcome, ReadOnlyMemory<byte>.Empty);
            }

            return response;
        }

        private static nfs_resop4 BuildOpNotInSessionResult(nfs_opnum4? opnum)
        {
            // The requested opcode could not be processed because no SEQUENCE preceded it. Encoding the
            // rejection in each op's result struct would require a generic shaper across the entire v4.1
            // operation surface. RFC 8881 §15.2 places the authoritative status in the COMPOUND-level
            // status field, so the per-op slot uses OP_ILLEGAL with the same status to keep the XDR
            // wire-shape valid; downstream consumers should consult <c>COMPOUND4res.status</c>.
            _ = opnum;
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_ILLEGAL,
                opillegal = new ILLEGAL4res { status = nfsstat4.NFS4ERR_OP_NOT_IN_SESSION },
            };
        }

        private static nfs_resop4 WrapExchangeId(EXCHANGE_ID4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_EXCHANGE_ID,
                opexchange_id = value,
            };
        }

        private static nfs_resop4 WrapCreateSession(CREATE_SESSION4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_CREATE_SESSION,
                opcreate_session = value,
            };
        }

        private static nfs_resop4 WrapDestroySession(DESTROY_SESSION4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_DESTROY_SESSION,
                opdestroy_session = value,
            };
        }

        private static nfs_resop4 WrapDestroyClientId(DESTROY_CLIENTID4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_DESTROY_CLIENTID,
                opdestroy_clientid = value,
            };
        }

        private static nfs_resop4 WrapBindConnToSession(BIND_CONN_TO_SESSION4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                opbind_conn_to_session = value,
            };
        }

        private static nfs_resop4 WrapSequence(SEQUENCE4res value)
        {
            return new nfs_resop4
            {
                resop = nfs_opnum4.OP_SEQUENCE,
                opsequence = value,
            };
        }

        private static nfsstat4? ExtractStatus(nfs_resop4 result)
        {
            return result.resop switch
            {
                nfs_opnum4.OP_EXCHANGE_ID => result.opexchange_id?.eir_status,
                nfs_opnum4.OP_CREATE_SESSION => result.opcreate_session?.csr_status,
                nfs_opnum4.OP_DESTROY_SESSION => result.opdestroy_session?.dsr_status,
                nfs_opnum4.OP_DESTROY_CLIENTID => result.opdestroy_clientid?.dcr_status,
                nfs_opnum4.OP_BIND_CONN_TO_SESSION => result.opbind_conn_to_session?.bctsr_status,
                nfs_opnum4.OP_SEQUENCE => result.opsequence?.sr_status,
                nfs_opnum4.OP_ILLEGAL => result.opillegal?.status,
                _ => null,
            };
        }
    }
}
