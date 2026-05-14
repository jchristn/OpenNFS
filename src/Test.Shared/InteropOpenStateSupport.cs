namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    internal static class InteropOpenStateSupport
    {
        internal static async Task<OpenNfsV40OpenResult> WaitForV40OpenReadyAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            ulong clientId,
            string openOwner,
            string entryName,
            OpenNfsV40ShareAccess shareAccess,
            OpenNfsV40ShareDeny shareDeny,
            uint sequenceId,
            OpenNfsV40OpenResult initialResult,
            CancellationToken cancellationToken)
        {
            OpenNfsV40OpenResult currentResult = initialResult;
            if (currentResult.IsSuccess)
            {
                return currentResult;
            }

            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentResult.Status != OpenNfsV40Status.Grace
                    && currentResult.Status != OpenNfsV40Status.Delay)
                {
                    return currentResult;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

                try
                {
                    currentResult = await client.Files.OpenExistingV40Async(
                        directoryHandle,
                        clientId,
                        openOwner,
                        entryName,
                        shareAccess,
                        shareDeny,
                        sequenceId,
                        cancellationToken).ConfigureAwait(false);
                    if (currentResult.IsSuccess)
                    {
                        return currentResult;
                    }
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux NFSv4.0 server to allow stateful OPEN after startup grace.",
                    lastException);
            }

            return currentResult;
        }

        internal static async Task<InteropSuiteSupport.OpenConfirmationResult> ConfirmOpenIfRequiredAsync(
            OpenNfsClient client,
            OpenNfsV40OpenResult openResult,
            uint confirmSequenceId,
            CancellationToken cancellationToken)
        {
            if (!openResult.IsSuccess || openResult.StateId is null)
            {
                throw new InvalidOperationException("Expected a successful NFSv4.0 OPEN result with a returned stateid.");
            }

            if (!openResult.RequiresConfirmation)
            {
                return new InteropSuiteSupport.OpenConfirmationResult(openResult.StateId, confirmSequenceId);
            }

            OpenNfsV40StateIdResult openConfirmResult;
            if (openResult.ObjectFileHandle.Length > 0)
            {
                openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.ObjectFileHandle.ToArray(),
                    openResult.StateId,
                    confirmSequenceId,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                openConfirmResult = await client.Files.ConfirmOpenV40Async(
                    openResult.StateId,
                    confirmSequenceId,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!openConfirmResult.IsSuccess || openConfirmResult.StateId is null)
            {
                throw new InvalidOperationException("Expected a successful NFSv4.0 OPEN_CONFIRM result with a returned stateid.");
            }

            return new InteropSuiteSupport.OpenConfirmationResult(openConfirmResult.StateId, confirmSequenceId + 1U);
        }
    }
}
