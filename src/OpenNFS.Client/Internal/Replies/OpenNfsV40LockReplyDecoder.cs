namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyEnvelopeReader;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyStateMapper;

    internal static class OpenNfsV40LockReplyDecoder
    {
        internal static OpenNfsV40LockResult ReadLockResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCK");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCK"));

            if (overallStatus == OpenNfsV40Status.Ok)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCK");
                LOCK4res lockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCK, "NFSv4.0 LOCK").oplock
                    ?? throw new InvalidDataException("The successful NFSv4.0 LOCK reply omitted the LOCK result arm.");
                LOCK4resok resok = lockResult.resok4
                    ?? throw new InvalidDataException("The successful NFSv4.0 LOCK reply omitted the LOCK success arm.");
                return new OpenNfsV40LockResult(overallStatus, MapStateId(resok.lock_stateid, "LOCK4resok.lock_stateid"));
            }

            if (overallStatus == OpenNfsV40Status.Denied)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCK");
                LOCK4res lockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCK, "NFSv4.0 LOCK").oplock
                    ?? throw new InvalidDataException("The denied NFSv4.0 LOCK reply omitted the LOCK result arm.");
                return new OpenNfsV40LockResult(
                    overallStatus,
                    conflict: MapLockConflict(lockResult.denied, "LOCK4res.denied"));
            }

            return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
        }

        internal static OpenNfsV40LockResult ReadLockTestResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCKT");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCKT"));

            if (overallStatus == OpenNfsV40Status.Ok)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKT");
                return new OpenNfsV40LockResult(overallStatus);
            }

            if (overallStatus == OpenNfsV40Status.Denied)
            {
                EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKT");
                LOCKT4res lockTestResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCKT, "NFSv4.0 LOCKT").oplockt
                    ?? throw new InvalidDataException("The denied NFSv4.0 LOCKT reply omitted the LOCKT result arm.");
                return new OpenNfsV40LockResult(
                    overallStatus,
                    conflict: MapLockConflict(lockTestResult.denied, "LOCKT4res.denied"));
            }

            return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
        }

        internal static OpenNfsV40LockResult ReadLockUnlockResult(ReadOnlyMemory<byte> encodedReply)
        {
            COMPOUND4res result = ReadCompoundResult(encodedReply, "NFSv4.0 LOCKU");
            OpenNfsV40Status overallStatus = MapStatus(ReadRequiredStatus(result.status, "NFSv4.0 LOCKU"));

            if (overallStatus != OpenNfsV40Status.Ok)
            {
                return new OpenNfsV40LockResult(ReadTerminalStatus(result, overallStatus));
            }

            EnsureSuccessOperationCount(result, 2, "NFSv4.0 LOCKU");
            LOCKU4res unlockResult = ReadExpectedOperation(result, 1, nfs_opnum4.OP_LOCKU, "NFSv4.0 LOCKU").oplocku
                ?? throw new InvalidDataException("The successful NFSv4.0 LOCKU reply omitted the LOCKU result arm.");
            return new OpenNfsV40LockResult(overallStatus, MapStateId(unlockResult.lock_stateid, "LOCKU4res.lock_stateid"));
        }
    }
}
