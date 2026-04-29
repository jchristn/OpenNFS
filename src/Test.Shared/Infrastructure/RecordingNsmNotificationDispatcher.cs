namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Protocol.V3.Nsm.Callbacks;

    internal sealed class RecordingNsmNotificationDispatcher : INsmNotificationDispatcher
    {
        private readonly Func<NsmNotificationCallback, CancellationToken, Task> _dispatchAsync;
        private readonly List<NsmNotificationCallback> _callbacks = new List<NsmNotificationCallback>();

        internal RecordingNsmNotificationDispatcher(
            Func<NsmNotificationCallback, CancellationToken, Task>? dispatchAsync = null)
        {
            _dispatchAsync = dispatchAsync ?? ((_, _) => Task.CompletedTask);
        }

        internal IReadOnlyList<NsmNotificationCallback> Callbacks => _callbacks;

        public async Task DispatchAsync(
            NsmNotificationCallback callback,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _callbacks.Add(callback);
            await _dispatchAsync(callback, cancellationToken).ConfigureAwait(false);
        }
    }
}
