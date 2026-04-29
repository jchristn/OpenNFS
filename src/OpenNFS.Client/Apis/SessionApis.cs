namespace OpenNFS.Client.Apis
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client.Compound;
    using OpenNFS.Client.Internal;
    using OpenNFS.Client.Internal.TransportPipeline;
    using OpenNFS.Protocol.V40.Generated;
    using OpenNFS.Rpc.Xdr;

    /// <summary>
    /// Provides grouped session-oriented convenience APIs over the lower-level raw client surface.
    /// Use the raw planning APIs on <see cref="OpenNfsClient"/> directly when exact protocol coverage is required beyond these helpers.
    /// </summary>
    public sealed class SessionApis
    {
        private readonly OpenNfsClient _client;

        internal SessionApis(OpenNfsClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Prepares an NFSv4.1 session-oriented COMPOUND plan using the default session tag.
        /// </summary>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated COMPOUND plan.</returns>
        public Task<OpenNfsCompoundPlan> PrepareAsync(IReadOnlyCollection<OpenNfsCompoundOperation> operations, CancellationToken cancellationToken)
        {
            return PrepareAsync(OpenNfsProtocolVersion.Nfs41, "session", operations, cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4 COMPOUND plan for session-oriented flows.
        /// </summary>
        /// <param name="protocolVersion">NFSv4 protocol version to use.</param>
        /// <param name="tag">Client tag for the COMPOUND payload.</param>
        /// <param name="operations">Ordered COMPOUND operations.</param>
        /// <param name="cancellationToken">Cancellation token for the planning operation.</param>
        /// <returns>The validated COMPOUND plan.</returns>
        public Task<OpenNfsCompoundPlan> PrepareAsync(
            OpenNfsProtocolVersion protocolVersion,
            string tag,
            IReadOnlyCollection<OpenNfsCompoundOperation> operations,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(new OpenNfsCompoundRequest(protocolVersion, tag, operations), cancellationToken);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>SETCLIENTID</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40SetClientIdResult> SetClientIdV40Async(
            string clientIdentifier,
            byte[] clientVerifier,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateSetClientIdV40Request(clientIdentifier, clientVerifier),
                "NFSv4.0 SETCLIENTID",
                OpenNfsTransportPipelineIdempotency.NonIdempotent,
                ReadSetClientIdV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>SETCLIENTID</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareSetClientIdV40Async(
            string clientIdentifier,
            byte[] clientVerifier,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateSetClientIdV40Request(clientIdentifier, clientVerifier), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>SETCLIENTID</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40SetClientIdResult ReadSetClientIdV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadSetClientIdResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>SETCLIENTID_CONFIRM</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40SessionResult> ConfirmClientIdV40Async(
            ulong clientId,
            byte[] confirmationVerifier,
            CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateConfirmClientIdV40Request(clientId, confirmationVerifier),
                "NFSv4.0 SETCLIENTID_CONFIRM",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadConfirmClientIdV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>SETCLIENTID_CONFIRM</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareConfirmClientIdV40Async(
            ulong clientId,
            byte[] confirmationVerifier,
            CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(
                CreateConfirmClientIdV40Request(clientId, confirmationVerifier),
                cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>SETCLIENTID_CONFIRM</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40SessionResult ReadConfirmClientIdV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadSetClientIdConfirmResult(encodedReply);
        }

        /// <summary>
        /// Executes an NFSv4.0 <c>RENEW</c> request and decodes the typed result.
        /// </summary>
        public Task<OpenNfsV40SessionResult> RenewV40Async(ulong clientId, CancellationToken cancellationToken)
        {
            return _client.ExecuteCompoundAsync(
                CreateRenewV40Request(clientId),
                "NFSv4.0 RENEW",
                OpenNfsTransportPipelineIdempotency.Idempotent,
                ReadRenewV40Result,
                cancellationToken);
        }

        /// <summary>
        /// Prepares an NFSv4.0 grouped <c>RENEW</c> COMPOUND plan.
        /// </summary>
        public Task<OpenNfsCompoundPlan> PrepareRenewV40Async(ulong clientId, CancellationToken cancellationToken)
        {
            return _client.PrepareCompoundAsync(CreateRenewV40Request(clientId), cancellationToken);
        }

        /// <summary>
        /// Decodes a typed NFSv4.0 <c>RENEW</c> result from a full encoded RPC reply.
        /// </summary>
        public OpenNfsV40SessionResult ReadRenewV40Result(ReadOnlyMemory<byte> encodedReply)
        {
            return OpenNfsV40ReplyDecoder.ReadRenewResult(encodedReply);
        }

        private static OpenNfsCompoundRequest CreateSetClientIdV40Request(string clientIdentifier, byte[] clientVerifier)
        {
            string safeClientIdentifier = OpenNfsClientArgument.RequireText(clientIdentifier, nameof(clientIdentifier));
            byte[] safeClientVerifier = OpenNfsClientArgument.RequireFixedBytes(clientVerifier, expectedLength: 8, nameof(clientVerifier));
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "setclientid",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETCLIENTID,
                        EncodeV40Payload(
                            new SETCLIENTID4args
                            {
                                client = new nfs_client_id4
                                {
                                    verifier = new verifier4
                                    {
                                        Value = safeClientVerifier,
                                    },
                                    id = Encoding.UTF8.GetBytes(safeClientIdentifier),
                                },
                                callback = new cb_client4
                                {
                                    cb_program = 0U,
                                    cb_location = new clientaddr4
                                    {
                                        r_netid = string.Empty,
                                        r_addr = string.Empty,
                                    },
                                },
                                callback_ident = 0U,
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateConfirmClientIdV40Request(ulong clientId, byte[] confirmationVerifier)
        {
            byte[] safeConfirmationVerifier = OpenNfsClientArgument.RequireFixedBytes(
                confirmationVerifier,
                expectedLength: 8,
                nameof(confirmationVerifier));
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "setclientid-confirm",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_SETCLIENTID_CONFIRM,
                        EncodeV40Payload(
                            new SETCLIENTID_CONFIRM4args
                            {
                                clientid = new clientid4
                                {
                                    Value = clientId,
                                },
                                setclientid_confirm = new verifier4
                                {
                                    Value = safeConfirmationVerifier,
                                },
                            }.WriteTo)),
                });
        }

        private static OpenNfsCompoundRequest CreateRenewV40Request(ulong clientId)
        {
            return new OpenNfsCompoundRequest(
                OpenNfsProtocolVersion.Nfs40,
                "renew",
                new OpenNfsCompoundOperation[]
                {
                    new OpenNfsCompoundOperation(
                        (uint)nfs_opnum4.OP_RENEW,
                        EncodeV40Payload(
                            new RENEW4args
                            {
                                clientid = new clientid4
                                {
                                    Value = clientId,
                                },
                            }.WriteTo)),
                });
        }

        private static byte[] EncodeV40Payload(Action<XdrWriter> writePayload)
        {
            ArgumentNullException.ThrowIfNull(writePayload);

            XdrWriter writer = new XdrWriter();
            writePayload(writer);
            return writer.ToArray();
        }
    }
}
