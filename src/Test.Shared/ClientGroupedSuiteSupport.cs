namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the grouped client suite catalog.
    /// </summary>
    internal static class ClientGroupedSuiteSupport
    {
        internal static RpcMessageEnvelope CreateAcceptedReplyEnvelope<T>(uint xid, T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return RpcMessageFactory.CreateAcceptedReply(
                xid: xid,
                status: RpcGenerated.accept_stat.SUCCESS,
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: writer.ToArray());
        }

        internal static byte[] EncodeAcceptedReply<T>(T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return EncodeAcceptedSuccessReply(writer.ToArray());
        }

        internal static byte[] EncodeAcceptedSuccessReply(byte[] procedurePayload)
        {
            return RpcMessageCodec.Encode(
                RpcMessageFactory.CreateAcceptedReply(
                    xid: 0x10203040,
                    status: RpcGenerated.accept_stat.SUCCESS,
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: procedurePayload));
        }

        internal static fattr3 CreateAttributes(ftype3 fileType, ulong size, ulong fileId)
        {
            return new fattr3
            {
                type = fileType,
                mode = new mode3
                {
                    Value = new uint32
                    {
                        Value = 420,
                    },
                },
                nlink = new uint32
                {
                    Value = 1,
                },
                uid = new uid3
                {
                    Value = new uint32
                    {
                        Value = 1000,
                    },
                },
                gid = new gid3
                {
                    Value = new uint32
                    {
                        Value = 1000,
                    },
                },
                size = CreateSize(size),
                used = CreateSize(size),
                rdev = new specdata3
                {
                    specdata1 = new uint32
                    {
                        Value = 0,
                    },
                    specdata2 = new uint32
                    {
                        Value = 0,
                    },
                },
                fsid = new uint64
                {
                    Value = 77,
                },
                fileid = new fileid3
                {
                    Value = new uint64
                    {
                        Value = fileId,
                    },
                },
                atime = CreateTime(10, 1),
                mtime = CreateTime(20, 2),
                ctime = CreateTime(30, 3),
            };
        }

        internal static post_op_attr CreatePostOperationAttributes(ftype3 fileType, ulong size, ulong fileId)
        {
            return new post_op_attr
            {
                attributes_follow = true,
                attributes = CreateAttributes(fileType, size, fileId),
            };
        }

        internal static pre_op_attr CreatePreOperationAttributes(ulong size)
        {
            return new pre_op_attr
            {
                attributes_follow = true,
                attributes = new wcc_attr
                {
                    size = CreateSize(size),
                    mtime = CreateTime(40, 4),
                    ctime = CreateTime(50, 5),
                },
            };
        }

        internal static wcc_data CreateWeakCacheConsistency(ulong beforeSize, ulong afterSize, ulong afterFileId)
        {
            return new wcc_data
            {
                before = CreatePreOperationAttributes(beforeSize),
                after = CreatePostOperationAttributes(ftype3.NF3REG, afterSize, afterFileId),
            };
        }

        internal static size3 CreateSize(ulong value)
        {
            return new size3
            {
                Value = new uint64
                {
                    Value = value,
                },
            };
        }

        internal static nfstime3 CreateTime(uint seconds, uint nanoseconds)
        {
            return new nfstime3
            {
                seconds = new uint32
                {
                    Value = seconds,
                },
                nseconds = new uint32
                {
                    Value = nanoseconds,
                },
            };
        }

        internal static DirectoryEntrySeed CreateDirectoryEntrySeed(ulong fileId, string name, ulong cookie)
        {
            return new DirectoryEntrySeed(fileId, name, cookie);
        }

        internal static DirectoryEntryPlusSeed CreateDirectoryEntryPlusSeed(
            ulong fileId,
            string name,
            ulong cookie,
            ftype3 fileType,
            ulong size,
            byte[]? handle)
        {
            return new DirectoryEntryPlusSeed(fileId, name, cookie, fileType, size, handle);
        }

        internal static entry3list CreateEntryList(params DirectoryEntrySeed[] entries)
        {
            entry3list head = new entry3list();
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                DirectoryEntrySeed entry = entries[index];
                head = new entry3list
                {
                    Value = new entry3
                    {
                        fileid = new fileid3
                        {
                            Value = new uint64
                            {
                                Value = entry.FileId,
                            },
                        },
                        name = new filename3
                        {
                            Value = entry.Name,
                        },
                        cookie = new cookie3
                        {
                            Value = new uint64
                            {
                                Value = entry.Cookie,
                            },
                        },
                        nextentry = head,
                    },
                };
            }
            return head;
        }

        internal static entryplus3list CreateEntryPlusList(params DirectoryEntryPlusSeed[] entries)
        {
            entryplus3list head = new entryplus3list();
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                DirectoryEntryPlusSeed entry = entries[index];
                head = new entryplus3list
                {
                    Value = new entryplus3
                    {
                        fileid = new fileid3
                        {
                            Value = new uint64
                            {
                                Value = entry.FileId,
                            },
                        },
                        name = new filename3
                        {
                            Value = entry.Name,
                        },
                        cookie = new cookie3
                        {
                            Value = new uint64
                            {
                                Value = entry.Cookie,
                            },
                        },
                        name_attributes = CreatePostOperationAttributes(entry.FileType, entry.Size, entry.FileId),
                        name_handle = new post_op_fh3
                        {
                            handle_follows = entry.Handle is not null,
                            handle = entry.Handle is null
                                ? null
                                : new nfs_fh3
                                {
                                    data = entry.Handle,
                                },
                        },
                        nextentry = head,
                    },
                };
            }
            return head;
        }

        internal readonly struct DirectoryEntrySeed
        {
            internal DirectoryEntrySeed(ulong fileId, string name, ulong cookie)
            {
                FileId = fileId;
                Name = name;
                Cookie = cookie;
            }

            internal ulong FileId { get; }
            internal string Name { get; }
            internal ulong Cookie { get; }
        }

        internal readonly struct DirectoryEntryPlusSeed
        {
            internal DirectoryEntryPlusSeed(
                ulong fileId,
                string name,
                ulong cookie,
                ftype3 fileType,
                ulong size,
                byte[]? handle)
            {
                FileId = fileId;
                Name = name;
                Cookie = cookie;
                FileType = fileType;
                Size = size;
                Handle = handle;
            }

            internal ulong FileId { get; }
            internal string Name { get; }
            internal ulong Cookie { get; }
            internal ftype3 FileType { get; }
            internal ulong Size { get; }
            internal byte[]? Handle { get; }
        }
    }
}
