namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;
    using OpenNFS.Client;
    using OpenNFS.Client.Sessions;
    using OpenNFS.Protocol.V41.Backchannel;
    using OpenNFS.Protocol.V41.Compound;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Hosting;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Protocol.V41.State;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;
    using Test.Shared.Infrastructure;

    /// <summary>
    /// Shared helpers for the NFSv4.1 session and callback suite catalog.
    /// </summary>
    internal static class NfsV41SuiteSupport
    {
        internal static bool IsAttributeBitSet(bitmap4 mask, ulong attributeIdentifier)
        {
            uint[]? words = mask.Value;
            if (words is null)
            {
                return false;
            }

            int wordIndex = (int)(attributeIdentifier / 32);
            int bitIndex = (int)(attributeIdentifier % 32);
            if (wordIndex >= words.Length)
            {
                return false;
            }

            return (words[wordIndex] & (1u << bitIndex)) != 0;
        }

        internal static utf8str_cs MakeTag(string text)
        {
            return new utf8str_cs
            {
                Value = new utf8string { Value = System.Text.Encoding.UTF8.GetBytes(text) },
            };
        }

        internal static CB_COMPOUND4args BuildCallbackCompound(
            byte[] sessionId,
            uint slotId,
            uint sequenceId,
            bool cacheThis,
            IReadOnlyList<nfs_cb_argop4> ops)
        {
            nfs_cb_argop4 sequenceOp = new nfs_cb_argop4
            {
                argop = (uint)nfs_cb_opnum4.OP_CB_SEQUENCE,
                opcbsequence = new CB_SEQUENCE4args
                {
                    csa_sessionid = new sessionid4 { Value = sessionId },
                    csa_sequenceid = new sequenceid4 { Value = sequenceId },
                    csa_slotid = new slotid4 { Value = slotId },
                    csa_highest_slotid = new slotid4 { Value = slotId },
                    csa_cachethis = cacheThis,
                    csa_referring_call_lists = Array.Empty<referring_call_list4>(),
                },
            };

            nfs_cb_argop4[] argarray = new nfs_cb_argop4[ops.Count + 1];
            argarray[0] = sequenceOp;
            for (int index = 0; index < ops.Count; index++)
            {
                argarray[index + 1] = ops[index];
            }

            return new CB_COMPOUND4args
            {
                tag = MakeTag("cb-test"),
                minorversion = 1,
                callback_ident = 0,
                argarray = argarray,
            };
        }

        internal static void EnsureCategory(nfsstat4 status, OpenNfsErrorCategory expected)
        {
            OpenNfsErrorCategory actual = OpenNfsV41StatusException.ClassifyCategory(status);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    "Expected " + status + " to map to " + expected + " but got " + actual + ".");
            }
        }

        internal static void EnsurePublicType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!type.IsPublic)
            {
                throw new InvalidOperationException(
                    "Type " + type.FullName + " must be publicly visible on the OpenNFS.Client surface.");
            }
        }

        internal static void EnsurePublicMethod(Type type, string methodName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            System.Reflection.MethodInfo[] candidates = type.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Name == methodName)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "Type " + type.FullName + " must expose a public method named '" + methodName + "'.");
        }

        internal static void EnsurePublicProperty(Type type, string propertyName)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            System.Reflection.PropertyInfo? property = type.GetProperty(propertyName,
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            if (property is null)
            {
                throw new InvalidOperationException(
                    "Type " + type.FullName + " must expose a public property named '" + propertyName + "'.");
            }
        }

        internal static Nfs41Session BuildSessionForCallback(byte[] sessionIdBytes, uint cbProgram)
        {
            Nfs41ChannelAttributes channelAttrs = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 65536,
                maximumResponseSize: 65536,
                maximumCachedResponseSize: 0,
                maximumOperations: 16,
                maximumRequests: 4);

            return new Nfs41Session(
                sessionId: new Nfs41SessionId(sessionIdBytes),
                clientId: 1ul,
                foreChannelAttributes: channelAttrs,
                backChannelAttributes: channelAttrs,
                callbackProgramNumber: cbProgram,
                initialConnectionIdentity: "127.0.0.1:cb-test");
        }

        internal sealed class DefaultCallbackHandler : OpenNfsV41CallbackHandler
        {
        }

        internal sealed class RecordingCallbackHandler : OpenNfsV41CallbackHandler
        {
            private readonly nfsstat4 recallStatus;
            private int recallCount;

            internal RecordingCallbackHandler(nfsstat4 recallStatus)
            {
                this.recallStatus = recallStatus;
            }

            internal int RecallCount => recallCount;

            public override ValueTask<CB_RECALL4res> OnRecallAsync(
                CB_RECALL4args arguments,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref recallCount);
                return ValueTask.FromResult(new CB_RECALL4res { status = recallStatus });
            }
        }

        internal static OpenNfsV41ClientOwner BuildClientOwner(byte verifier, byte ownerSeed)
        {
            return new OpenNfsV41ClientOwner(
                verifier: new byte[] { verifier, verifier, verifier, verifier, verifier, verifier, verifier, verifier },
                ownerId: new byte[] { ownerSeed, 0xCC, 0x55, 0xEE });
        }

        internal static async Task<OpenNfsV41ClientSession> EstablishClientSessionAsync(
            int nfsPort,
            byte ownerSeed,
            CancellationToken cancellationToken)
        {
            OpenNfsV41ClientSessionOptions options = new OpenNfsV41ClientSessionOptions(
                endpoint: new IPEndPoint(IPAddress.Loopback, nfsPort),
                clientOwner: BuildClientOwner(verifier: 0xE0, ownerSeed: ownerSeed));
            options.RequestedSlots = 4;
            options.CallTimeout = TimeSpan.FromSeconds(15);
            return await OpenNfsV41ClientSession.EstablishAsync(options, cancellationToken).ConfigureAwait(false);
        }

        internal static Nfs41SessionOperationProcessor CreateProcessorWithScope(string scope)
        {
            Nfs41ServerOwner serverOwner = new Nfs41ServerOwner(
                minorId: 1,
                majorId: new byte[] { 0x4F, 0x70, 0x65, 0x6E, 0x4E, 0x46, 0x53 });
            Nfs41ChannelAttributes maximums = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 1024 * 1024,
                maximumResponseSize: 1024 * 1024,
                maximumCachedResponseSize: 64 * 1024,
                maximumOperations: 64,
                maximumRequests: 64);
            Nfs41ServerConfiguration configuration = new Nfs41ServerConfiguration(
                serverOwner,
                serverScope: System.Text.Encoding.UTF8.GetBytes(scope),
                foreChannelMaximums: maximums,
                backChannelMaximums: maximums);
            Nfs41ClientRegistry clientRegistry = new Nfs41ClientRegistry();
            Nfs41SessionRegistry sessionRegistry = new Nfs41SessionRegistry();

            int allocatedCount = 0;
            byte[] AllocateSessionId()
            {
                byte[] bytes = new byte[Nfs41SessionId.Length];
                int counter = Interlocked.Increment(ref allocatedCount);
                bytes[Nfs41SessionId.Length - 1] = (byte)(counter & 0xFF);
                bytes[Nfs41SessionId.Length - 2] = (byte)((counter >> 8) & 0xFF);
                return bytes;
            }

            return new Nfs41SessionOperationProcessor(configuration, clientRegistry, sessionRegistry, AllocateSessionId);
        }

        internal static async Task<COMPOUND4res> DispatchCompoundAsync(
            Nfs41CompoundService service,
            COMPOUND4args arguments,
            string connectionIdentity,
            uint xid,
            CancellationToken cancellationToken)
        {
            byte[] replyBytes = await DispatchCompoundRawAsync(service, arguments, connectionIdentity, xid, cancellationToken).ConfigureAwait(false);
            XdrReader reader = new XdrReader(replyBytes);
            COMPOUND4res value = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        internal static async Task<byte[]> DispatchCompoundRawAsync(
            Nfs41CompoundService service,
            COMPOUND4args arguments,
            string connectionIdentity,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            RpcMessageEnvelope reply = await service.DispatchAsync(request, connectionIdentity, cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Wire-level COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            return reply.ProcedurePayload.ToArray();
        }

        internal static async Task<COMPOUND4res> SendCompoundOverTransportAsync(
            RpcTcpTransport transport,
            COMPOUND4args arguments,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Real-network COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            XdrReader reader = new XdrReader(reply.ProcedurePayload);
            COMPOUND4res value = COMPOUND4res.ReadFrom(reader);
            reader.EnsureFullyConsumed();
            return value;
        }

        internal static async Task<byte[]> SendCompoundRawOverTransportAsync(
            RpcTcpTransport transport,
            COMPOUND4args arguments,
            uint xid,
            CancellationToken cancellationToken)
        {
            XdrWriter argumentsWriter = new XdrWriter();
            arguments.WriteTo(argumentsWriter);

            RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                xid: xid,
                program: (uint)NFS4_PROGRAM_Program.Program,
                version: (uint)NFS4_PROGRAM_Program.Version_NFS_V4,
                procedure: (uint)NFS4_PROGRAM_Program.Procedure_NFS_V4_NFSPROC4_COMPOUND,
                credential: RpcAuthenticationCodec.CreateNone(),
                verifier: RpcAuthenticationCodec.CreateNone(),
                procedurePayload: argumentsWriter.ToArray());

            await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
            {
                throw new InvalidOperationException("Real-network COMPOUND dispatch must produce an accepted SUCCESS reply.");
            }

            return reply.ProcedurePayload.ToArray();
        }

        internal static async Task<byte[]> EstablishSessionOverWireAsync(
            Nfs41CompoundService service,
            string connectionIdentity,
            byte ownerSeed,
            uint requestedSlots,
            CancellationToken cancellationToken)
        {
            COMPOUND4args bootstrap = new COMPOUND4args
            {
                tag = MakeTag("est-bootstrap"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4 { argop = nfs_opnum4.OP_EXCHANGE_ID, opexchange_id = BuildExchangeIdArguments(0xC3, ownerSeed) },
                },
            };
            COMPOUND4res bootstrapResponse = await DispatchCompoundAsync(service, bootstrap, connectionIdentity, xid: 100, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(bootstrapResponse.status, "wire EXCHANGE_ID");
            ulong clientId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_clientid!.Value;
            uint sequenceId = bootstrapResponse.resarray![0].opexchange_id!.eir_resok4!.eir_sequenceid!.Value;

            COMPOUND4args createSession = new COMPOUND4args
            {
                tag = MakeTag("est-create"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_CREATE_SESSION,
                        opcreate_session = BuildCreateSessionArguments(clientId, sequenceId, requestedSlots),
                    },
                },
            };
            COMPOUND4res createSessionResponse = await DispatchCompoundAsync(service, createSession, connectionIdentity, xid: 101, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(createSessionResponse.status, "wire CREATE_SESSION");
            return createSessionResponse.resarray![0].opcreate_session!.csr_resok4!.csr_sessionid!.Value!;
        }

        internal static Nfs41SessionOperationProcessor CreateProcessor()
        {
            Nfs41ServerOwner serverOwner = new Nfs41ServerOwner(
                minorId: 1,
                majorId: new byte[] { 0x4F, 0x70, 0x65, 0x6E, 0x4E, 0x46, 0x53 });
            Nfs41ChannelAttributes maximums = new Nfs41ChannelAttributes(
                headerPadSize: 0,
                maximumRequestSize: 1024 * 1024,
                maximumResponseSize: 1024 * 1024,
                maximumCachedResponseSize: 64 * 1024,
                maximumOperations: 64,
                maximumRequests: 64);
            Nfs41ServerConfiguration configuration = new Nfs41ServerConfiguration(
                serverOwner,
                serverScope: new byte[] { 0x6F, 0x70, 0x65, 0x6E, 0x6E, 0x66, 0x73 },
                foreChannelMaximums: maximums,
                backChannelMaximums: maximums);
            Nfs41ClientRegistry clientRegistry = new Nfs41ClientRegistry();
            Nfs41SessionRegistry sessionRegistry = new Nfs41SessionRegistry();

            int allocatedCount = 0;
            byte[] AllocateSessionId()
            {
                byte[] bytes = new byte[Nfs41SessionId.Length];
                int counter = Interlocked.Increment(ref allocatedCount);
                bytes[Nfs41SessionId.Length - 1] = (byte)(counter & 0xFF);
                bytes[Nfs41SessionId.Length - 2] = (byte)((counter >> 8) & 0xFF);
                return bytes;
            }

            return new Nfs41SessionOperationProcessor(configuration, clientRegistry, sessionRegistry, AllocateSessionId);
        }

        internal static EXCHANGE_ID4args BuildExchangeIdArguments(byte verifier, byte ownerSeed)
        {
            return new EXCHANGE_ID4args
            {
                eia_clientowner = new client_owner4
                {
                    co_verifier = new verifier4 { Value = new byte[] { verifier, verifier, verifier, verifier, verifier, verifier, verifier, verifier } },
                    co_ownerid = new byte[] { ownerSeed, 0x10, 0x20, 0x30 },
                },
                eia_flags = 0,
                eia_state_protect = new state_protect4_a
                {
                    spa_how = state_protect_how4.SP4_NONE,
                },
                eia_client_impl_id = Array.Empty<nfs_impl_id4>(),
            };
        }

        internal static CREATE_SESSION4args BuildCreateSessionArguments(ulong clientId, uint sequenceId, uint requestedSlots)
        {
            channel_attrs4 attrs = new channel_attrs4
            {
                ca_headerpadsize = new count4 { Value = 0 },
                ca_maxrequestsize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize = new count4 { Value = 1024 * 1024 },
                ca_maxresponsesize_cached = new count4 { Value = 64 * 1024 },
                ca_maxoperations = new count4 { Value = 16 },
                ca_maxrequests = new count4 { Value = requestedSlots },
                ca_rdma_ird = Array.Empty<uint>(),
            };

            return new CREATE_SESSION4args
            {
                csa_clientid = new clientid4 { Value = clientId },
                csa_sequence = new sequenceid4 { Value = sequenceId },
                csa_flags = 0,
                csa_fore_chan_attrs = attrs,
                csa_back_chan_attrs = attrs,
                csa_cb_program = 0x40000000u,
                csa_sec_parms = Array.Empty<callback_sec_parms4>(),
            };
        }

        internal static SEQUENCE4args BuildSequenceArguments(byte[] sessionIdBytes, uint slotId, uint sequenceId, bool cacheThis, uint highestSlotId)
        {
            return new SEQUENCE4args
            {
                sa_sessionid = new sessionid4 { Value = sessionIdBytes },
                sa_sequenceid = new sequenceid4 { Value = sequenceId },
                sa_slotid = new slotid4 { Value = slotId },
                sa_highest_slotid = new slotid4 { Value = highestSlotId },
                sa_cachethis = cacheThis,
            };
        }

        internal static byte[] EstablishSession(
            Nfs41SessionOperationProcessor processor,
            Nfs41OperationContext context,
            byte ownerSeed,
            uint requestedSlots)
        {
            EXCHANGE_ID4res exchangeResult = processor.ProcessExchangeId(BuildExchangeIdArguments(0xC0, ownerSeed));
            EnsureSuccess(exchangeResult.eir_status, nameof(processor.ProcessExchangeId));
            ulong clientId = exchangeResult.eir_resok4!.eir_clientid!.Value;
            uint sequenceId = exchangeResult.eir_resok4!.eir_sequenceid!.Value;

            CREATE_SESSION4res createResult = processor.ProcessCreateSession(
                BuildCreateSessionArguments(clientId, sequenceId, requestedSlots),
                context);
            EnsureSuccess(createResult.csr_status, nameof(processor.ProcessCreateSession));
            return createResult.csr_resok4!.csr_sessionid!.Value!;
        }

        internal static void EnsureSuccess(nfsstat4? status, string operationName)
        {
            if (status != nfsstat4.NFS4_OK)
            {
                throw new InvalidOperationException(
                    "Expected " + operationName + " to return NFS4_OK but received " + status?.ToString() + ".");
            }
        }
    }
}
