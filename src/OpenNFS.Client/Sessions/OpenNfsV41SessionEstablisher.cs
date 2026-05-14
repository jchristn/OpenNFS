namespace OpenNFS.Client.Sessions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V41.Generated;

    internal static class OpenNfsV41SessionEstablisher
    {
        internal static async Task<OpenNfsV41SessionEstablishmentResult> EstablishAsync(
            OpenNfsV41ClientSessionOptions options,
            CancellationToken cancellationToken)
        {
            OpenNfsV41ClientConnection connection = await OpenNfsV41ClientConnection
                .ConnectAsync(
                    options.Endpoint,
                    options.ConnectTimeout,
                    options.CallTimeout,
                    options.AuthenticationFlavor,
                    options.AuthSysCredentials,
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                COMPOUND4res exchangeIdResponse = await SendExchangeIdAsync(connection, options, cancellationToken).ConfigureAwait(false);
                EXCHANGE_ID4resok exchangeOk = exchangeIdResponse.resarray![0].opexchange_id?.eir_resok4
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include the expected resok payload.");

                ulong issuedClientId = exchangeOk.eir_clientid?.Value
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include a clientid.");
                uint createSequenceId = exchangeOk.eir_sequenceid?.Value
                    ?? throw new InvalidOperationException("EXCHANGE_ID succeeded but did not include a sequenceid.");
                ulong issuedServerMinorId = exchangeOk.eir_server_owner?.so_minor_id ?? 0u;
                byte[] issuedServerMajorId = exchangeOk.eir_server_owner?.so_major_id ?? Array.Empty<byte>();
                byte[] issuedServerScope = exchangeOk.eir_server_scope ?? Array.Empty<byte>();

                COMPOUND4res createSessionResponse = await SendCreateSessionAsync(
                    connection,
                    issuedClientId,
                    createSequenceId,
                    options.RequestedSlots,
                    cancellationToken).ConfigureAwait(false);
                CREATE_SESSION4resok createOk = createSessionResponse.resarray![0].opcreate_session?.csr_resok4
                    ?? throw new InvalidOperationException("CREATE_SESSION succeeded but did not include the expected resok payload.");

                byte[] sessionIdBytes = createOk.csr_sessionid?.Value
                    ?? throw new InvalidOperationException("CREATE_SESSION succeeded but did not include a sessionid.");
                uint negotiatedSlotCount = createOk.csr_fore_chan_attrs?.ca_maxrequests?.Value
                    ?? options.RequestedSlots;
                if (negotiatedSlotCount == 0)
                {
                    throw new InvalidOperationException("CREATE_SESSION negotiated zero fore-channel slots.");
                }

                return new OpenNfsV41SessionEstablishmentResult(
                    connection,
                    sessionIdBytes,
                    issuedClientId,
                    negotiatedSlotCount,
                    issuedServerMinorId,
                    issuedServerMajorId,
                    issuedServerScope);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<COMPOUND4res> SendExchangeIdAsync(
            OpenNfsV41ClientConnection connection,
            OpenNfsV41ClientSessionOptions options,
            CancellationToken cancellationToken)
        {
            COMPOUND4args exchangeIdCompound = new COMPOUND4args
            {
                tag = OpenNfsV41SessionProtocol.MakeTag("client-exchange-id"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_EXCHANGE_ID,
                        opexchange_id = OpenNfsV41SessionProtocol.BuildExchangeIdArguments(options.ClientOwner),
                    },
                },
            };
            COMPOUND4res exchangeIdResponse = await connection.SendCompoundAsync(exchangeIdCompound, cancellationToken).ConfigureAwait(false);
            OpenNfsV41SessionProtocol.EnsureCompoundOk(exchangeIdResponse, "EXCHANGE_ID");
            return exchangeIdResponse;
        }

        private static async Task<COMPOUND4res> SendCreateSessionAsync(
            OpenNfsV41ClientConnection connection,
            ulong clientId,
            uint sequenceId,
            uint requestedSlots,
            CancellationToken cancellationToken)
        {
            COMPOUND4args createSessionCompound = new COMPOUND4args
            {
                tag = OpenNfsV41SessionProtocol.MakeTag("client-create-session"),
                minorversion = 1,
                argarray = new[]
                {
                    new nfs_argop4
                    {
                        argop = nfs_opnum4.OP_CREATE_SESSION,
                        opcreate_session = OpenNfsV41SessionProtocol.BuildCreateSessionArguments(clientId, sequenceId, requestedSlots),
                    },
                },
            };
            COMPOUND4res createSessionResponse = await connection.SendCompoundAsync(createSessionCompound, cancellationToken).ConfigureAwait(false);
            OpenNfsV41SessionProtocol.EnsureCompoundOk(createSessionResponse, "CREATE_SESSION");
            return createSessionResponse;
        }
    }
}
