namespace Test.Shared
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenNFS.Client;

    internal static class InteropLinuxWaitSupport
    {
        internal static async Task<OpenNfsV40LookupResult> WaitForLinuxV40RootAsync(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;
            OpenNfsV40LookupResult? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    OpenNfsV40LookupResult result = await client.Directories.GetRootV40Async(cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.ObjectFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException("Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle.", lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        internal static async Task<OpenNfsV3LookupResult> WaitForV3LookupAsync(
            OpenNfsClient client,
            byte[] directoryHandle,
            string entryName,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;
            OpenNfsV3LookupResult? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    OpenNfsV3LookupResult result = await client.Directories.LookupV3Async(
                        directoryHandle,
                        entryName,
                        cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.ObjectFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux NFSv3 server to return a usable filehandle for '" + entryName + "'.",
                    lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux NFSv3 server to return a usable filehandle for '"
                + entryName
                + "'. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        internal static async Task<byte[]> ResolveLinuxExportRootV40Async(
            OpenNfsClient client,
            CancellationToken cancellationToken)
        {
            OpenNfsV40LookupResult rootResult = await WaitForLinuxV40RootAsync(client, cancellationToken).ConfigureAwait(false);
            OpenNfsV40ReadDirectoryResult rootListing = await client.Directories.ReadDirectoryV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                0UL,
                new byte[8],
                4096U,
                cancellationToken).ConfigureAwait(false);

            if (rootListing.IsSuccess)
            {
                bool rootAlreadyLooksLikeExport = rootListing.Entries.Any(
                    static entry => string.Equals(entry.Name, "d", StringComparison.Ordinal)
                        || string.Equals(entry.Name, "h.txt", StringComparison.Ordinal));
                if (rootAlreadyLooksLikeExport)
                {
                    return rootResult.ObjectFileHandle.ToArray();
                }
            }

            OpenNfsV40LookupResult exportLookup = await client.Directories.LookupV40Async(
                rootResult.ObjectFileHandle.ToArray(),
                "export",
                cancellationToken).ConfigureAwait(false);
            if (!exportLookup.IsSuccess || exportLookup.ObjectFileHandle.Length == 0)
            {
                throw new InvalidOperationException(
                    "Expected the Linux NFSv4.0 pseudo-root to expose an 'export' directory, but received status '"
                    + exportLookup.Status.ToString()
                    + "'.");
            }

            return exportLookup.ObjectFileHandle.ToArray();
        }

        internal static async Task<OpenNfsMountV3Result> WaitForLinuxMountV3Async(
            string host,
            int mountPort,
            string exportPath,
            bool enableUdpForNfsV3,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            Exception? lastException = null;
            OpenNfsMountV3Result? lastResult = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer(host, mountPort)
                        .WithUdpForNfsV3(enableUdpForNfsV3)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    OpenNfsMountV3Result result =
                        await client.Exports.MountV3Async(exportPath, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess && result.RootFileHandle.Length > 0)
                    {
                        return result;
                    }

                    lastResult = result;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            if (lastException is not null)
            {
                throw new TimeoutException(
                    "Timed out waiting for the Linux MOUNT v3 server to return a usable root filehandle for '" + exportPath + "'.",
                    lastException);
            }

            throw new TimeoutException(
                "Timed out waiting for the Linux MOUNT v3 server to return a usable root filehandle for '"
                + exportPath
                + "'. Last status was '"
                + lastResult?.Status.ToString()
                + "'.");
        }

        internal static async Task<byte[]> ResolveLinuxExportRootV40Async(
            string host,
            int nfsPort,
            CancellationToken cancellationToken)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            Exception? lastException = null;

            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using OpenNfsClient client = new OpenNfsClientBuilder()
                        .WithServer(host, nfsPort)
                        .Build();
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    return await ResolveLinuxExportRootV40Async(client, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for the Linux NFSv4.0 server to return a usable root filehandle.", lastException);
        }
    }
}
