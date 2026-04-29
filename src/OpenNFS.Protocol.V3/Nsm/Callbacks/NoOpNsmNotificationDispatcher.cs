namespace OpenNFS.Protocol.V3.Nsm.Callbacks
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class NoOpNsmNotificationDispatcher : INsmNotificationDispatcher
    {
        internal static NoOpNsmNotificationDispatcher Instance { get; } = new NoOpNsmNotificationDispatcher();

        private NoOpNsmNotificationDispatcher()
        {
        }

        public Task DispatchAsync(
            NsmNotificationCallback callback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
