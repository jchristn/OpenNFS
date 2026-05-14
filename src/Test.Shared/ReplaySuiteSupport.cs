namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Server.Procedures;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared replay-suite helpers.
    /// </summary>
    internal static class ReplaySuiteSupport
    {
        internal static DictionaryNfsFileSystem CreateReplayFileSystem()
        {
            return new DictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports"] = NfsPathKind.Directory,
                    [@"C:\exports\data.bin"] = NfsPathKind.File,
                },
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [@"C:\exports\data.bin"] = Array.Empty<byte>(),
                });
        }

        internal static RpcTcpTransport CreateClientTransport(NetworkStream stream)
        {
            return new RpcTcpTransport(
                stream,
                new RpcTransportOptions(
                    timeouts: new RpcTransportTimeouts(
                        readTimeout: TimeSpan.FromSeconds(5),
                        writeTimeout: TimeSpan.FromSeconds(5))));
        }

        internal static OpenNfsV3ProcedureRequest CreateRawWriteRequest(NfsFileHandle fileHandle, byte[] data)
        {
            return new OpenNfsV3ProcedureRequest(
                procedureNumber: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                procedurePayload: WritePayload(
                    new WRITE3args
                    {
                        file = ToWireFileHandle(fileHandle),
                        offset = new offset3
                        {
                            Value = new uint64
                            {
                                Value = 0UL,
                            },
                        },
                        count = new count3
                        {
                            Value = new uint32
                            {
                                Value = (uint)data.Length,
                            },
                        },
                        stable = stable_how.FILE_SYNC,
                        data = data,
                    },
                    static (value, writer) => value.WriteTo(writer)));
        }

        internal static RpcMessageEnvelope CreateWriteCall(
            uint xid,
            NfsFileHandle fileHandle,
            string requesterMachineName,
            byte[] data)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS_PROGRAM_Program.Program,
                version: (uint)NFS_PROGRAM_Program.Version_NFS_V3,
                procedure: (uint)NFS_PROGRAM_Program.Procedure_NFS_V3_NFSPROC3_WRITE,
                credential: RpcAuthenticationCodec.CreateSystem(
                    new authsys_parms
                    {
                        stamp = xid,
                        machinename = requesterMachineName,
                        uid = 0,
                        gid = 0,
                        gids = Array.Empty<uint>(),
                    }),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: WritePayload(
                    new WRITE3args
                    {
                        file = ToWireFileHandle(fileHandle),
                        offset = new offset3
                        {
                            Value = new uint64
                            {
                                Value = 0UL,
                            },
                        },
                        count = new count3
                        {
                            Value = new uint32
                            {
                                Value = (uint)data.Length,
                            },
                        },
                        stable = stable_how.FILE_SYNC,
                        data = data,
                    },
                    static (value, writer) => value.WriteTo(writer)));
        }

        internal static T ReadAcceptedSuccessReply<T>(RpcMessageEnvelope reply, Func<XdrReader, T> readValue)
        {
            if (reply.Header.body?.rbody?.stat != reply_stat.MSG_ACCEPTED
                || reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Expected an accepted RPC SUCCESS reply while decoding an NFSv3 replay result.");
            }

            return Nfs3ProcedurePayloadCodec.ReadPayload(reply.ProcedurePayload, readValue);
        }

        internal static nfs_fh3 ToWireFileHandle(NfsFileHandle fileHandle)
        {
            return new nfs_fh3
            {
                data = fileHandle.ToArray(),
            };
        }

        internal static byte[] WritePayload<T>(T value, Action<T, XdrWriter> writeValue)
        {
            XdrWriter writer = new XdrWriter();
            writeValue(value, writer);
            return writer.ToArray();
        }
    }
}
