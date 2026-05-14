namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.Protocol.V40.Compound;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;

    internal static class NfsV40SuitePayloadSupport
    {
        internal static RpcMessageEnvelope CreateCompoundCall(
            uint xid,
            string tag,
            uint minorVersion,
            nfs_argop4[] operations)
        {
            XdrWriter writer = new XdrWriter();
            new COMPOUND4args
            {
                tag = new utf8str_cs
                {
                    Value = new utf8string
                    {
                        Value = Encoding.UTF8.GetBytes(tag),
                    },
                },
                minorversion = minorVersion,
                argarray = operations,
            }.WriteTo(writer);

            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: writer.ToArray());
        }

        internal static COMPOUND4res ReadCompoundReply(RpcMessageEnvelope reply)
        {
            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv4.0 COMPOUND result.");
            }

            return Nfs40CompoundPayloadCodec.ReadPayload(reply.ProcedurePayload, COMPOUND4res.ReadFrom);
        }

        internal static string ReadUtf8(utf8str_cs? value)
        {
            byte[] bytes = value?.Value?.Value ?? Array.Empty<byte>();
            return Encoding.UTF8.GetString(bytes);
        }

        internal static stateid4 CreateAnonymousStateId()
        {
            return new stateid4
            {
                seqid = 0,
                other = new byte[12],
            };
        }

        internal static fattr4 CreateTypeAttributes(nfs_ftype4 fileType)
        {
            XdrWriter writer = new XdrWriter();
            new fattr4_type
            {
                Value = fileType,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_TYPE),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        internal static fattr4 CreateAclAttributes(IReadOnlyList<NfsAclEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            XdrWriter writer = new XdrWriter();
            nfsace4[] mappedEntries = new nfsace4[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                NfsAclEntry entry = entries[index] ?? throw new ArgumentNullException(nameof(entries), "ACL entry collections cannot contain null entries.");
                mappedEntries[index] = new nfsace4
                {
                    type = new acetype4
                    {
                        Value = (uint)entry.EntryType,
                    },
                    flag = new aceflag4
                    {
                        Value = (uint)entry.EntryFlags,
                    },
                    access_mask = new acemask4
                    {
                        Value = (uint)entry.Permissions,
                    },
                    who = new utf8str_mixed
                    {
                        Value = new utf8string
                        {
                            Value = Encoding.UTF8.GetBytes(entry.Who),
                        },
                    },
                };
            }

            new fattr4_acl
            {
                Value = mappedEntries,
            }.WriteTo(writer);

            return new fattr4
            {
                attrmask = Nfs40AttributeEncoder.CreateBitmap((int)Nfs40Constants.FATTR4_ACL),
                attr_vals = new attrlist4
                {
                    Value = writer.ToArray(),
                },
            };
        }

        internal static fattr4 CreateEmptyAttributes()
        {
            return new fattr4
            {
                attrmask = new bitmap4
                {
                    Value = Array.Empty<uint>(),
                },
                attr_vals = new attrlist4
                {
                    Value = Array.Empty<byte>(),
                },
            };
        }
    }
}
