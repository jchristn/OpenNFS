namespace OpenNFS.Client.Internal
{
    internal static class OpenNfsV3RpcConstants
    {
        internal const ulong MountProgram = 100005;
        internal const uint MountDumpProcedure = 2;
        internal const uint MountExportProcedure = 5;
        internal const uint MountNullProcedure = 0;
        internal const uint MountProcedure = 1;
        internal const uint MountUmountAllProcedure = 4;
        internal const uint MountUmountProcedure = 3;
        internal const ulong MountVersion = 3;

        internal const ulong NfsProgram = 100003;
        internal const uint NfsAccessProcedure = 4;
        internal const uint NfsCommitProcedure = 21;
        internal const uint NfsCreateProcedure = 8;
        internal const uint NfsFsInfoProcedure = 19;
        internal const uint NfsFsStatProcedure = 18;
        internal const uint NfsGetattrProcedure = 1;
        internal const uint NfsLinkProcedure = 15;
        internal const uint NfsLookupProcedure = 3;
        internal const uint NfsMkdirProcedure = 9;
        internal const uint NfsNullProcedure = 0;
        internal const uint NfsPathConfProcedure = 20;
        internal const uint NfsReadProcedure = 6;
        internal const uint NfsReadLinkProcedure = 5;
        internal const uint NfsReaddirPlusProcedure = 17;
        internal const uint NfsReaddirProcedure = 16;
        internal const uint NfsRemoveProcedure = 12;
        internal const uint NfsRenameProcedure = 14;
        internal const uint NfsRmdirProcedure = 13;
        internal const uint NfsSetAttrProcedure = 2;
        internal const uint NfsSymlinkProcedure = 10;
        internal const ulong NfsVersion = 3;
        internal const uint NfsWriteProcedure = 7;

        internal const ulong NlmProgram = 100021;
        internal const uint NlmCancelProcedure = 3;
        internal const uint NlmGrantedProcedure = 5;
        internal const uint NlmLockProcedure = 2;
        internal const uint NlmNullProcedure = 0;
        internal const uint NlmTestProcedure = 1;
        internal const uint NlmUnlockProcedure = 4;
        internal const ulong NlmVersion = 4;
    }
}
