namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Procedures;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    /// <summary>
    /// Shared helpers for the NFSv3 foundation suite catalog.
    /// </summary>
    internal static class NfsV3FoundationSuiteSupport
    {
        internal static sattr3 CreateDefaultSetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }

        internal static RpcMessageEnvelope CreateCall(uint xid, uint procedure, ReadOnlyMemory<byte> procedurePayload)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS_PROGRAM_Program.Program,
                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                procedure: procedure,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        internal static T ReadAcceptedSuccessReply<T>(RpcMessageEnvelope reply, Func<XdrReader, T> readValue)
        {
            ArgumentNullException.ThrowIfNull(reply);
            ArgumentNullException.ThrowIfNull(readValue);

            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv3 procedure result.");
            }

            return Nfs3ProcedurePayloadCodec.ReadPayload(reply.ProcedurePayload, readValue);
        }

        internal static sattr3 CreateUnsetSetAttributes()
        {
            return new sattr3
            {
                mode = new set_mode3
                {
                    set_it = false,
                },
                uid = new set_uid3
                {
                    set_it = false,
                },
                gid = new set_gid3
                {
                    set_it = false,
                },
                size = new set_size3
                {
                    set_it = false,
                },
                atime = new set_atime
                {
                    set_it = time_how.DONT_CHANGE,
                },
                mtime = new set_mtime
                {
                    set_it = time_how.DONT_CHANGE,
                },
            };
        }

        internal static nfs_fh3 ToWireFileHandle(NfsFileHandle fileHandle)
        {
            ArgumentNullException.ThrowIfNull(fileHandle);

            return new nfs_fh3
            {
                data = fileHandle.ToArray(),
            };
        }

        internal static byte[] WritePayload<T>(T value, Action<T, XdrWriter> writeValue)
        {
            ArgumentNullException.ThrowIfNull(writeValue);

            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return writer.ToArray();
        }

        internal static IReadOnlyList<entry3> FlattenEntryList(entry3list? entries)
        {
            List<entry3> flattenedEntries = new List<entry3>();
            entry3list? current = entries;

            while (current?.Value is not null)
            {
                entry3 entry = current.Value;
                flattenedEntries.Add(entry);
                current = entry.nextentry;
            }

            return flattenedEntries;
        }

        internal static IReadOnlyList<entryplus3> FlattenEntryPlusList(entryplus3list? entries)
        {
            List<entryplus3> flattenedEntries = new List<entryplus3>();
            entryplus3list? current = entries;

            while (current?.Value is not null)
            {
                entryplus3 entry = current.Value;
                flattenedEntries.Add(entry);
                current = entry.nextentry;
            }

            return flattenedEntries;
        }

        internal static ulong ReadTimeValue(nfstime3? value)
        {
            if (value?.seconds is null || value.nseconds is null)
            {
                throw new InvalidOperationException("Expected an NFSv3 timestamp value to be present while validating weak-cache-consistency metadata.");
            }

            return ((ulong)value.seconds.Value << 32) | value.nseconds.Value;
        }
    }
}
