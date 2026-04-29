namespace OpenNFS.Protocol.V3.Server.Procedures
{
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Rpc.RpcMessages;

    internal interface INfs3ProcedureHandler
    {
        uint ProcedureNumber { get; }

        Task<RpcMessageEnvelope> HandleAsync(RpcMessageEnvelope request, CancellationToken cancellationToken);
    }
}
