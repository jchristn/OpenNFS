namespace OpenNFS.Client.Internal.TransportPipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class OpenNfsTransportPipelineTimeout
    {
        internal static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            TimeSpan timeout,
            string operationName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(operationName);

            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource();
            using CancellationTokenSource linkedCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

            Task<T> operationTask = action(linkedCancellationTokenSource.Token);
            Task timeoutTask = Task.Delay(timeout, cancellationToken);
            Task completedTask = await Task.WhenAny(operationTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == operationTask)
            {
                timeoutCancellationTokenSource.Cancel();
                return await operationTask.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            timeoutCancellationTokenSource.Cancel();
            throw new TimeoutException("The client operation '" + operationName + "' exceeded the configured timeout of " + timeout + ".");
        }
    }
}
