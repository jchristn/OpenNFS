namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V41.Generated;

    internal static class OpenNfsV41SessionProtocol
    {
        internal static int CountSuccessfulOperations(OpenNfsV41CompoundOutcome outcome)
        {
            nfs_resop4[] results = outcome.Response.resarray ?? Array.Empty<nfs_resop4>();
            int count = 0;
            for (int index = 0; index < results.Length; index++)
            {
                if (GetOpStatus(results[index]) == nfsstat4.NFS4_OK)
                {
                    count++;
                }
            }

            return count;
        }

        internal static nfsstat4? GetOpStatus(nfs_resop4 result)
        {
            return result.resop switch
            {
                nfs_opnum4.OP_SEQUENCE => result.opsequence?.sr_status,
                nfs_opnum4.OP_EXCHANGE_ID => result.opexchange_id?.eir_status,
                nfs_opnum4.OP_CREATE_SESSION => result.opcreate_session?.csr_status,
                nfs_opnum4.OP_DESTROY_SESSION => result.opdestroy_session?.dsr_status,
                nfs_opnum4.OP_DESTROY_CLIENTID => result.opdestroy_clientid?.dcr_status,
                nfs_opnum4.OP_BIND_CONN_TO_SESSION => result.opbind_conn_to_session?.bctsr_status,
                nfs_opnum4.OP_ILLEGAL => result.opillegal?.status,
                _ => null,
            };
        }

        internal static bool IsTransportFailure(Exception exception)
        {
            return exception is System.IO.IOException
                || exception is System.Net.Sockets.SocketException
                || exception is ObjectDisposedException;
        }

        internal static nfs_argop4[] BuildArgArrayWithSequence(
            byte[] sessionId,
            OpenNfsV41SequenceLease lease,
            bool cacheReply,
            IReadOnlyList<nfs_argop4> operations)
        {
            nfs_argop4 sequenceOp = new nfs_argop4
            {
                argop = nfs_opnum4.OP_SEQUENCE,
                opsequence = new SEQUENCE4args
                {
                    sa_sessionid = new sessionid4 { Value = sessionId },
                    sa_sequenceid = new sequenceid4 { Value = lease.SequenceId },
                    sa_slotid = new slotid4 { Value = lease.SlotId },
                    sa_highest_slotid = new slotid4 { Value = lease.HighestSlotId },
                    sa_cachethis = cacheReply,
                },
            };

            nfs_argop4[] argarray = new nfs_argop4[operations.Count + 1];
            argarray[0] = sequenceOp;
            for (int index = 0; index < operations.Count; index++)
            {
                argarray[index + 1] = operations[index];
            }

            return argarray;
        }

        internal static COMPOUND4args BuildBindConnectionCompound(string tag, byte[] sessionId)
        {
            return new COMPOUND4args
            {
                tag = MakeTag(tag),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_BIND_CONN_TO_SESSION,
                        opbind_conn_to_session = new BIND_CONN_TO_SESSION4args
                        {
                            bctsa_sessid = new sessionid4 { Value = sessionId },
                            bctsa_dir = channel_dir_from_client4.CDFC4_FORE,
                            bctsa_use_conn_in_rdma_mode = false,
                        },
                    },
                },
            };
        }

        internal static COMPOUND4args BuildDestroySessionCompound(byte[] sessionId, ulong clientId)
        {
            return new COMPOUND4args
            {
                tag = MakeTag("client-destroy-session"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_DESTROY_SESSION,
                        opdestroy_session = new DESTROY_SESSION4args
                        {
                            dsa_sessionid = new sessionid4 { Value = sessionId },
                        },
                    },
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_DESTROY_CLIENTID,
                        opdestroy_clientid = new DESTROY_CLIENTID4args
                        {
                            dca_clientid = new clientid4 { Value = clientId },
                        },
                    },
                },
            };
        }

        internal static EXCHANGE_ID4args BuildExchangeIdArguments(OpenNfsV41ClientOwner owner)
        {
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4 { Value = owner.GetVerifier() },
                    co_ownerid = owner.GetOwnerId(),
                },
                eia_flags = 0,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        internal static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            channel_attrs4 attrs = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0 },
                ca_maxrequestsize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new count4 { Value = 64 * 1024 },
                ca_maxoperations = new count4 { Value = 16 },
                ca_maxrequests = new count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4 { Value = clientId },
                csa_sequence = new sequenceid4 { Value = sequenceId },
                csa_flags = 0,
                csa_fore_chan_attrs = attrs,
                csa_back_chan_attrs = attrs,
                csa_cb_program = 0x40000000u,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        internal static utf8str_cs MakeTag(string text)
        {
            return new utf8str_cs
            {
                Value = new utf8string { Value = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty) },
            };
        }

        internal static void EnsureCompoundOk(COMPOUND4res response, string operationName)
        {
            if (response.status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException(
                    "NFSv4.1 " + operationName + " COMPOUND returned " + response.status?.ToString() + ".");
            }

            if (response.resarray is null || response.resarray.Length == 0)
            {
                throw new InvalidOperationException(
                    "NFSv4.1 " + operationName + " COMPOUND returned an empty result array.");
            }
        }
    }
}
