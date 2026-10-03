namespace OpenNFS.Rpc.Telemetry
{
    using System;
    using System.Buffers.Binary;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Telemetry;

    /// <summary>
    /// Classifies server RPC replies into bounded outcome, status, and error-type label values without decoding the
    /// full procedure result: every NFS, MOUNT MNT, and NLM result this server produces starts with its status word.
    /// </summary>
    /// <remarks>Stateless and thread safe. Never throws for malformed payloads; it falls back to <c>other</c>.</remarks>
    internal static class OpenNfsRpcReplyClassifier
    {
        private const int NfsErrorIo = 5;
        private const int NfsErrorServerFault = 10006;
        private const int NlmFailed = 9;

        internal static OpenNfsRpcReplyClassification Classify(uint program, uint version, uint procedure, RpcMessageEnvelope reply)
        {
            reply_body? replyBody = reply.Header.body?.rbody;
            if (replyBody is null)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeRpcError, OpenNfsTelemetryNames.ValueUnknown, "invalid_reply", false);
            }

            if (replyBody.stat == reply_stat.MSG_DENIED)
            {
                rejected_reply? rejected = replyBody.rreply;
                if (rejected?.stat == reject_stat.AUTH_ERROR)
                {
                    return new OpenNfsRpcReplyClassification(
                        OpenNfsTelemetryNames.OutcomeRpcError,
                        ResolveAuthStatus(rejected.stat_value),
                        "AUTH_ERROR",
                        true);
                }

                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeRpcError, "RPC_MISMATCH", "RPC_MISMATCH", false);
            }

            accept_stat? acceptStatus = replyBody.areply?.reply_data?.stat;
            if (acceptStatus != accept_stat.SUCCESS)
            {
                string acceptName = ResolveAcceptStatus(acceptStatus);
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeRpcError, acceptName, acceptName, false);
            }

            ReadOnlySpan<byte> payload = reply.ProcedurePayload.Span;
            switch (program)
            {
                case OpenNfsRpcNameCatalog.NfsProgram:
                    if (procedure == 0U)
                    {
                        break;
                    }

                    if (version == 4U)
                    {
                        return FromStatus(payload, 0, OpenNfsRpcNameCatalog.ResolveNfs4Status, IsNfsServerFault);
                    }

                    return FromStatus(payload, 0, OpenNfsRpcNameCatalog.ResolveNfs3Status, IsNfsServerFault);

                case OpenNfsRpcNameCatalog.MountProgram:
                    if (procedure == 1U)
                    {
                        return FromStatus(payload, 0, OpenNfsRpcNameCatalog.ResolveMountStatus, IsNfsServerFault);
                    }

                    break;

                case OpenNfsRpcNameCatalog.NlmProgram:
                    if ((procedure >= 1U && procedure <= 5U) || (procedure >= 20U && procedure <= 22U))
                    {
                        return FromNlmStatus(payload);
                    }

                    break;
            }

            return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.ValueNone, null, false);
        }

        internal static string ResolveAuthFlavor(auth_flavor? flavor)
        {
            switch (flavor)
            {
                case null:
                case auth_flavor.AUTH_NONE:
                    return OpenNfsTelemetryNames.AuthFlavorNone;
                case auth_flavor.AUTH_SYS:
                    return OpenNfsTelemetryNames.AuthFlavorSys;
                case auth_flavor.RPCSEC_GSS:
                    return OpenNfsTelemetryNames.AuthFlavorRpcSecGss;
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        private static OpenNfsRpcReplyClassification FromStatus(
            ReadOnlySpan<byte> payload,
            int offset,
            Func<int, string> resolveName,
            Func<int, bool> isServerFault)
        {
            if (payload.Length < offset + 4)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.ValueOther, null, false);
            }

            int status = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4));
            string statusName = resolveName(status);
            if (status == 0)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, statusName, null, false);
            }

            return isServerFault(status)
                ? new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeServerError, statusName, statusName, false)
                : new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeNfsError, statusName, statusName, false);
        }

        private static OpenNfsRpcReplyClassification FromNlmStatus(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 4)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.ValueOther, null, false);
            }

            int cookieLength = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(0, 4));
            if (cookieLength < 0 || cookieLength > payload.Length)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.ValueOther, null, false);
            }

            int statusOffset = 4 + ((cookieLength + 3) & ~3);
            if (payload.Length < statusOffset + 4)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, OpenNfsTelemetryNames.ValueOther, null, false);
            }

            int status = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(statusOffset, 4));
            string statusName = OpenNfsRpcNameCatalog.ResolveNlmStatus(status);
            if (status == 0)
            {
                return new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeSuccess, statusName, null, false);
            }

            return status == NlmFailed
                ? new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeServerError, statusName, statusName, false)
                : new OpenNfsRpcReplyClassification(OpenNfsTelemetryNames.OutcomeNfsError, statusName, statusName, false);
        }

        private static bool IsNfsServerFault(int status)
        {
            return status == NfsErrorIo || status == NfsErrorServerFault;
        }

        private static string ResolveAcceptStatus(accept_stat? status)
        {
            switch (status)
            {
                case accept_stat.PROG_UNAVAIL:
                    return "PROG_UNAVAIL";
                case accept_stat.PROG_MISMATCH:
                    return "PROG_MISMATCH";
                case accept_stat.PROC_UNAVAIL:
                    return "PROC_UNAVAIL";
                case accept_stat.GARBAGE_ARGS:
                    return "GARBAGE_ARGS";
                case accept_stat.SYSTEM_ERR:
                    return "SYSTEM_ERR";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        private static string ResolveAuthStatus(auth_stat? status)
        {
            switch (status)
            {
                case auth_stat.AUTH_BADCRED:
                    return "AUTH_BADCRED";
                case auth_stat.AUTH_REJECTEDCRED:
                    return "AUTH_REJECTEDCRED";
                case auth_stat.AUTH_BADVERF:
                    return "AUTH_BADVERF";
                case auth_stat.AUTH_REJECTEDVERF:
                    return "AUTH_REJECTEDVERF";
                case auth_stat.AUTH_TOOWEAK:
                    return "AUTH_TOOWEAK";
                case auth_stat.AUTH_FAILED:
                    return "AUTH_FAILED";
                case auth_stat.RPCSEC_GSS_CREDPROBLEM:
                    return "RPCSEC_GSS_CREDPROBLEM";
                case auth_stat.RPCSEC_GSS_CTXPROBLEM:
                    return "RPCSEC_GSS_CTXPROBLEM";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }
    }
}
