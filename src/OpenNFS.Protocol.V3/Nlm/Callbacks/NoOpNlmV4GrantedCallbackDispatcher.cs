namespace OpenNFS.Protocol.V3.Nlm.Callbacks
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class NoOpNlmV4GrantedCallbackDispatcher : INlmV4GrantedCallbackDispatcher
    {
        internal static NoOpNlmV4GrantedCallbackDispatcher Instance { get; } = new NoOpNlmV4GrantedCallbackDispatcher();

        private NoOpNlmV4GrantedCallbackDispatcher()
        {
        }

        public Task<NlmV4GrantedCallbackStatus> DispatchGrantedAsync(
            NlmV4GrantedCallback callback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NlmV4GrantedCallbackStatus.Failed);
        }
    }
}
