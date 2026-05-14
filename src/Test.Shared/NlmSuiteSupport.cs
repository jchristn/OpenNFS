namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using OpenNFS.Client;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Raw;
    using OpenNFS.Protocol.V3.Generated;
    using OpenNFS.Protocol.V3.Nlm;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;
    using OpenNFS.Protocol.V3.Nsm;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.Server;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the NLM v4 suite catalog.
    /// </summary>
    internal static class NlmSuiteSupport
    {
        internal static OpenNfsClient CreateClient(ScriptedRpcExecutor executor)
        {
            return new OpenNfsClient(
                new OpenNfsClientBuilder()
                    .WithPrimaryEndpoint("nlm.example", 4045)
                    .Build()
                    .Settings,
                executor,
                transportPipeline: null);
        }

        internal static OpenNfsServer CreateLockingServer()
        {
            CapabilityAwareDictionaryNfsFileSystem fileSystem = new CapabilityAwareDictionaryNfsFileSystem(
                new Dictionary<string, NfsPathKind>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\locks"] = NfsPathKind.Directory,
                    ["C:\\locks\\data.bin"] = NfsPathKind.File,
                });

            return new OpenNfsServerBuilder()
                .UseFileSystem(fileSystem)
                .Build();
        }

        internal static async Task<byte[]> CreateFileHandleAsync(OpenNfsServer server, System.Threading.CancellationToken cancellationToken)
        {
            NfsFileHandle fileHandle = await server.CreateFileHandleAsync(
                new NfsFileHandleTarget("/data.bin", "C:\\locks\\data.bin"),
                cancellationToken).ConfigureAwait(false);
            return fileHandle.ToArray();
        }

        internal static RpcMessageEnvelope CreateCall(uint xid, uint procedureNumber, byte[] procedurePayload)
        {
            return RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NLM_PROG_Program.Program,
                version: (uint)NLM_PROG_Program.Version_NLM4_VERS,
                procedure: procedureNumber,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: procedurePayload);
        }

        internal static byte[] CreateTestArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool exclusive)
        {
            nlm4_testargs arguments = new nlm4_testargs
            {
                cookie = CreateNetObject(cookie),
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        internal static byte[] CreateLockArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive,
            bool reclaim,
            int state)
        {
            nlm4_lockargs arguments = new nlm4_lockargs
            {
                cookie = CreateNetObject(cookie),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
                reclaim = reclaim,
                state = new int32
                {
                    Value = state,
                },
            };

            return Encode(arguments.WriteTo);
        }

        internal static byte[] CreateCancelArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length,
            bool block,
            bool exclusive)
        {
            nlm4_cancargs arguments = new nlm4_cancargs
            {
                cookie = CreateNetObject(cookie),
                block = block,
                exclusive = exclusive,
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        internal static byte[] CreateUnlockArguments(
            byte[] cookie,
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            nlm4_unlockargs arguments = new nlm4_unlockargs
            {
                cookie = CreateNetObject(cookie),
                alock = CreateLock(fileHandle, ownerHandle, callerName, ownerProcessId, offset, length),
            };

            return Encode(arguments.WriteTo);
        }

        internal static nlm4_lock CreateLock(
            byte[] fileHandle,
            byte[] ownerHandle,
            string callerName,
            int ownerProcessId,
            ulong offset,
            ulong length)
        {
            return new nlm4_lock
            {
                caller_name = callerName,
                fh = CreateNetObject(fileHandle),
                oh = CreateNetObject(ownerHandle),
                svid = new int32
                {
                    Value = ownerProcessId,
                },
                l_offset = new uint64
                {
                    Value = offset,
                },
                l_len = new uint64
                {
                    Value = length,
                },
            };
        }

        internal static netobj CreateNetObject(byte[] value)
        {
            return new netobj
            {
                Value = value,
            };
        }

        internal static byte[] Encode(Action<XdrWriter> writeAction)
        {
            XdrWriter writer = new XdrWriter();
            writeAction(writer);
            return writer.ToArray();
        }
    }
}
