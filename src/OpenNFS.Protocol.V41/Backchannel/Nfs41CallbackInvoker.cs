namespace OpenNFS.Protocol.V41.Backchannel
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;
    using OpenNFS.Protocol.V41.Sessions;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Transport;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Issues server-to-client <c>CB_COMPOUND</c> requests over an established back-channel transport.
    /// </summary>
    /// <remarks>
    /// The invoker is the server-side counterpart to the client-side
    /// <c>OpenNfsV41CallbackDispatcher</c>. It auto-injects a <c>CB_SEQUENCE</c> operation with
    /// freshly-allocated back-channel slot and sequence-id values, wraps the result in an ONC RPC
    /// CALL targeting the session's <c>csa_cb_program</c>, sends it over the supplied transport,
    /// reads the matching RPC REPLY, and decodes <c>CB_COMPOUND4res</c>. A separate <c>IRpcTransport</c>
    /// is required because the bidirectional multiplexing of fore-channel and back-channel calls over
    /// a single TCP connection is a separate slice; this invoker proves the wire-level callback flow
    /// over a dedicated back-channel transport.
    /// </remarks>
    public sealed class Nfs41CallbackInvoker
    {
        private readonly Nfs41Session session;
        private readonly IRpcTransport transport;
        private readonly SemaphoreSlim sendLock;
        private uint nextXid;
        private uint nextSequenceIdForSlotZero;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nfs41CallbackInvoker"/> class.
        /// </summary>
        /// <param name="session">The session whose back-channel is targeted.</param>
        /// <param name="transport">The RPC transport carrying back-channel traffic.</param>
        public Nfs41CallbackInvoker(Nfs41Session session, IRpcTransport transport)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(transport);

            this.session = session;
            this.transport = transport;
            sendLock = new SemaphoreSlim(1, 1);
            nextXid = 1u;
            nextSequenceIdForSlotZero = 1u;
        }

        /// <summary>
        /// Issues a CB_COMPOUND containing the supplied operations after an auto-injected CB_SEQUENCE.
        /// </summary>
        /// <param name="operations">The callback operations to send after CB_SEQUENCE.</param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The callback outcome.</returns>
        public async Task<Nfs41CallbackOutcome> InvokeAsync(
            IReadOnlyList<nfs_cb_argop4> operations,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operations);

            await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                uint slotId = 0;
                uint sequenceId = nextSequenceIdForSlotZero;
                uint xid = unchecked(nextXid++);

                CB_COMPOUND4args compound = BuildCallbackCompound(slotId, sequenceId, operations);

                XdrWriter argumentsWriter = new XdrWriter();
                compound.WriteTo(argumentsWriter);

                RpcMessageEnvelope request = RpcMessageFactory.CreateCall(
                    xid: xid,
                    program: (uint)session.CallbackProgramNumber,
                    version: (uint)NFS4_CALLBACK_Program.Version_NFS_CB,
                    procedure: (uint)NFS4_CALLBACK_Program.Procedure_NFS_CB_CB_COMPOUND,
                    credential: RpcAuthenticationCodec.CreateNone(),
                    verifier: RpcAuthenticationCodec.CreateNone(),
                    procedurePayload: argumentsWriter.ToArray());

                await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
                RpcMessageEnvelope reply = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);

                if (reply.Header.body?.rbody?.areply?.reply_data?.stat != accept_stat.SUCCESS)
                {
                    throw new InvalidDataException(
                        "Back-channel CB_COMPOUND reply did not carry an accepted SUCCESS status; received '"
                        + reply.Header.body?.rbody?.areply?.reply_data?.stat?.ToString() + "'.");
                }

                XdrReader reader = new XdrReader(reply.ProcedurePayload);
                CB_COMPOUND4res response = CB_COMPOUND4res.ReadFrom(reader);
                reader.EnsureFullyConsumed();

                if (response.status == nfsstat4.NFS4_OK
                    || (response.resarray is { Length: > 0 }
                        && response.resarray[0].resop == (uint)nfs_cb_opnum4.OP_CB_SEQUENCE
                        && response.resarray[0].opcbsequence?.csr_status == nfsstat4.NFS4_OK))
                {
                    nextSequenceIdForSlotZero = unchecked(sequenceId + 1u);
                }

                return new Nfs41CallbackOutcome(response, slotId, sequenceId);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private CB_COMPOUND4args BuildCallbackCompound(
            uint slotId,
            uint sequenceId,
            IReadOnlyList<nfs_cb_argop4> operations)
        {
            nfs_cb_argop4 sequenceOp = new nfs_cb_argop4
            {
                argop = (uint)nfs_cb_opnum4.OP_CB_SEQUENCE,
                opcbsequence = new CB_SEQUENCE4args
                {
                    csa_sessionid = new sessionid4 { Value = session.SessionId.ToBytes() },
                    csa_sequenceid = new sequenceid4 { Value = sequenceId },
                    csa_slotid = new slotid4 { Value = slotId },
                    csa_highest_slotid = new slotid4 { Value = slotId },
                    csa_cachethis = false,
                    csa_referring_call_lists = Array.Empty<referring_call_list4>(),
                },
            };

            nfs_cb_argop4[] argarray = new nfs_cb_argop4[operations.Count + 1];
            argarray[0] = sequenceOp;
            for (int index = 0; index < operations.Count; index++)
            {
                argarray[index + 1] = operations[index];
            }

            return new CB_COMPOUND4args
            {
                tag = new utf8str_cs
                {
                    Value = new utf8string { Value = System.Text.Encoding.UTF8.GetBytes("server-callback") },
                },
                minorversion = 1,
                callback_ident = 0,
                argarray = argarray,
            };
        }
    }
}
