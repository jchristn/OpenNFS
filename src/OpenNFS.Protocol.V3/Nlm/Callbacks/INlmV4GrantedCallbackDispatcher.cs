namespace OpenNFS.Protocol.V3.Nlm.Callbacks
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface INlmV4GrantedCallbackDispatcher
    {
        Task<NlmV4GrantedCallbackStatus> DispatchGrantedAsync(
            NlmV4GrantedCallback callback,
            CancellationToken cancellationToken);
    }
}
