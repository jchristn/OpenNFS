namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nlm.Callbacks;

    internal sealed class RecordingNlmV4GrantedCallbackDispatcher : INlmV4GrantedCallbackDispatcher
    {
        private readonly Func<NlmV4GrantedCallback, CancellationToken, Task<NlmV4GrantedCallbackStatus>> _dispatchAsync;
        private readonly List<NlmV4GrantedCallback> _callbacks = new List<NlmV4GrantedCallback>();

        internal RecordingNlmV4GrantedCallbackDispatcher(
            Func<NlmV4GrantedCallback, CancellationToken, Task<NlmV4GrantedCallbackStatus>> dispatchAsync)
        {
            ArgumentNullException.ThrowIfNull(dispatchAsync);
            _dispatchAsync = dispatchAsync;
        }

        internal IReadOnlyList<NlmV4GrantedCallback> Callbacks => _callbacks;

        public Task<NlmV4GrantedCallbackStatus> DispatchGrantedAsync(
            NlmV4GrantedCallback callback,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _callbacks.Add(callback);
            return _dispatchAsync(callback, cancellationToken);
        }
    }
}
