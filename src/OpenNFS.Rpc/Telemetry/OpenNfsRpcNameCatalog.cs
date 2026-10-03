namespace OpenNFS.Rpc.Telemetry
{
    using OpenNFS.Telemetry;

    /// <summary>
    /// Maps ONC RPC program, version, procedure, and protocol status numbers to the bounded label values used on
    /// OpenNFS metrics. Every method returns a compile-time constant string, so a malformed or hostile request can
    /// never introduce a new label value: unknown numbers collapse to <see cref="OpenNfsTelemetryNames.ValueUnknown"/>
    /// or <see cref="OpenNfsTelemetryNames.ValueOther"/>.
    /// </summary>
    /// <remarks>
    /// Generated from the XDR-generated program and status definitions in the protocol projects. Thread safe.
    /// </remarks>
    internal static class OpenNfsRpcNameCatalog
    {
        internal const uint NfsProgram = 100003U;
        internal const uint MountProgram = 100005U;
        internal const uint NlmProgram = 100021U;
        internal const uint NsmProgram = 100024U;
        internal const uint PortmapProgram = 100000U;

        internal static string ResolveService(uint program)
        {
            switch (program)
            {
                case NfsProgram:
                    return OpenNfsTelemetryNames.ServiceNfs;
                case MountProgram:
                    return OpenNfsTelemetryNames.ServiceMount;
                case NlmProgram:
                    return OpenNfsTelemetryNames.ServiceNlm;
                case NsmProgram:
                    return OpenNfsTelemetryNames.ServiceNsm;
                case PortmapProgram:
                    return OpenNfsTelemetryNames.ServicePortmap;
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        internal static string ResolveVersion(uint version)
        {
            switch (version)
            {
                case 1U:
                    return "1";
                case 2U:
                    return "2";
                case 3U:
                    return "3";
                case 4U:
                    return "4";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        internal static string ResolveMethod(uint program, uint version, uint procedure)
        {
            switch (program)
            {
                case NfsProgram:
                    return version == 4U ? ResolveNfs4Procedure(procedure) : ResolveNfs3Procedure(procedure);
                case MountProgram:
                    return ResolveMountProcedure(procedure);
                case NlmProgram:
                    return ResolveNlmProcedure(procedure);
                case NsmProgram:
                    return ResolveNsmProcedure(procedure);
                case PortmapProgram:
                    return version == 2U ? ResolvePortmapProcedure(procedure) : ResolveRpcbindProcedure(procedure);
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        internal static string ResolveNfs3Status(int status)
        {
            switch (status)
            {
                case 0:
                    return "NFS3_OK";
                case 1:
                    return "NFS3ERR_PERM";
                case 2:
                    return "NFS3ERR_NOENT";
                case 5:
                    return "NFS3ERR_IO";
                case 6:
                    return "NFS3ERR_NXIO";
                case 13:
                    return "NFS3ERR_ACCES";
                case 17:
                    return "NFS3ERR_EXIST";
                case 18:
                    return "NFS3ERR_XDEV";
                case 19:
                    return "NFS3ERR_NODEV";
                case 20:
                    return "NFS3ERR_NOTDIR";
                case 21:
                    return "NFS3ERR_ISDIR";
                case 22:
                    return "NFS3ERR_INVAL";
                case 27:
                    return "NFS3ERR_FBIG";
                case 28:
                    return "NFS3ERR_NOSPC";
                case 30:
                    return "NFS3ERR_ROFS";
                case 31:
                    return "NFS3ERR_MLINK";
                case 63:
                    return "NFS3ERR_NAMETOOLONG";
                case 66:
                    return "NFS3ERR_NOTEMPTY";
                case 69:
                    return "NFS3ERR_DQUOT";
                case 70:
                    return "NFS3ERR_STALE";
                case 71:
                    return "NFS3ERR_REMOTE";
                case 10001:
                    return "NFS3ERR_BADHANDLE";
                case 10002:
                    return "NFS3ERR_NOT_SYNC";
                case 10003:
                    return "NFS3ERR_BAD_COOKIE";
                case 10004:
                    return "NFS3ERR_NOTSUPP";
                case 10005:
                    return "NFS3ERR_TOOSMALL";
                case 10006:
                    return "NFS3ERR_SERVERFAULT";
                case 10007:
                    return "NFS3ERR_BADTYPE";
                case 10008:
                    return "NFS3ERR_JUKEBOX";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        internal static string ResolveNfs4Status(int status)
        {
            switch (status)
            {
                case 0:
                    return "NFS4_OK";
                case 1:
                    return "NFS4ERR_PERM";
                case 2:
                    return "NFS4ERR_NOENT";
                case 5:
                    return "NFS4ERR_IO";
                case 6:
                    return "NFS4ERR_NXIO";
                case 13:
                    return "NFS4ERR_ACCESS";
                case 17:
                    return "NFS4ERR_EXIST";
                case 18:
                    return "NFS4ERR_XDEV";
                case 20:
                    return "NFS4ERR_NOTDIR";
                case 21:
                    return "NFS4ERR_ISDIR";
                case 22:
                    return "NFS4ERR_INVAL";
                case 27:
                    return "NFS4ERR_FBIG";
                case 28:
                    return "NFS4ERR_NOSPC";
                case 30:
                    return "NFS4ERR_ROFS";
                case 31:
                    return "NFS4ERR_MLINK";
                case 63:
                    return "NFS4ERR_NAMETOOLONG";
                case 66:
                    return "NFS4ERR_NOTEMPTY";
                case 69:
                    return "NFS4ERR_DQUOT";
                case 70:
                    return "NFS4ERR_STALE";
                case 10001:
                    return "NFS4ERR_BADHANDLE";
                case 10003:
                    return "NFS4ERR_BAD_COOKIE";
                case 10004:
                    return "NFS4ERR_NOTSUPP";
                case 10005:
                    return "NFS4ERR_TOOSMALL";
                case 10006:
                    return "NFS4ERR_SERVERFAULT";
                case 10007:
                    return "NFS4ERR_BADTYPE";
                case 10008:
                    return "NFS4ERR_DELAY";
                case 10009:
                    return "NFS4ERR_SAME";
                case 10010:
                    return "NFS4ERR_DENIED";
                case 10011:
                    return "NFS4ERR_EXPIRED";
                case 10012:
                    return "NFS4ERR_LOCKED";
                case 10013:
                    return "NFS4ERR_GRACE";
                case 10014:
                    return "NFS4ERR_FHEXPIRED";
                case 10015:
                    return "NFS4ERR_SHARE_DENIED";
                case 10016:
                    return "NFS4ERR_WRONGSEC";
                case 10017:
                    return "NFS4ERR_CLID_INUSE";
                case 10018:
                    return "NFS4ERR_RESOURCE";
                case 10019:
                    return "NFS4ERR_MOVED";
                case 10020:
                    return "NFS4ERR_NOFILEHANDLE";
                case 10021:
                    return "NFS4ERR_MINOR_VERS_MISMATCH";
                case 10022:
                    return "NFS4ERR_STALE_CLIENTID";
                case 10023:
                    return "NFS4ERR_STALE_STATEID";
                case 10024:
                    return "NFS4ERR_OLD_STATEID";
                case 10025:
                    return "NFS4ERR_BAD_STATEID";
                case 10026:
                    return "NFS4ERR_BAD_SEQID";
                case 10027:
                    return "NFS4ERR_NOT_SAME";
                case 10028:
                    return "NFS4ERR_LOCK_RANGE";
                case 10029:
                    return "NFS4ERR_SYMLINK";
                case 10030:
                    return "NFS4ERR_RESTOREFH";
                case 10031:
                    return "NFS4ERR_LEASE_MOVED";
                case 10032:
                    return "NFS4ERR_ATTRNOTSUPP";
                case 10033:
                    return "NFS4ERR_NO_GRACE";
                case 10034:
                    return "NFS4ERR_RECLAIM_BAD";
                case 10035:
                    return "NFS4ERR_RECLAIM_CONFLICT";
                case 10036:
                    return "NFS4ERR_BADXDR";
                case 10037:
                    return "NFS4ERR_LOCKS_HELD";
                case 10038:
                    return "NFS4ERR_OPENMODE";
                case 10039:
                    return "NFS4ERR_BADOWNER";
                case 10040:
                    return "NFS4ERR_BADCHAR";
                case 10041:
                    return "NFS4ERR_BADNAME";
                case 10042:
                    return "NFS4ERR_BAD_RANGE";
                case 10043:
                    return "NFS4ERR_LOCK_NOTSUPP";
                case 10044:
                    return "NFS4ERR_OP_ILLEGAL";
                case 10045:
                    return "NFS4ERR_DEADLOCK";
                case 10046:
                    return "NFS4ERR_FILE_OPEN";
                case 10047:
                    return "NFS4ERR_ADMIN_REVOKED";
                case 10048:
                    return "NFS4ERR_CB_PATH_DOWN";
                case 10049:
                    return "NFS4ERR_BADIOMODE";
                case 10050:
                    return "NFS4ERR_BADLAYOUT";
                case 10051:
                    return "NFS4ERR_BAD_SESSION_DIGEST";
                case 10052:
                    return "NFS4ERR_BADSESSION";
                case 10053:
                    return "NFS4ERR_BADSLOT";
                case 10054:
                    return "NFS4ERR_COMPLETE_ALREADY";
                case 10055:
                    return "NFS4ERR_CONN_NOT_BOUND_TO_SESSION";
                case 10056:
                    return "NFS4ERR_DELEG_ALREADY_WANTED";
                case 10057:
                    return "NFS4ERR_BACK_CHAN_BUSY";
                case 10058:
                    return "NFS4ERR_LAYOUTTRYLATER";
                case 10059:
                    return "NFS4ERR_LAYOUTUNAVAILABLE";
                case 10060:
                    return "NFS4ERR_NOMATCHING_LAYOUT";
                case 10061:
                    return "NFS4ERR_RECALLCONFLICT";
                case 10062:
                    return "NFS4ERR_UNKNOWN_LAYOUTTYPE";
                case 10063:
                    return "NFS4ERR_SEQ_MISORDERED";
                case 10064:
                    return "NFS4ERR_SEQUENCE_POS";
                case 10065:
                    return "NFS4ERR_REQ_TOO_BIG";
                case 10066:
                    return "NFS4ERR_REP_TOO_BIG";
                case 10067:
                    return "NFS4ERR_REP_TOO_BIG_TO_CACHE";
                case 10068:
                    return "NFS4ERR_RETRY_UNCACHED_REP";
                case 10069:
                    return "NFS4ERR_UNSAFE_COMPOUND";
                case 10070:
                    return "NFS4ERR_TOO_MANY_OPS";
                case 10071:
                    return "NFS4ERR_OP_NOT_IN_SESSION";
                case 10072:
                    return "NFS4ERR_HASH_ALG_UNSUPP";
                case 10074:
                    return "NFS4ERR_CLIENTID_BUSY";
                case 10075:
                    return "NFS4ERR_PNFS_IO_HOLE";
                case 10076:
                    return "NFS4ERR_SEQ_FALSE_RETRY";
                case 10077:
                    return "NFS4ERR_BAD_HIGH_SLOT";
                case 10078:
                    return "NFS4ERR_DEADSESSION";
                case 10079:
                    return "NFS4ERR_ENCR_ALG_UNSUPP";
                case 10080:
                    return "NFS4ERR_PNFS_NO_LAYOUT";
                case 10081:
                    return "NFS4ERR_NOT_ONLY_OP";
                case 10082:
                    return "NFS4ERR_WRONG_CRED";
                case 10083:
                    return "NFS4ERR_WRONG_TYPE";
                case 10084:
                    return "NFS4ERR_DIRDELEG_UNAVAIL";
                case 10085:
                    return "NFS4ERR_REJECT_DELEG";
                case 10086:
                    return "NFS4ERR_RETURNCONFLICT";
                case 10087:
                    return "NFS4ERR_DELEG_REVOKED";
                case 10088:
                    return "NFS4ERR_PARTNER_NOTSUPP";
                case 10089:
                    return "NFS4ERR_PARTNER_NO_AUTH";
                case 10090:
                    return "NFS4ERR_UNION_NOTSUPP";
                case 10091:
                    return "NFS4ERR_OFFLOAD_DENIED";
                case 10092:
                    return "NFS4ERR_WRONG_LFS";
                case 10093:
                    return "NFS4ERR_BADLABEL";
                case 10094:
                    return "NFS4ERR_OFFLOAD_NO_REQS";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        internal static string ResolveMountStatus(int status)
        {
            switch (status)
            {
                case 0:
                    return "MNT3_OK";
                case 1:
                    return "MNT3ERR_PERM";
                case 2:
                    return "MNT3ERR_NOENT";
                case 5:
                    return "MNT3ERR_IO";
                case 13:
                    return "MNT3ERR_ACCES";
                case 20:
                    return "MNT3ERR_NOTDIR";
                case 22:
                    return "MNT3ERR_INVAL";
                case 63:
                    return "MNT3ERR_NAMETOOLONG";
                case 10004:
                    return "MNT3ERR_NOTSUPP";
                case 10006:
                    return "MNT3ERR_SERVERFAULT";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        internal static string ResolveNlmStatus(int status)
        {
            switch (status)
            {
                case 0:
                    return "NLM4_GRANTED";
                case 1:
                    return "NLM4_DENIED";
                case 2:
                    return "NLM4_DENIED_NOLOCKS";
                case 3:
                    return "NLM4_BLOCKED";
                case 4:
                    return "NLM4_DENIED_GRACE_PERIOD";
                case 5:
                    return "NLM4_DEADLCK";
                case 6:
                    return "NLM4_ROFS";
                case 7:
                    return "NLM4_STALE_FH";
                case 8:
                    return "NLM4_FBIG";
                case 9:
                    return "NLM4_FAILED";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        internal static string ResolveNfs4Operation(int operation)
        {
            switch (operation)
            {
                case 3:
                    return "ACCESS";
                case 4:
                    return "CLOSE";
                case 5:
                    return "COMMIT";
                case 6:
                    return "CREATE";
                case 7:
                    return "DELEGPURGE";
                case 8:
                    return "DELEGRETURN";
                case 9:
                    return "GETATTR";
                case 10:
                    return "GETFH";
                case 11:
                    return "LINK";
                case 12:
                    return "LOCK";
                case 13:
                    return "LOCKT";
                case 14:
                    return "LOCKU";
                case 15:
                    return "LOOKUP";
                case 16:
                    return "LOOKUPP";
                case 17:
                    return "NVERIFY";
                case 18:
                    return "OPEN";
                case 19:
                    return "OPENATTR";
                case 20:
                    return "OPEN_CONFIRM";
                case 21:
                    return "OPEN_DOWNGRADE";
                case 22:
                    return "PUTFH";
                case 23:
                    return "PUTPUBFH";
                case 24:
                    return "PUTROOTFH";
                case 25:
                    return "READ";
                case 26:
                    return "READDIR";
                case 27:
                    return "READLINK";
                case 28:
                    return "REMOVE";
                case 29:
                    return "RENAME";
                case 30:
                    return "RENEW";
                case 31:
                    return "RESTOREFH";
                case 32:
                    return "SAVEFH";
                case 33:
                    return "SECINFO";
                case 34:
                    return "SETATTR";
                case 35:
                    return "SETCLIENTID";
                case 36:
                    return "SETCLIENTID_CONFIRM";
                case 37:
                    return "VERIFY";
                case 38:
                    return "WRITE";
                case 39:
                    return "RELEASE_LOCKOWNER";
                case 40:
                    return "BACKCHANNEL_CTL";
                case 41:
                    return "BIND_CONN_TO_SESSION";
                case 42:
                    return "EXCHANGE_ID";
                case 43:
                    return "CREATE_SESSION";
                case 44:
                    return "DESTROY_SESSION";
                case 45:
                    return "FREE_STATEID";
                case 46:
                    return "GET_DIR_DELEGATION";
                case 47:
                    return "GETDEVICEINFO";
                case 48:
                    return "GETDEVICELIST";
                case 49:
                    return "LAYOUTCOMMIT";
                case 50:
                    return "LAYOUTGET";
                case 51:
                    return "LAYOUTRETURN";
                case 52:
                    return "SECINFO_NO_NAME";
                case 53:
                    return "SEQUENCE";
                case 54:
                    return "SET_SSV";
                case 55:
                    return "TEST_STATEID";
                case 56:
                    return "WANT_DELEGATION";
                case 57:
                    return "DESTROY_CLIENTID";
                case 58:
                    return "RECLAIM_COMPLETE";
                case 59:
                    return "ALLOCATE";
                case 60:
                    return "COPY";
                case 61:
                    return "COPY_NOTIFY";
                case 62:
                    return "DEALLOCATE";
                case 63:
                    return "IO_ADVISE";
                case 64:
                    return "LAYOUTERROR";
                case 65:
                    return "LAYOUTSTATS";
                case 66:
                    return "OFFLOAD_CANCEL";
                case 67:
                    return "OFFLOAD_STATUS";
                case 68:
                    return "READ_PLUS";
                case 69:
                    return "SEEK";
                case 70:
                    return "WRITE_SAME";
                case 71:
                    return "CLONE";
                case 10044:
                    return "ILLEGAL";
                default:
                    return OpenNfsTelemetryNames.ValueOther;
            }
        }

        private static string ResolveNfs3Procedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "GETATTR";
                case 2U:
                    return "SETATTR";
                case 3U:
                    return "LOOKUP";
                case 4U:
                    return "ACCESS";
                case 5U:
                    return "READLINK";
                case 6U:
                    return "READ";
                case 7U:
                    return "WRITE";
                case 8U:
                    return "CREATE";
                case 9U:
                    return "MKDIR";
                case 10U:
                    return "SYMLINK";
                case 11U:
                    return "MKNOD";
                case 12U:
                    return "REMOVE";
                case 13U:
                    return "RMDIR";
                case 14U:
                    return "RENAME";
                case 15U:
                    return "LINK";
                case 16U:
                    return "READDIR";
                case 17U:
                    return "READDIRPLUS";
                case 18U:
                    return "FSSTAT";
                case 19U:
                    return "FSINFO";
                case 20U:
                    return "PATHCONF";
                case 21U:
                    return "COMMIT";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolveNfs4Procedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "COMPOUND";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolveMountProcedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "MNT";
                case 2U:
                    return "DUMP";
                case 3U:
                    return "UMNT";
                case 4U:
                    return "UMNTALL";
                case 5U:
                    return "EXPORT";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolveNlmProcedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "TEST";
                case 2U:
                    return "LOCK";
                case 3U:
                    return "CANCEL";
                case 4U:
                    return "UNLOCK";
                case 5U:
                    return "GRANTED";
                case 6U:
                    return "TEST_MSG";
                case 7U:
                    return "LOCK_MSG";
                case 8U:
                    return "CANCEL_MSG";
                case 9U:
                    return "UNLOCK_MSG";
                case 10U:
                    return "GRANTED_MSG";
                case 11U:
                    return "TEST_RES";
                case 12U:
                    return "LOCK_RES";
                case 13U:
                    return "CANCEL_RES";
                case 14U:
                    return "UNLOCK_RES";
                case 15U:
                    return "GRANTED_RES";
                case 20U:
                    return "SHARE";
                case 21U:
                    return "UNSHARE";
                case 22U:
                    return "NM_LOCK";
                case 23U:
                    return "FREE_ALL";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolveNsmProcedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "STAT";
                case 2U:
                    return "MON";
                case 3U:
                    return "UNMON";
                case 4U:
                    return "UNMON_ALL";
                case 5U:
                    return "SIMU_CRASH";
                case 6U:
                    return "NOTIFY";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolvePortmapProcedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "SET";
                case 2U:
                    return "UNSET";
                case 3U:
                    return "GETPORT";
                case 4U:
                    return "DUMP";
                case 5U:
                    return "CALLIT";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }

        private static string ResolveRpcbindProcedure(uint procedure)
        {
            switch (procedure)
            {
                case 0U:
                    return "NULL";
                case 1U:
                    return "SET";
                case 2U:
                    return "UNSET";
                case 3U:
                    return "GETADDR";
                case 4U:
                    return "DUMP";
                case 5U:
                    return "BCAST";
                case 6U:
                    return "GETTIME";
                case 7U:
                    return "UADDR2TADDR";
                case 8U:
                    return "TADDR2UADDR";
                case 9U:
                    return "GETVERSADDR";
                case 10U:
                    return "INDIRECT";
                case 11U:
                    return "GETADDRLIST";
                case 12U:
                    return "GETSTAT";
                default:
                    return OpenNfsTelemetryNames.ValueUnknown;
            }
        }
    }
}
