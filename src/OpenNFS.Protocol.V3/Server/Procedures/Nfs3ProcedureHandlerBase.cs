namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.RpcMessages;
    using OpenNFS.Rpc.Xdr;

    internal abstract class Nfs3ProcedureHandlerBase<TArguments, TResult> : INfs3ProcedureHandler
    {
        private readonly Func<XdrReader, TArguments> _readArguments;

        protected Nfs3ProcedureHandlerBase(uint procedureNumber, Func<XdrReader, TArguments> readArguments)
        {
            ArgumentNullException.ThrowIfNull(readArguments);

            ProcedureNumber = procedureNumber;
            _readArguments = readArguments;
        }

        public uint ProcedureNumber { get; }

        public async Task<RpcMessageEnvelope> HandleAsync(RpcMessageEnvelope request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            TArguments arguments;
            try
            {
                arguments = Nfs3ProcedurePayloadCodec.ReadPayload(request.ProcedurePayload, _readArguments);
            }
            catch (XdrDataException)
            {
                return RpcMessageFactory.CreateAcceptedReply(
                    xid: request.Header.xid,
                    status: accept_stat.GARBAGE_ARGS);
            }

            TResult result = await HandleCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
            return Nfs3ProcedurePayloadCodec.CreateAcceptedSuccessReply(request.Header.xid, result, WriteResult);
        }

        protected abstract Task<TResult> HandleCoreAsync(TArguments arguments, CancellationToken cancellationToken);

        protected abstract void WriteResult(TResult result, XdrWriter writer);
    }
}
