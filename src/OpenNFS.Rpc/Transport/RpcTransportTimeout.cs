namespace OpenNFS.Rpc.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class RpcTransportTimeout
    {
        public static async Task ExecuteAsync(
            Func<CancellationToken, ValueTask> operation,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string operationName)
        {
            ArgumentNullException.ThrowIfNull(operation);

            using CancellationTokenSource timeoutTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutTokenSource.Token);

            try
            {
                await operation(linkedTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException("The operation '" + operationName + "' exceeded the configured timeout of " + timeout + ".", exception);
            }
        }

        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string operationName)
        {
            ArgumentNullException.ThrowIfNull(operation);

            using CancellationTokenSource timeoutTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutTokenSource.Token);

            try
            {
                return await operation(linkedTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeoutTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException("The operation '" + operationName + "' exceeded the configured timeout of " + timeout + ".", exception);
            }
        }
    }
}
