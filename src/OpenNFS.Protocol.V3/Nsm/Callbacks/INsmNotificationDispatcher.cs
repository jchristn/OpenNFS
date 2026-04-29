namespace OpenNFS.Protocol.V3.Nsm.Callbacks
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface INsmNotificationDispatcher
    {
        Task DispatchAsync(
            NsmNotificationCallback callback,
            CancellationToken cancellationToken);
    }
}
