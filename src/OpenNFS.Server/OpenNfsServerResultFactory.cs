namespace OpenNFS.Server
{
    using System;
    using System.Threading.Tasks;

    internal static class OpenNfsServerResultFactory
    {
        internal static async Task<OpenNfsServerResult> TryAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                return OpenNfsServerResult.Success();
            }
            catch (OpenNfsServerException exception)
            {
                return OpenNfsServerResult.Failure(exception);
            }
            catch (ObjectDisposedException exception)
            {
                return OpenNfsServerResult.Failure(new OpenNfsServerStateException(exception.Message, exception));
            }
            catch (InvalidOperationException exception)
            {
                return OpenNfsServerResult.Failure(new OpenNfsServerStateException(exception.Message, exception));
            }
        }
    }
}
