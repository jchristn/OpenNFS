namespace OpenNFS.Client.Internal
{
    using System;
    using System.IO;
    using OpenNFS.Protocol.V40.Generated;
    using static OpenNFS.Client.Internal.OpenNfsV40ReplyValueReader;

    internal static class OpenNfsV40ReplyStateMapper
    {
        internal static OpenNfsV40ChangeInfo MapChangeInfo(change_info4? value)
        {
            if (value is null)
            {
                throw new InvalidDataException("The successful NFSv4.0 reply omitted a required change_info4 payload.");
            }

            return new OpenNfsV40ChangeInfo(
                value.atomic,
                ReadRequiredUInt64(value.before?.Value, "change_info4.before"),
                ReadRequiredUInt64(value.after?.Value, "change_info4.after"));
        }

        internal static OpenNfsV40Delegation? MapDelegation(open_delegation4? value)
        {
            if (value?.delegation_type is null || value.delegation_type == open_delegation_type4.OPEN_DELEGATE_NONE)
            {
                return null;
            }

            return value.delegation_type switch
            {
                open_delegation_type4.OPEN_DELEGATE_READ => new OpenNfsV40Delegation(
                    OpenNfsV40DelegationType.Read,
                    MapStateId(value.read?.stateid, "open_delegation4.read.stateid"),
                    value.read?.recall ?? false),
                open_delegation_type4.OPEN_DELEGATE_WRITE => new OpenNfsV40Delegation(
                    OpenNfsV40DelegationType.Write,
                    MapStateId(value.write?.stateid, "open_delegation4.write.stateid"),
                    value.write?.recall ?? false),
                _ => null,
            };
        }

        internal static OpenNfsV40LockConflict MapLockConflict(LOCK4denied? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            lock_owner4 owner = value.owner
                ?? throw new InvalidDataException("The decoded " + fieldName + ".owner field was required but missing.");
            clientid4 clientId = owner.clientid
                ?? throw new InvalidDataException("The decoded " + fieldName + ".owner.clientid field was required but missing.");
            return new OpenNfsV40LockConflict(
                clientId.Value,
                ReadRequiredOpaque(owner.owner, fieldName + ".owner.owner"),
                ReadRequiredUInt64(value.offset?.Value, fieldName + ".offset"),
                ReadRequiredUInt64(value.length?.Value, fieldName + ".length"),
                (OpenNfsV40LockType)(int)ReadRequiredEnum(value.locktype, fieldName + ".locktype"));
        }

        internal static OpenNfsV40StateId MapStateId(stateid4? value, string fieldName)
        {
            if (value is null)
            {
                throw new InvalidDataException("The decoded " + fieldName + " field was required but missing.");
            }

            return new OpenNfsV40StateId(
                value.seqid,
                ReadRequiredFixedOpaque(value.other, fieldName + ".other", 12));
        }

        internal static OpenNfsWriteStability MapWriteStability(stable_how4? value)
        {
            stable_how4 stableValue = ReadRequiredEnum(value, "stable_how4");
            return stableValue switch
            {
                stable_how4.UNSTABLE4 => OpenNfsWriteStability.Unstable,
                stable_how4.DATA_SYNC4 => OpenNfsWriteStability.DataSync,
                stable_how4.FILE_SYNC4 => OpenNfsWriteStability.FileSync,
                _ => throw new InvalidDataException(
                    "The decoded NFSv4.0 stable_how4 value '" + stableValue.ToString() + "' is not supported."),
            };
        }
    }
}
