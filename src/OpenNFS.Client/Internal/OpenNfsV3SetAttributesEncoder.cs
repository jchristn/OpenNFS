namespace OpenNFS.Client.Internal
{
    using System;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.Xdr;

    internal static class OpenNfsV3SetAttributesEncoder
    {
        internal static OpenNfsV3ProcedureRequest CreateRequest(
            byte[] fileHandle,
            OpenNfsV3SetAttributes attributes,
            OpenNfsV3Time? guardChangeTime)
        {
            byte[] safeFileHandle = OpenNfsClientArgument.RequireBytes(fileHandle, nameof(fileHandle), allowEmpty: false);
            ArgumentNullException.ThrowIfNull(attributes);

            SETATTR3args arguments = new SETATTR3args
            {
                @object = new nfs_fh3
                {
                    data = safeFileHandle,
                },
                new_attributes = CreateAttributes(attributes),
                guard = guardChangeTime is null
                    ? new sattrguard3
                    {
                        check = false,
                    }
                    : new sattrguard3
                    {
                        check = true,
                        obj_ctime = CreateTime(guardChangeTime),
                    },
            };

            XdrWriter writer = new XdrWriter();
            arguments.WriteTo(writer);
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: OpenNfsV3RpcConstants.NfsSetAttrProcedure,
                procedurePayload: writer.ToArray());
        }

        internal static sattr3 CreateAttributes(OpenNfsV3SetAttributes attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);

            return new sattr3
            {
                mode = attributes.Mode.HasValue
                    ? new set_mode3 { set_it = true, mode = new mode3 { Value = new uint32 { Value = attributes.Mode.Value } } }
                    : new set_mode3 { set_it = false },
                uid = attributes.UserId.HasValue
                    ? new set_uid3 { set_it = true, uid = new uid3 { Value = new uint32 { Value = attributes.UserId.Value } } }
                    : new set_uid3 { set_it = false },
                gid = attributes.GroupId.HasValue
                    ? new set_gid3 { set_it = true, gid = new gid3 { Value = new uint32 { Value = attributes.GroupId.Value } } }
                    : new set_gid3 { set_it = false },
                size = attributes.SizeBytes.HasValue
                    ? new set_size3 { set_it = true, size = new size3 { Value = new uint64 { Value = attributes.SizeBytes.Value } } }
                    : new set_size3 { set_it = false },
                atime = new set_atime
                {
                    set_it = MapTimeHow(attributes.AccessTimeMode),
                    atime_value = attributes.AccessTimeMode == OpenNfsV3TimeSetMode.SetToClientTime
                        ? CreateTime(attributes.AccessTime!)
                        : null,
                },
                mtime = new set_mtime
                {
                    set_it = MapTimeHow(attributes.ModifyTimeMode),
                    mtime_value = attributes.ModifyTimeMode == OpenNfsV3TimeSetMode.SetToClientTime
                        ? CreateTime(attributes.ModifyTime!)
                        : null,
                },
            };
        }

        private static time_how MapTimeHow(OpenNfsV3TimeSetMode mode)
        {
            return mode switch
            {
                OpenNfsV3TimeSetMode.DoNotChange => time_how.DONT_CHANGE,
                OpenNfsV3TimeSetMode.SetToServerTime => time_how.SET_TO_SERVER_TIME,
                OpenNfsV3TimeSetMode.SetToClientTime => time_how.SET_TO_CLIENT_TIME,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The timestamp update mode is not defined."),
            };
        }

        private static nfstime3 CreateTime(OpenNfsV3Time time)
        {
            return new nfstime3
            {
                seconds = new uint32 { Value = time.Seconds },
                nseconds = new uint32 { Value = time.Nanoseconds },
            };
        }
    }
}
