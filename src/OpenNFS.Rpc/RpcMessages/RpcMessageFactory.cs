namespace OpenNFS.Rpc.RpcMessages
{
    using System;
    using OpenNFS.Rpc.Generated;

    /// <summary>
    /// Builds RFC 5531 message envelopes over the generated RPC header models.
    /// </summary>
    public static class RpcMessageFactory
    {
        /// <summary>
        /// Creates an ONC RPC call message envelope.
        /// </summary>
        /// <param name="xid">The transaction identifier.</param>
        /// <param name="program">The RPC program number.</param>
        /// <param name="version">The RPC program version number.</param>
        /// <param name="procedure">The RPC procedure number.</param>
        /// <param name="credential">The call credential envelope. Defaults to <c>AUTH_NONE</c> when omitted.</param>
        /// <param name="verifier">The call verifier envelope. Defaults to <c>AUTH_NONE</c> when omitted.</param>
        /// <param name="procedurePayload">The encoded procedure arguments that follow the RFC 5531 call header.</param>
        /// <returns>The assembled call envelope.</returns>
        public static RpcMessageEnvelope CreateCall(
            uint xid,
            uint program,
            uint version,
            uint procedure,
            opaque_auth? credential = null,
            opaque_auth? verifier = null,
            ReadOnlyMemory<byte> procedurePayload = default)
        {
            return new RpcMessageEnvelope(
                new rpc_msg
                {
                    xid = xid,
                    body = new rpc_msg_body
                    {
                        mtype = msg_type.CALL,
                        cbody = new call_body
                        {
                            rpcvers = RpcProtocolConstants.RpcVersion,
                            prog = program,
                            vers = version,
                            proc = procedure,
                            cred = credential ?? RpcAuthenticationCodec.CreateNone(),
                            verf = verifier ?? RpcAuthenticationCodec.CreateNone(),
                        },
                    },
                },
                procedurePayload);
        }

        /// <summary>
        /// Creates an accepted reply message envelope.
        /// </summary>
        /// <param name="xid">The transaction identifier.</param>
        /// <param name="status">The accepted reply status.</param>
        /// <param name="verifier">The reply verifier envelope. Defaults to <c>AUTH_NONE</c> when omitted.</param>
        /// <param name="procedurePayload">
        /// The encoded procedure result bytes.
        /// This payload is only allowed for <see cref="accept_stat.SUCCESS"/>.
        /// </param>
        /// <param name="mismatchLowVersion">The low supported version when <paramref name="status"/> is <see cref="accept_stat.PROG_MISMATCH"/>.</param>
        /// <param name="mismatchHighVersion">The high supported version when <paramref name="status"/> is <see cref="accept_stat.PROG_MISMATCH"/>.</param>
        /// <returns>The assembled accepted reply envelope.</returns>
        public static RpcMessageEnvelope CreateAcceptedReply(
            uint xid,
            accept_stat status,
            opaque_auth? verifier = null,
            ReadOnlyMemory<byte> procedurePayload = default,
            uint? mismatchLowVersion = null,
            uint? mismatchHighVersion = null)
        {
            accepted_reply_reply_data replyData = new accepted_reply_reply_data
            {
                stat = status,
            };

            switch (status)
            {
                case accept_stat.SUCCESS:
                    EnsureNoMismatchArguments(status, mismatchLowVersion, mismatchHighVersion);
                    replyData.results = Array.Empty<byte>();
                    break;
                case accept_stat.PROG_MISMATCH:
                    EnsureMismatchArguments(status, mismatchLowVersion, mismatchHighVersion);
                    EnsureNoProcedurePayload(status, procedurePayload);
                    replyData.mismatch_info = new accepted_reply_reply_data_mismatch_info
                    {
                        low = mismatchLowVersion!.Value,
                        high = mismatchHighVersion!.Value,
                    };
                    break;
                case accept_stat.PROG_UNAVAIL:
                case accept_stat.PROC_UNAVAIL:
                case accept_stat.GARBAGE_ARGS:
                case accept_stat.SYSTEM_ERR:
                    EnsureNoMismatchArguments(status, mismatchLowVersion, mismatchHighVersion);
                    EnsureNoProcedurePayload(status, procedurePayload);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported accepted reply status.");
            }

            return new RpcMessageEnvelope(
                new rpc_msg
                {
                    xid = xid,
                    body = new rpc_msg_body
                    {
                        mtype = msg_type.REPLY,
                        rbody = new reply_body
                        {
                            stat = reply_stat.MSG_ACCEPTED,
                            areply = new accepted_reply
                            {
                                verf = verifier ?? RpcAuthenticationCodec.CreateNone(),
                                reply_data = replyData,
                            },
                        },
                    },
                },
                procedurePayload);
        }

        /// <summary>
        /// Creates a rejected reply message envelope.
        /// </summary>
        /// <param name="xid">The transaction identifier.</param>
        /// <param name="status">The rejected reply status.</param>
        /// <param name="mismatchLowVersion">The low supported version when <paramref name="status"/> is <see cref="reject_stat.RPC_MISMATCH"/>.</param>
        /// <param name="mismatchHighVersion">The high supported version when <paramref name="status"/> is <see cref="reject_stat.RPC_MISMATCH"/>.</param>
        /// <param name="authenticationStatus">The authentication failure status when <paramref name="status"/> is <see cref="reject_stat.AUTH_ERROR"/>.</param>
        /// <returns>The assembled rejected reply envelope.</returns>
        public static RpcMessageEnvelope CreateRejectedReply(
            uint xid,
            reject_stat status,
            uint? mismatchLowVersion = null,
            uint? mismatchHighVersion = null,
            auth_stat? authenticationStatus = null)
        {
            rejected_reply rejectedReply = new rejected_reply
            {
                stat = status,
            };

            switch (status)
            {
                case reject_stat.RPC_MISMATCH:
                    EnsureMismatchArguments(status, mismatchLowVersion, mismatchHighVersion);
                    if (authenticationStatus.HasValue)
                    {
                        throw new ArgumentException(
                            "Authentication status may only be supplied for AUTH_ERROR replies.",
                            nameof(authenticationStatus));
                    }

                    rejectedReply.mismatch_info = new rejected_reply_mismatch_info
                    {
                        low = mismatchLowVersion!.Value,
                        high = mismatchHighVersion!.Value,
                    };
                    break;
                case reject_stat.AUTH_ERROR:
                    EnsureNoMismatchArguments(status, mismatchLowVersion, mismatchHighVersion);
                    if (!authenticationStatus.HasValue)
                    {
                        throw new ArgumentException(
                            "Authentication status is required for AUTH_ERROR replies.",
                            nameof(authenticationStatus));
                    }

                    rejectedReply.stat_value = authenticationStatus.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported rejected reply status.");
            }

            return new RpcMessageEnvelope(
                new rpc_msg
                {
                    xid = xid,
                    body = new rpc_msg_body
                    {
                        mtype = msg_type.REPLY,
                        rbody = new reply_body
                        {
                            stat = reply_stat.MSG_DENIED,
                            rreply = rejectedReply,
                        },
                    },
                });
        }

        private static void EnsureMismatchArguments<TStatus>(TStatus status, uint? mismatchLowVersion, uint? mismatchHighVersion)
            where TStatus : struct
        {
            if (!mismatchLowVersion.HasValue || !mismatchHighVersion.HasValue)
            {
                throw new ArgumentException(
                    "Status '" + status.ToString() + "' requires both low and high version values.");
            }
        }

        private static void EnsureNoMismatchArguments<TStatus>(TStatus status, uint? mismatchLowVersion, uint? mismatchHighVersion)
            where TStatus : struct
        {
            if (mismatchLowVersion.HasValue || mismatchHighVersion.HasValue)
            {
                throw new ArgumentException(
                    "Status '" + status.ToString() + "' does not allow version mismatch values.");
            }
        }

        private static void EnsureNoProcedurePayload(accept_stat status, ReadOnlyMemory<byte> procedurePayload)
        {
            if (procedurePayload.Length != 0)
            {
                throw new ArgumentException(
                    "Accepted reply status '" + status.ToString() + "' does not allow a procedure payload.",
                    nameof(procedurePayload));
            }
        }
    }
}
